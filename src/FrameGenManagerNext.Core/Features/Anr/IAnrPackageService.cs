using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Anr;

public interface IAnrPackageService
{
    Task<Result<AnrPackageInfo>> EnsureLatestAsync(
        IProgress<AnrDownloadProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result<AnrReleaseInfo>> CheckLatestAsync(CancellationToken cancellationToken);

    Task<Result<AnrPackageInfo>> ImportAsync(
        string zipPath,
        CancellationToken cancellationToken);

    Task<AnrPackageInfo?> GetCachedAsync(CancellationToken cancellationToken);
}
