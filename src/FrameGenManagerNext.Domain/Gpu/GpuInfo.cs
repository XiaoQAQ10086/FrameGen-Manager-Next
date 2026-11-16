namespace FrameGenManagerNext.Domain.Gpu;

public sealed record GpuInfo(
    string Name,
    string? DriverVersion,
    GpuRoute Route,
    GpuHagsState HardwareAcceleratedGpuScheduling,
    GpuFeatureGate FrameGeneration,
    GpuFeatureGate SmoothMotion,
    string? Diagnostic = null);
