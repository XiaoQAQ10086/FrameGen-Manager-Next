namespace FrameGenManagerNext.Domain.Deployment;

public sealed class DeploymentManifest
{
    public int SchemaVersion { get; init; } = 1;

    public string FamilyId { get; init; } = string.Empty;

    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string TargetDirectory { get; init; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; init; }

    public DeploymentManifestState State { get; init; }

    public List<DeployedFileRecord> Files { get; init; } = [];

    public Dictionary<string, string> Options { get; init; } = [];
}
