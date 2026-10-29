using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public interface IGraphicsApiProbe
{
    Task<GraphicsApi> DetectAsync(string exePath, CancellationToken cancellationToken);
}
