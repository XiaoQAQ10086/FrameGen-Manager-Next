using System.Text.Json;
using FrameGenManagerNext.Domain.Deployment;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> BuildDlss5StateAsync()
    {
        var library = await _library.LoadAsync(CancellationToken.None);
        var games = new List<object>();

        foreach (var game in library.EnumerateVisibleGames())
        {
            if (string.IsNullOrWhiteSpace(game.RenderExePath))
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    targetDirectory = game.TargetDirectory,
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
                CancellationToken.None);
            if (inspection.IsFailure)
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    targetDirectory = game.TargetDirectory,
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
                available = true,
                state = inspection.Value.FileState.ToString(),
                api = inspection.Value.Api.ToString(),
                reShade = inspection.Value.ReShadePresent,
                legacy = inspection.Value.LegacyRenoDxPresent,
                leftovers = inspection.Value.HasLeftovers,
            });
        }

        return new { games };
    }

    private async Task<object?> InstallDlss5Async(JsonElement payload)
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
            var result = await _dlss5.InstallAsync(
                game.TargetDirectory,
                game.RenderExePath,
                progress,
                CancellationToken.None);
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
}
