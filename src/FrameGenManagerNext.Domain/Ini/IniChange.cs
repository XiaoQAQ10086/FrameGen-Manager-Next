namespace FrameGenManagerNext.Domain.Ini;

/// <summary>
/// Describes what an override did to the document.
/// </summary>
public sealed record IniChange(
    string Section,
    string Key,
    string? OldValue,
    string NewValue,
    bool Applied);
