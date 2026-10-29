using FrameGenManagerNext.Domain.Gpu;

namespace FrameGenManagerNext.Core.Features.Gpu;

public sealed class GpuService : IGpuService
{
    private readonly IGpuProbe _probe;

    public GpuService(IGpuProbe probe)
    {
        _probe = probe;
    }

    public Task<GpuInfo> GetAsync(CancellationToken cancellationToken)
    {
        return _probe.GetAsync(cancellationToken);
    }
}
