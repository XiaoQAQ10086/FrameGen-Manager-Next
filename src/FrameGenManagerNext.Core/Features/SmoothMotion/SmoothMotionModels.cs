using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Gpu;

namespace FrameGenManagerNext.Core.Features.SmoothMotion;

public sealed record SmoothMotionSettingState(
    bool? Enabled,
    string ProfileName,
    bool IsOurs,
    string? Detail);

public sealed record SmoothMotionChangeResult(
    bool Ok,
    uint? ReadBack,
    string ProfileName,
    string Message);

public sealed record SmoothMotionInspection(
    DeploymentState FileState,
    GpuRoute Route,
    string PackId,
    SmoothMotionSettingState Setting);

public sealed record SmoothMotionReport(
    DeploymentReport FileReport,
    SmoothMotionChangeResult Setting,
    string Message);

public sealed record SmoothMotionRestoreReport(
    RestoreReport FileRestore,
    SmoothMotionChangeResult Setting,
    string Message);
