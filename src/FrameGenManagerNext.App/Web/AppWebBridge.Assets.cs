using FrameGenManagerNext.Core.Features.Maintenance;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> CheckAssetUpdatesAsync()
    {
        var report = await _maintenance.DetectAsync(CancellationToken.None);
        _lastAssetReport = report;
        return ToAssetUpdateDto(report);
    }

    private async Task<object?> ClearAssetUpdatesAsync()
    {
        // Re-read manifests before clearing: a game may have been updated
        // manually from the card while the prompt was waiting.
        var report = await _maintenance.DetectAsync(CancellationToken.None);
        _lastAssetReport = report;

        if (!report.HasUpdates)
        {
            return new
            {
                cleared = 0,
                failed = 0,
                cancelled = false,
                failures = Array.Empty<object>(),
                warnings = Array.Empty<string>(),
            };
        }

        return await RunExclusiveAsync(async () =>
        {
            _assetClearCancellation = new CancellationTokenSource();
            try
            {
                var progress = new Progress<AssetMaintenanceProgress>(update =>
                {
                    SendEvent("asset-progress", new
                    {
                        family = update.Family.ToString(),
                        game = update.GameName,
                        current = update.Current,
                        total = update.Total,
                        phase = update.Phase,
                    });
                });

                var result = await _maintenance.ClearAsync(
                    report,
                    progress,
                    _assetClearCancellation.Token);

                _lastAssetReport = null;
                return new
                {
                    cleared = result.ClearedGames,
                    failed = result.FailedGames,
                    cancelled = result.Cancelled,
                    failures = result.Failures.Select(failure => new
                    {
                        family = failure.Family.ToString(),
                        gameId = failure.GameId,
                        gameName = failure.GameName,
                        targetDirectory = failure.TargetDirectory,
                        code = failure.Code.ToString(),
                        detail = failure.Detail,
                        remaining = failure.Remaining,
                    }).ToList(),
                    warnings = result.Warnings,
                };
            }
            finally
            {
                _assetClearCancellation?.Dispose();
                _assetClearCancellation = null;
            }
        });
    }

    private object? CancelAssetClear()
    {
        _assetClearCancellation?.Cancel();
        return new { cancelled = true };
    }

    private static object ToAssetUpdateDto(AssetUpdateReport report)
    {
        return new
        {
            groups = report.Groups.Select(group => new
            {
                family = group.Family.ToString(),
                currentVersion = group.CurrentVersion,
                games = group.Games.Select(game => new
                {
                    id = game.GameId,
                    name = game.Name,
                    targetDirectory = game.TargetDirectory,
                    renderExe = game.RenderExePath,
                    installedVersion = game.InstalledVersion,
                    currentVersion = game.CurrentVersion,
                }).ToList(),
            }).ToList(),
            totalGames = report.TotalGames,
        };
    }
}
