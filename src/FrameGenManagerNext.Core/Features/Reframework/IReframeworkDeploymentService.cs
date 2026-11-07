using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Reframework;

public interface IReframeworkDeploymentService
{
    Task<Result<DeploymentInspection>> InspectAsync(
        string targetDirectory,
        CancellationToken cancellationToken);

    Task<Result<DeploymentReport>> InstallAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result<RestoreReport>> UninstallAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);
}
