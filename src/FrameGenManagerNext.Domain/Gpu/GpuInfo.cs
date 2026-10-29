namespace FrameGenManagerNext.Domain.Gpu;

public sealed record GpuInfo(
    string Name,
    string? DriverVersion,
    GpuRoute Route,
    bool? HardwareAcceleratedGpuScheduling,
    GpuFeatureGate FrameGeneration,
    GpuFeatureGate SmoothMotion,
    string? Diagnostic = null);
