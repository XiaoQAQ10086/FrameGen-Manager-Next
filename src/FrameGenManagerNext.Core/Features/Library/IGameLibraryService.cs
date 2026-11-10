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

    /// <summary>
    /// Permanently forgets a library entry. Game files and deployment backups are
    /// left untouched; a later scan can discover an installed game again.
    /// </summary>
    Task<GameLibrary> ForgetAsync(string gameId, CancellationToken cancellationToken);

    Task<GameLibrary> SetTargetAsync(string gameId, string exePath, CancellationToken cancellationToken);
}
