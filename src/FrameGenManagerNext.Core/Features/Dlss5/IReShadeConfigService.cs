namespace FrameGenManagerNext.Core.Features.Dlss5;

public interface IReShadeConfigService
{
    Task MergeSearchPathsAsync(string targetDirectory, CancellationToken cancellationToken);
}
