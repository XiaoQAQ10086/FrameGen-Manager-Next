using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Deployment;

public interface IDeploymentEngine
{
    Task<Result<DeploymentInspection>> InspectAsync(
        DeploymentPlan plan,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>?> LoadOptionsAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken);

    Task<DeploymentManifest?> LoadManifestAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken);

    Task<Result<DeploymentReport>> DeployAsync(
        DeploymentPlan plan,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result<RestoreReport>> RestoreAsync(
        string familyId,
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);

    Task<Result> DiscardBackupAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken);
}
