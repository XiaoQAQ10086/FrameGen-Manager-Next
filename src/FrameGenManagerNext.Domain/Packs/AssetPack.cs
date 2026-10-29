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

    public AssetPackIni? Ini { get; init; }

    public List<AssetPackStep> Steps { get; init; } = [];

    public AssetPackTrust Trust { get; init; } = AssetPackTrust.Bundled;
}
