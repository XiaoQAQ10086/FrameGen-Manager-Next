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
                    launchMode = LaunchModeOf(game),
                    available = false,
                    gate = gpu.SmoothMotion.ToString(),
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
                    launchMode = LaunchModeOf(game),
                    available = false,
                    gate = gpu.SmoothMotion.ToString(),
                    state = inspection.Error!.Code.ToString(),
                    route = "Unknown",
                    component = "Unknown",
                    enabled = (bool?)null,
                    profile = (string?)null,
                    detail = DescribeError(inspection.Error, string.Empty),
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
                launchMode = LaunchModeOf(game),
                available = true,
                gate = gpu.SmoothMotion.ToString(),
                state = "Ready",
                route = inspection.Value.Route.ToString(),
                component = inspection.Value.FileState.ToString(),
                enabled = inspection.Value.Setting.Enabled,
                profile = inspection.Value.Setting.ProfileName,
                detail = inspection.Value.Setting.Detail,
                antiCheat = inspection.Value.AntiCheat,
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
                _logger.LogWarning(
                    "sm.enable failed game={Game} target={Target} code={Code} diagnostic={Diagnostic} context={Context}",
                    game.Name,
                    game.TargetDirectory,
                    result.Error!.Code,
                    result.Error.Diagnostic,
                    ContextText(result.Error.Context));
                throw new BridgeException(
                    result.Error!.Code,
                    DescribeError(result.Error, "enable failed"),
                    result.Error.Context);
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
                _logger.LogWarning(
                    "sm.disable failed game={Game} target={Target} code={Code} diagnostic={Diagnostic} context={Context}",
                    game.Name,
                    game.TargetDirectory,
                    result.Error!.Code,
                    result.Error.Diagnostic,
                    ContextText(result.Error.Context));
                throw new BridgeException(
                    result.Error!.Code,
                    DescribeError(result.Error, "disable failed"),
                    result.Error.Context);
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
                _logger.LogWarning(
                    "sm.restore failed game={Game} target={Target} code={Code} diagnostic={Diagnostic} context={Context}",
                    game.Name,
                    game.TargetDirectory,
                    result.Error!.Code,
                    result.Error.Diagnostic,
                    ContextText(result.Error.Context));
                throw new BridgeException(
                    result.Error!.Code,
                    DescribeError(result.Error, "restore failed"),
                    result.Error.Context);
            }

            return await BuildSmoothMotionStateAsync();
        });
    }

    private static string DescribeError(Domain.Errors.AppError error, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(error.Diagnostic))
        {
            return error.Diagnostic;
        }

        if (error.Context.TryGetValue("reason", out var reason) &&
            !string.IsNullOrWhiteSpace(reason))
        {
            return reason;
        }

        return string.IsNullOrWhiteSpace(fallback)
            ? error.Code.ToString()
            : fallback;
    }

    private static string ContextText(IReadOnlyDictionary<string, string>? context)
    {
        return context is null || context.Count == 0
            ? string.Empty
            : string.Join("; ", context.Select(pair => $"{pair.Key}={pair.Value}"));
    }
}
