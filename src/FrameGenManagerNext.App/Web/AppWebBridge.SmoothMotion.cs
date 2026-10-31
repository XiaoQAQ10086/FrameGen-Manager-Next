using System.Diagnostics;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Domain.Deployment;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> BuildSmoothMotionStateAsync()
    {
        var stateClock = Stopwatch.StartNew();
        var settings = _settings.Load();
        var gpu = await _gpu.GetAsync(CancellationToken.None);
        var library = await _library.LoadAsync(CancellationToken.None);
        var games = new List<object>();
        var allowCoverDownload = library.EnumerateVisibleGames().Count() <= 40;

        var exePaths = library.EnumerateVisibleGames()
            .Select(game => game.RenderExePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var smStates = new Dictionary<string, SmoothMotionSettingState>(StringComparer.OrdinalIgnoreCase);
        if (exePaths.Count > 0)
        {
            try
            {
                var batch = await _nvapiSmoothMotion.GetStatesAsync(exePaths, CancellationToken.None);
                for (var index = 0; index < exePaths.Count && index < batch.Count; index++)
                {
                    smStates[exePaths[index]] = batch[index];
                }
            }
            catch (Exception)
            {
                smStates.Clear();
            }
        }

        foreach (var game in library.EnumerateVisibleGames())
        {
            var cover = settings.ShowCovers ? await GetCoverUrlAsync(game, allowCoverDownload) : null;
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
                CancellationToken.None,
                smStates.TryGetValue(game.RenderExePath!, out var known) ? known : null);

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
                frameGenDeployed,
            });
        }

        _logger.LogInformation(
            "sm.state count={Count} elapsed={Elapsed}ms",
            games.Count,
            stateClock.ElapsedMilliseconds);

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
