namespace FrameGenManagerNext.Domain.Gpu;

/// <summary>
/// Result of checking whether a GPU/driver combination can use a feature.
/// </summary>
public enum GpuFeatureGate
{
    Unknown = 0,
    Supported,
    DriverTooOld,
    GpuNotSupported,
    GpuNotNeeded,
}
