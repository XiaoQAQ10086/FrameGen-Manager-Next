namespace FrameGenManagerNext.Domain.Ini;

/// <summary>
/// One INI override or stock value.
/// </summary>
public sealed record IniSetting(string Section, string Key, string Value);
