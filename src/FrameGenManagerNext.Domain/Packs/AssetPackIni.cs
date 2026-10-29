using FrameGenManagerNext.Domain.Ini;

namespace FrameGenManagerNext.Domain.Packs;

public sealed class AssetPackIni
{
    public string Target { get; init; } = string.Empty;

    public string Template { get; init; } = string.Empty;

    public string Mode { get; init; } = "merge";

    public string? TemplateSha256 { get; init; }

    public long? TemplateSize { get; init; }

    public List<IniSetting> StockValues { get; init; } = [];
}
