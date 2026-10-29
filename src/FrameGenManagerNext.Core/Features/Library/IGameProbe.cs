using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Library;

public interface IGameProbe
{
    Task<GameEntry> ProbeAsync(GameCandidate candidate, CancellationToken cancellationToken);

    Task<GameEntry?> ProbeManualAsync(string exePath, CancellationToken cancellationToken);
}
