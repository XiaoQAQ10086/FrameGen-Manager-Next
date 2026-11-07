namespace FrameGenManagerNext.Core.Features.Dlss5;

public interface IReShadeConfigService
{
    Task MergeSearchPathsAsync(string targetDirectory, CancellationToken cancellationToken);

    Task EnsureOverlayKeyAsync(
        string targetDirectory,
        int virtualKey,
        CancellationToken cancellationToken);
}
