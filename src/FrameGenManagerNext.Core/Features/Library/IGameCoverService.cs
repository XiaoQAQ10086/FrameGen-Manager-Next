using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Library;

public interface IGameCoverService
{
    Task<string?> GetCoverPathAsync(GameEntry game, CancellationToken cancellationToken, bool allowDownload = true);
}
