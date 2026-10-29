namespace FrameGenManagerNext.Core.Features.Framegen;

public sealed record FrameGenOptions(
    string ProxySlot = "version.dll",
    int Optimized = 1,
    int MaxGeneratedFrames = 3,
    string Preset = "A",
    string Engine = "DLSS",
    string SuperResolutionMode = "Off",
    string XeSsFlowResolution = "Middle",
    string XeSsHudless = "Auto")
{
    public static FrameGenOptions Default { get; } = new();
}
