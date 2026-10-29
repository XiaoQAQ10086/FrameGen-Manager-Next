namespace FrameGenManagerNext.Domain.Packs;

public sealed class AssetPackStep
{
    public string Type { get; init; } = string.Empty;

    public Dictionary<string, string>? Arguments { get; init; }
}
