using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Domain.Ini;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Dlss5;

public sealed class ReShadeConfigService : IReShadeConfigService, IDisposable
{
    private readonly HotkeyQueueStore _queue;
    private readonly IGameProcessProbe _processes;
    private readonly ILogger<ReShadeConfigService> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public ReShadeConfigService(
        HotkeyQueueStore queue,
        IGameProcessProbe processes,
        ILogger<ReShadeConfigService> logger)
    {
        _queue = queue;
        _processes = processes;
        _logger = logger;
    }

    public async Task MergeSearchPathsAsync(
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var iniPath = Path.Combine(targetDirectory, "ReShade.ini");
        if (!File.Exists(iniPath))
        {
            return;
        }

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var text = await File.ReadAllTextAsync(iniPath, cancellationToken);
            var document = IniDocument.Parse(text);

            document.SetValue("General", "EffectSearchPaths", @".\reshade-shaders\Shaders\**");
            document.SetValue("General", "TextureSearchPaths", @".\reshade-shaders\Textures\**");

            await File.WriteAllTextAsync(iniPath, document.ToText("\r\n"), cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<HotkeyWriteResult> EnsureOverlayKeyAsync(
        string targetDirectory,
        string? exePath,
        int virtualKey,
        CancellationToken cancellationToken)
    {
        if (virtualKey is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualKey));
        }

        if (!string.IsNullOrWhiteSpace(exePath) && _processes.IsRunning(exePath))
        {
            await _queue.EnqueueAsync(targetDirectory, exePath, virtualKey, cancellationToken);
            _logger.LogInformation(
                "ReShade overlay key queued because the game is running target={Target}",
                targetDirectory);
            return new HotkeyWriteResult(Applied: false, Queued: true);
        }

        try
        {
            await WriteOverlayKeyGuardedAsync(targetDirectory, virtualKey, cancellationToken);
            await _queue.RemoveAsync(targetDirectory, cancellationToken);
            return new HotkeyWriteResult(Applied: true, Queued: false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            await _queue.EnqueueAsync(targetDirectory, exePath, virtualKey, cancellationToken);
            _logger.LogInformation(
                exception,
                "ReShade overlay key could not be written; queued for retry target={Target}",
                targetDirectory);
            return new HotkeyWriteResult(Applied: false, Queued: true);
        }
    }

    public async Task<HotkeyFlushReport> FlushPendingAsync(CancellationToken cancellationToken)
    {
        var items = await _queue.ReadAsync(cancellationToken);
        var applied = 0;
        var remaining = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iniPath = Path.Combine(item.TargetDirectory, "ReShade.ini");
            if (!Directory.Exists(item.TargetDirectory) || !File.Exists(iniPath))
            {
                await _queue.RemoveAsync(item.TargetDirectory, cancellationToken);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.ExePath) && _processes.IsRunning(item.ExePath))
            {
                remaining++;
                continue;
            }

            try
            {
                await WriteOverlayKeyGuardedAsync(item.TargetDirectory, item.VirtualKey, cancellationToken);
                await _queue.RemoveAsync(item.TargetDirectory, cancellationToken);
                applied++;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
            {
                await _queue.MarkFailureAsync(item.TargetDirectory, exception.Message, cancellationToken);
                remaining++;
            }
        }

        return new HotkeyFlushReport(applied, remaining);
    }

    public void Dispose()
    {
        _writeGate.Dispose();
    }

    private async Task WriteOverlayKeyGuardedAsync(
        string targetDirectory,
        int virtualKey,
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await WriteOverlayKeyAsync(targetDirectory, virtualKey, cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static async Task WriteOverlayKeyAsync(
        string targetDirectory,
        int virtualKey,
        CancellationToken cancellationToken)
    {
        var iniPath = Path.Combine(targetDirectory, "ReShade.ini");
        var document = File.Exists(iniPath)
            ? IniDocument.Parse(await File.ReadAllTextAsync(iniPath, cancellationToken))
            : IniDocument.Parse(string.Empty);
        document.SetValue("INPUT", "KeyOverlay", $"{virtualKey},0,0,0");
        await File.WriteAllTextAsync(iniPath, document.ToText("\r\n"), cancellationToken);
    }
}
