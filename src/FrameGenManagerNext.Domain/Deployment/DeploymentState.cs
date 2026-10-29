namespace FrameGenManagerNext.Domain.Deployment;

public enum DeploymentState
{
    NotDeployed = 0,
    Installed,
    Outdated,
    Partial,
    Conflict,
    InstalledWithoutManifest,
}
