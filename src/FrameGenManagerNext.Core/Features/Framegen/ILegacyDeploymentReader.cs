namespace FrameGenManagerNext.Core.Features.Framegen;

/// <summary>
/// Read-only view of deployments made by the old FrameGen-Manager. The old data
/// directory is never modified: it is only used to recognise and clean files that
/// the new app did not deploy.
/// </summary>
public interface ILegacyDeploymentReader
{
    Task<LegacyDeploymentInfo?> FindAsync(string targetDirectory, CancellationToken cancellationToken);
}
