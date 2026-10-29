namespace FrameGenManagerNext.Domain.Deployment;

public sealed record DeploymentProgress(
    string Phase,
    string Subject,
    int Current,
    int Total);
