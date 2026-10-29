using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.SmoothMotion;

public interface ISmoothMotionDeploymentService
{
    Task<Result<SmoothMotionInspection>> InspectAsync(
        string game,
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken);

    Task<Result<SmoothMotionReport>> EnableAsync(
        string game,
        string targetDirectory,
        string exePath,
        IProgress<Domain.Deployment.DeploymentProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result<SmoothMotionChangeResult>> DisableAsync(
        string game,
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken);

    Task<Result<SmoothMotionRestoreReport>> RestoreAsync(
        string game,
        string targetDirectory,
        string exePath,
        IProgress<Domain.Deployment.DeploymentProgress>? progress,
        CancellationToken cancellationToken);
}
