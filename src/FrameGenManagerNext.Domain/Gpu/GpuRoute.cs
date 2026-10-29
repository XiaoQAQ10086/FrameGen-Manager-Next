namespace FrameGenManagerNext.Domain.Gpu;

/// <summary>
/// Hardware capability buckets used by the old tool and by pack gpuRoutes.
/// </summary>
public enum GpuRoute
{
    Unknown = 0,
    Sm86,
    Sm75,
    Gtx16,
    NoTensorCore,
    NotNeeded,
    Unsupported,
}
