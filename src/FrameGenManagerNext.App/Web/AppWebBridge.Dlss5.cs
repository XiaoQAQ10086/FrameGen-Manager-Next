using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> BuildDlss5StateAsync(Dlss5UninstallReport? uninstall = null)
    {
        var stateClock = Stopwatch.StartNew();
        var settings = _settings.Load();
        var library = await _library.LoadAsync(CancellationToken.None);
        var games = new List<object>();
        var allowCoverDownload = library.EnumerateVisibleGames().Count() <= 40;

        foreach (var game in library.EnumerateVisibleGames())
        {
            var cover = settings.ShowCovers ? await GetCoverUrlAsync(game, allowCoverDownload) : null;
            var initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant();
            var targets = GetExeTargets(game.TargetDirectory);

            if (string.IsNullOrWhiteSpace(game.RenderExePath))
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    launcher = game.Launcher.ToString(),
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
                    legacyFiles = Array.Empty<string>(),
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
                    launcher = game.Launcher.ToString(),
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
                    legacyFiles = Array.Empty<string>(),
                    leftovers = false,
                });
                continue;
            }

            games.Add(new
            {
                id = game.Id,
                name = game.Name,
                launcher = game.Launcher.ToString(),
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
                legacyFiles = inspection.Value.LegacyFiles ?? Array.Empty<string>(),
                leftovers = inspection.Value.HasLeftovers,
            });
        }

        var removed = await BuildRemovedGamesAsync(library);
        _logger.LogInformation(
            "dlss5.state count={Count} elapsed={Elapsed}ms",
            games.Count,
            stateClock.ElapsedMilliseconds);
        return new
        {
            games,
            removed,
            uninstall = uninstall is null
                ? null
                : new
                {
                    removed = uninstall.Removed,
                    remaining = uninstall.Remaining,
                    clean = uninstall.IsClean,
                },
        };
    }

    private async Task<object?> InstallDlss5Async(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        var target = ResolveDeploymentTarget(game, payload);
        var installExe = target.ExePath ?? game.RenderExePath;
        if (string.IsNullOrWhiteSpace(installExe) || !File.Exists(installExe))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "render executable not found");
        }
        var knownApi = ReadGraphicsApi(payload);

        var apiModule = knownApi is null
            ? AutoProxyModule(game.GraphicsApi)
            : ProxyModule(knownApi.Value);
        if (apiModule is not null)
        {
            var frameGen = await _frameGen.InspectAsync(
                game.TargetDirectory,
                FrameGenOptions.Default,
                CancellationToken.None);
            var frameGenDeployed = frameGen.IsSuccess && frameGen.Value.State is
                DeploymentState.Installed or
                DeploymentState.Outdated or
                DeploymentState.Partial or
                DeploymentState.Conflict or
                DeploymentState.InstalledWithoutManifest;
            if (frameGenDeployed &&
                frameGen.Value.Options is not null &&
                frameGen.Value.Options.TryGetValue("proxySlot", out var proxySlot) &&
                string.Equals(proxySlot, apiModule, StringComparison.OrdinalIgnoreCase))
            {
                throw new BridgeException(
                    Domain.Errors.ErrorCode.Conflict,
                    $"frame generation already uses the {apiModule} proxy; choose another API or restore frame generation first");
            }
        }

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

            var report = result.Value;
            if (!report.IsClean)
            {
                _logger.LogWarning(
                    "dlss5.uninstall target={Target} removed={Removed} remaining={Remaining}",
                    game.TargetDirectory,
                    report.Removed.Count,
                    report.Remaining.Count);
            }

            return await BuildDlss5StateAsync(report);
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

    private static string? AutoProxyModule(GraphicsApi api)
    {
        return api switch
        {
            GraphicsApi.Direct3D9 => "d3d9.dll",
            GraphicsApi.Direct3D10 or GraphicsApi.Direct3D11 or GraphicsApi.Direct3D12 => "dxgi.dll",
            GraphicsApi.OpenGl => "opengl32.dll",
            _ => null,
        };
    }

    private static string? ProxyModule(GraphicsApi api)
    {
        return api switch
        {
            GraphicsApi.Direct3D9 => "d3d9.dll",
            GraphicsApi.Direct3D10 => "d3d10.dll",
            GraphicsApi.Direct3D11 => "d3d11.dll",
            GraphicsApi.Direct3D12 => "d3d12.dll",
            GraphicsApi.OpenGl => "opengl32.dll",
            _ => null,
        };
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
