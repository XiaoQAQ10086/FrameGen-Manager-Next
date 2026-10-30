namespace FrameGenManagerNext.Domain.Packs;

public sealed class AssetPack
{
    public int SchemaVersion { get; init; }

    public string Id { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string MinAppVersion { get; init; } = string.Empty;

    public List<string> GpuRoutes { get; init; } = [];

    public List<AssetPackFile> Files { get; init; } = [];

    /// <summary>
    /// Relative directories owned entirely by this deployment (for example the
    /// FrameGen runtime folder). The plugin writes logs and caches there after
    /// installation, so restore and cleanup remove them recursively.
    /// </summary>
    public List<string> OwnedDirectories { get; init; } = [];

    public AssetPackIni? Ini { get; init; }

    public List<AssetPackStep> Steps { get; init; } = [];

    public AssetPackTrust Trust { get; init; } = AssetPackTrust.Bundled;
}
