namespace FrameGenManagerNext.Domain.Deployment;

public sealed record DeploymentInspection(
    DeploymentState State,
    string? PackVersion,
    bool HasManifest,
    IReadOnlyList<string> Details);
