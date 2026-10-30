namespace FrameGenManagerNext.Domain.Deployment;

public sealed record DeployedFileRecord(
    string RelativePath,
    string Sha256,
    long Size,
    bool ExistedBefore,
    string? BackupRelativePath,
    string Role)
{
    /// <summary>
    /// Last-write time captured when the file was deployed. Inspection can skip
    /// hashing large runtime files when size and timestamp still match.
    /// </summary>
    public long? LastWriteTimeUtcTicks { get; init; }
}
