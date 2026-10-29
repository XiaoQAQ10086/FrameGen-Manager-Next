namespace FrameGenManagerNext.Domain.Deployment;

public sealed record DeploymentFilePlan(
    string RelativePath,
    string? SourcePath,
    byte[]? Content,
    string Sha256,
    long Size,
    string Role);
