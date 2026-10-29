using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Library;

public interface IGameLibraryService
{
    Task<GameLibrary> LoadAsync(CancellationToken cancellationToken);

    Task<GameScanResult> ScanAsync(
        IProgress<GameScanProgress>? progress,
        CancellationToken cancellationToken);

    Task<GameLibrary> AddManualAsync(string exePath, CancellationToken cancellationToken);

    Task<GameLibrary> RemoveAsync(string gameId, CancellationToken cancellationToken);

    Task<GameLibrary> SetIgnoredAsync(string gameId, bool ignored, CancellationToken cancellationToken);
}
