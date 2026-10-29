namespace FrameGenManagerNext.Domain.Packs;

public sealed class AssetPackFile
{
    public string Source { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public bool Required { get; init; } = true;

    public bool ProxySlot { get; init; }

    public string? Sha256 { get; init; }

    public long? Size { get; init; }
}
