namespace FrameGenManagerNext.Core.Features.Dlss5;

public interface IReShadeConfigService
{
    Task MergeSearchPathsAsync(string targetDirectory, CancellationToken cancellationToken);

    Task<HotkeyWriteResult> EnsureOverlayKeyAsync(
        string targetDirectory,
        string? exePath,
        int virtualKey,
        CancellationToken cancellationToken);

    Task<HotkeyFlushReport> FlushPendingAsync(CancellationToken cancellationToken);
}
