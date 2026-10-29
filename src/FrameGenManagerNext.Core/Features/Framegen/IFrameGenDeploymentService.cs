using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Framegen;

public interface IFrameGenDeploymentService
{
    Task<Result<DeploymentInspection>> InspectAsync(
        string targetDirectory,
        FrameGenOptions options,
        CancellationToken cancellationToken);

    Task<Result<DeploymentReport>> DeployAsync(
        string targetDirectory,
        FrameGenOptions options,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result<RestoreReport>> RestoreAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);
}
