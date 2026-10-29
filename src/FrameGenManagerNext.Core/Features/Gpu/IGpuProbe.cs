using FrameGenManagerNext.Domain.Gpu;

namespace FrameGenManagerNext.Core.Features.Gpu;

public interface IGpuProbe
{
    Task<GpuInfo> GetAsync(CancellationToken cancellationToken);
}
