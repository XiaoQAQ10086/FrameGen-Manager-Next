using FrameGenManagerNext.Domain.Deployment;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed partial class DeploymentEngine
{
    public async Task<IReadOnlyDictionary<string, string>?> LoadOptionsAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        var target = ValidateTarget(targetDirectory);
        if (target.IsFailure)
        {
            return null;
        }

        var manifest = await _backups.LoadManifestAsync(familyId, target.Value, cancellationToken);
        if (manifest is null ||
            manifest.State is DeploymentManifestState.Restored or DeploymentManifestState.RolledBack ||
            manifest.Options.Count == 0)
        {
            return null;
        }

        return manifest.Options;
    }

    public Task<DeploymentManifest?> LoadManifestAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        var target = ValidateTarget(targetDirectory);
        return target.IsFailure
            ? Task.FromResult<DeploymentManifest?>(null)
            : _backups.LoadManifestAsync(familyId, target.Value, cancellationToken);
    }

}
