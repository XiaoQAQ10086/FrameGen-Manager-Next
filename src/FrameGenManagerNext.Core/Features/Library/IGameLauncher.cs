using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Library;

public interface IGameLauncher
{
    Task<Result> LaunchAsync(GameEntry game, CancellationToken cancellationToken);
}
