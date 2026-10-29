using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Gpu;

public interface IGpuSpoofService
{
    Task<GpuSpoofState> InspectAsync(CancellationToken cancellationToken);

    Task<Result<GpuSpoofOperationReport>> ApplyAsync(
        string presetName,
        CancellationToken cancellationToken);

    Task<Result<GpuSpoofOperationReport>> RestoreAsync(
        GpuSpoofRestoreTarget target,
        CancellationToken cancellationToken);
}
