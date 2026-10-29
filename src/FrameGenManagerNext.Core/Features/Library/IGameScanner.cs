using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Library;

/// <summary>
/// Read-only launcher scanner. Implementations live in Infrastructure.
/// </summary>
public interface IGameScanner
{
    LauncherKind Launcher { get; }

    Task<IReadOnlyList<GameCandidate>> ScanAsync(CancellationToken cancellationToken);
}
