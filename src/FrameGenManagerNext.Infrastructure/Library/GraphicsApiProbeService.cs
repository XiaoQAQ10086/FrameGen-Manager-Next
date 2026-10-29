using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Infrastructure.Library;

public sealed class GraphicsApiProbeService : IGraphicsApiProbe
{
    public Task<GraphicsApi> DetectAsync(string exePath, CancellationToken cancellationToken)
    {
        return Task.Run(() => GameProbe.DetectApi(exePath), cancellationToken);
    }
}
