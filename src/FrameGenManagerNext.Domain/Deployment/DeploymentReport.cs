namespace FrameGenManagerNext.Domain.Deployment;

public sealed record DeploymentReport(
    DeploymentState State,
    int FilesWritten,
    int FilesBackedUp,
    IReadOnlyList<string> Warnings);
