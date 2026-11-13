using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Anr;

public interface IAnrDeploymentService
{
    Task<AnrInspection> InspectAsync(string targetDirectory, CancellationToken cancellationToken);

    Task<Result<DeploymentReport>> DeployAsync(
        string targetDirectory,
        string exePath,
        string? proxyModule,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result<RestoreReport>> RestoreAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);
}
