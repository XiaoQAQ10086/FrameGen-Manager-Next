namespace FrameGenManagerNext.Domain.Gpu;

public enum GpuSpoofRestoreTarget
{
    DriverName,
    OriginalValue,
}

public sealed record GpuSpoofAdapter(
    string EnumKey,
    string DriverName,
    string CurrentName,
    string HardwareId,
    bool Spoofed,
    bool HasOriginalBackup,
    string? OriginalValue);

public sealed record GpuSpoofState(
    bool IsElevated,
    IReadOnlyList<GpuSpoofAdapter> Adapters,
    string? Error = null)
{
    public GpuSpoofAdapter? Primary => Adapters.Count > 0 ? Adapters[0] : null;

    public bool CanModify => Primary is not null;
}

public sealed record GpuSpoofOperationReport(
    string Message,
    GpuSpoofState State);

public static class GpuSpoofPresets
{
    /// <summary>
    /// Whitelist written into the registry. Older builds offered more entries,
    /// so they stay accepted even though the UI exposes two.
    /// </summary>
    public static readonly IReadOnlyList<string> Allowed =
    [
        "NVIDIA GeForce RTX 5090",
        "NVIDIA GeForce RTX 5080",
        "NVIDIA GeForce RTX 5070 Ti",
        "NVIDIA GeForce RTX 5070",
        "NVIDIA GeForce RTX 5060 Ti",
        "NVIDIA GeForce RTX 5060",
    ];

    public static readonly IReadOnlyList<string> UiChoices =
    [
        "NVIDIA GeForce RTX 5090",
        "NVIDIA GeForce RTX 5060",
    ];

    public static bool IsAllowed(string name)
    {
        return Allowed.Contains(name, StringComparer.Ordinal);
    }
}
