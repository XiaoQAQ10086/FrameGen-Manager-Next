using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Updates;

namespace FrameGenManagerNext.Core.Features.Updates;

public interface IUpdateFeed
{
    Task<Result<UpdateManifest>> CheckAsync(CancellationToken cancellationToken);

    Task<Result<string>> DownloadInstallerAsync(
        UpdateManifest manifest,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken);
}
