namespace FrameGenManagerNext.Domain.Gpu;

public static class GpuHagsStateClassifier
{
    /// <summary>
    /// Combines the live WDDM caps result with the explicit HwSchMode registry
    /// override. A live-enabled adapter wins; a saved mode of 2 with a live
    /// disabled result means the setting is on but has not taken effect yet.
    /// </summary>
    public static GpuHagsState Classify(
        bool? liveEnabled,
        bool liveSupported,
        int? registryMode)
    {
        return liveEnabled switch
        {
            true => GpuHagsState.Enabled,
            false when registryMode == 2 => GpuHagsState.PendingReboot,
            false when registryMode == 1 => GpuHagsState.Disabled,
            false => liveSupported ? GpuHagsState.Disabled : GpuHagsState.Unsupported,
            null when registryMode == 2 => GpuHagsState.Enabled,
            null when registryMode == 1 => GpuHagsState.Disabled,
            _ => GpuHagsState.Unknown,
        };
    }
}
