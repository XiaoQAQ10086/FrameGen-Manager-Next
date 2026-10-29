using FrameGenManagerNext.Domain.Gpu;

namespace FrameGenManagerNext.Core.Features.Gpu;

public interface IGpuService
{
    Task<GpuInfo> GetAsync(CancellationToken cancellationToken);
}
