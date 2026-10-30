namespace FrameGenManagerNext.Core.Features.Framegen;

/// <summary>
/// A deployment recorded by the old tool's manifest. The new app does not import
/// backups; the file list is used only to recognise the files and clean them.
/// </summary>
public sealed record LegacyDeploymentInfo(
    string ManifestPath,
    string? ProxyName,
    bool Installing,
    IReadOnlyList<LegacyDeployedFile> Files)
{
    /// <summary>Normalized game directory recorded by the old tool.</summary>
    public string GameDirectory { get; init; } = string.Empty;
}
