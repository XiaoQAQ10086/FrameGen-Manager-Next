using System.Text.Json;
using FrameGenManagerNext.Domain.Deployment;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> BuildSmoothMotionStateAsync()
    {
        var settings = _settings.Load();
        var gpu = await _gpu.GetAsync(CancellationToken.None);
        var library = await _library.LoadAsync(CancellationToken.None);
        var games = new List<object>();

        foreach (var game in library.EnumerateVisibleGames())
        {
            var cover = settings.ShowCovers ? await GetCoverUrlAsync(game) : null;
            var initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(game.RenderExePath))
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    targetDirectory = game.TargetDirectory,
                    initial,
                    cover,
                    available = false,
                    state = "Unavailable",
                    route = "Unknown",
                    component = "Unknown",
                    enabled = (bool?)null,
                    profile = (string?)null,
                    detail = "未识别渲染 EXE",
                });
                continue;
            }

            var inspection = await _smoothMotion.InspectAsync(
                game.Name,
                game.TargetDirectory,
                game.RenderExePath,
                CancellationToken.None);

            if (inspection.IsFailure)
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    targetDirectory = game.TargetDirectory,
                    initial,
                    cover,
                    available = false,
                    state = inspection.Error!.Code.ToString(),
                    route = "Unknown",
                    component = "Unknown",
                    enabled = (bool?)null,
                    profile = (string?)null,
                    detail = inspection.Error.Diagnostic,
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
                available = true,
                state = "Ready",
                route = inspection.Value.Route.ToString(),
                component = inspection.Value.FileState.ToString(),
                enabled = inspection.Value.Setting.Enabled,
                profile = inspection.Value.Setting.ProfileName,
                detail = inspection.Value.Setting.Detail,
            });
        }

        return new
        {
            gpu = new
            {
                name = gpu.Name,
                driver = gpu.DriverVersion,
                route = gpu.Route.ToString(),
                gate = gpu.SmoothMotion.ToString(),
                minDriver = "616.64",
            },
            games,
            removed = await BuildRemovedGamesAsync(library),
        };
    }

    private async Task<object?> EnableSmoothMotionAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game?.RenderExePath is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "render executable not found");
        }

        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _smoothMotion.EnableAsync(
                game.Name,
                game.TargetDirectory,
                game.RenderExePath,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "enable failed");
            }

            return await BuildSmoothMotionStateAsync();
        });
    }

    private async Task<object?> DisableSmoothMotionAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game?.RenderExePath is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "render executable not found");
        }

        return await RunExclusiveAsync(async () =>
        {
            var result = await _smoothMotion.DisableAsync(
                game.Name,
                game.TargetDirectory,
                game.RenderExePath,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "disable failed");
            }

            return await BuildSmoothMotionStateAsync();
        });
    }

    private async Task<object?> RestoreSmoothMotionAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game?.RenderExePath is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "render executable not found");
        }

        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _smoothMotion.RestoreAsync(
                game.Name,
                game.TargetDirectory,
                game.RenderExePath,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "restore failed");
            }

            return await BuildSmoothMotionStateAsync();
        });
    }
}
