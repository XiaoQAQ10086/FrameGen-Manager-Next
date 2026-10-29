namespace FrameGenManagerNext.Domain.Deployment;

public enum DeploymentManifestState
{
    Installing,
    Installed,
    RolledBack,
    Partial,
    Restored,
}
