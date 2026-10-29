using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public interface IDlss5DeploymentService
{
    Task<Result<Dlss5Inspection>> InspectAsync(
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken);

    Task<Result<Dlss5Report>> InstallAsync(
        string targetDirectory,
        string exePath,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result<Dlss5RestoreReport>> RestoreAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result<Dlss5UninstallReport>> UninstallAsync(
        string targetDirectory,
        string? exePath,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);
}
