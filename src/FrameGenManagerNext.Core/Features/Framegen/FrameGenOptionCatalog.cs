namespace FrameGenManagerNext.Core.Features.Framegen;

/// <summary>
/// Options accepted by the FrameGen pack. The UI and CLI use the same catalog.
/// </summary>
public static class FrameGenOptionCatalog
{
    public static IReadOnlyList<string> ProxySlots { get; } =
    [
        "version.dll",
        "winmm.dll",
        "dbghelp.dll",
        "dinput8.dll",
        "dxgi.dll",
        "d3d12.dll",
    ];

    public static IReadOnlyList<int> OptimizedLevels { get; } = [0, 1, 2, 3];

    public static IReadOnlyList<int> FrameMultipliers { get; } = [1, 2, 3, 5];

    public static IReadOnlyList<string> Presets { get; } = ["A", "B", "C"];

    public static IReadOnlyList<string> Engines { get; } = ["DLSS", "XeSS"];

    public static IReadOnlyList<string> SuperResolutionModes { get; } =
    [
        "Off",
        "M-INT8",
        "L-INT8",
        "M-FP16",
        "L-FP16",
    ];

    public static IReadOnlyList<string> XeSsFlowResolutions { get; } =
    [
        "Coarse",
        "Middle",
        "Fine",
    ];

    public static IReadOnlyList<string> XeSsHudlessValues { get; } = ["Auto", "1", "0"];
}
