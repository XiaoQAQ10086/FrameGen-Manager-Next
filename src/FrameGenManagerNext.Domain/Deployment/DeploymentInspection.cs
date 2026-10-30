namespace FrameGenManagerNext.Domain.Deployment;

public sealed record DeploymentInspection(
    DeploymentState State,
    string? PackVersion,
    bool HasManifest,
    IReadOnlyList<string> Details,
    IReadOnlyDictionary<string, string>? Options = null,
    bool FromLegacyTool = false);
