using System.IO;
using System.Text.Json;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> BuildDlss5StateAsync()
    {
        var settings = _settings.Load();
        var library = await _library.LoadAsync(CancellationToken.None);
        var games = new List<object>();

        foreach (var game in library.EnumerateVisibleGames())
        {
            var cover = settings.ShowCovers ? await GetCoverUrlAsync(game) : null;
            var initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant();
            var targets = GetExeTargets(game.TargetDirectory);

            if (string.IsNullOrWhiteSpace(game.RenderExePath))
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    targetDirectory = game.TargetDirectory,
                    initial,
                    cover,
                    renderExe = game.RenderExePath,
                    targets,
                    available = false,
                    state = "Unavailable",
                    api = game.GraphicsApi.ToString(),
                    reShade = false,
                    legacy = false,
                    leftovers = false,
                });
                continue;
            }

            var inspection = await _dlss5.InspectAsync(
                game.TargetDirectory,
                game.RenderExePath,
                CancellationToken.None,
                game.GraphicsApi);
            if (inspection.IsFailure)
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    targetDirectory = game.TargetDirectory,
                    initial,
                    cover,
                    renderExe = game.RenderExePath,
                    targets,
                    available = false,
                    state = inspection.Error!.Code.ToString(),
                    api = game.GraphicsApi.ToString(),
                    reShade = false,
                    legacy = false,
                    leftovers = false,
                });
                continue;
            }

            games.Add(new
            {
                id = game.Id,
                name = game.Name,
                targetDirectory = game.TargetDirectory,
                initial,
                cover,
                renderExe = game.RenderExePath,
                targets,
                available = true,
                state = inspection.Value.FileState.ToString(),
                api = inspection.Value.Api.ToString(),
                reShade = inspection.Value.ReShadePresent,
                legacy = inspection.Value.LegacyRenoDxPresent,
                leftovers = inspection.Value.HasLeftovers,
            });
        }

        return new { games, removed = await BuildRemovedGamesAsync(library) };
    }

    private async Task<object?> InstallDlss5Async(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game?.RenderExePath is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "render executable not found");
        }

        var target = ResolveDeploymentTarget(game, payload);
        var installExe = target.ExePath ?? game.RenderExePath!;
        var knownApi = ReadGraphicsApi(payload);

        return await RunExclusiveAsync(async () =>
        {
            await PersistTargetAsync(game, target.ExePath);

            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _dlss5.InstallAsync(
                target.Directory,
                installExe,
                progress,
                CancellationToken.None,
                knownApi);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "install failed");
            }

            return await BuildDlss5StateAsync();
        });
    }

    private async Task<object?> RestoreDlss5Async(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _dlss5.RestoreAsync(
                game.TargetDirectory,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "restore failed");
            }

            return await BuildDlss5StateAsync();
        });
    }

    private async Task<object?> UninstallDlss5Async(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _dlss5.UninstallAsync(
                game.TargetDirectory,
                game.RenderExePath,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "uninstall failed");
            }

            return await BuildDlss5StateAsync();
        });
    }

    private static List<object> EnumerateExeTargets(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return [];
            }

            return Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                .Select(path => new { path, name = Path.GetFileName(path) })
                .OrderBy(item => item.name, StringComparer.CurrentCultureIgnoreCase)
                .Take(64)
                .Cast<object>()
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static GraphicsApi? ReadGraphicsApi(JsonElement payload)
    {
        var value = payload.TryGetProperty("api", out var element) ? element.GetString() : null;
        if (string.IsNullOrWhiteSpace(value) || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.ToUpperInvariant() switch
        {
            "DX9" or "D3D9" => GraphicsApi.Direct3D9,
            "DX10" or "D3D10" => GraphicsApi.Direct3D10,
            "DX11" or "D3D11" => GraphicsApi.Direct3D11,
            "DX12" or "D3D12" => GraphicsApi.Direct3D12,
            "VULKAN" => GraphicsApi.Vulkan,
            "OPENGL" or "GL" => GraphicsApi.OpenGl,
            _ => null,
        };
    }
}
