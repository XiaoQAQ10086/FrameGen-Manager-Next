using FrameGenManagerNext.Core.Features.Dlss5;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Services;

/// <summary>
/// Retries ReShade hotkey writes that were deferred because the game was running
/// or ReShade.ini was locked.
/// </summary>
public sealed class HotkeyQueueWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IReShadeConfigService _config;
    private readonly ILogger<HotkeyQueueWorker> _logger;

    public HotkeyQueueWorker(
        IReShadeConfigService config,
        ILogger<HotkeyQueueWorker> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var report = await _config.FlushPendingAsync(stoppingToken);
                if (report.Applied > 0)
                {
                    _logger.LogInformation(
                        "Applied {Applied} queued ReShade hotkey writes",
                        report.Applied);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not flush queued ReShade hotkey writes");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
