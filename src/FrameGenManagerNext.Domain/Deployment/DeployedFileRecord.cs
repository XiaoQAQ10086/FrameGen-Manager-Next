namespace FrameGenManagerNext.Domain.Deployment;

public sealed record DeployedFileRecord(
    string RelativePath,
    string Sha256,
    long Size,
    bool ExistedBefore,
    string? BackupRelativePath,
    string Role);
