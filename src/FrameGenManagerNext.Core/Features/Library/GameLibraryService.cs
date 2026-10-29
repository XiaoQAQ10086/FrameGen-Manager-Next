using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Library;

public sealed partial class GameLibraryService : IGameLibraryService, IDisposable
{
    private readonly List<IGameScanner> _scanners;
    private readonly IGameProbe _probe;
    private readonly IGameLibraryStore _store;
    private readonly ILogger<GameLibraryService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public GameLibraryService(
        IEnumerable<IGameScanner> scanners,
        IGameProbe probe,
        IGameLibraryStore store,
        ILogger<GameLibraryService> logger)
    {
        _scanners = scanners.ToList();
        _probe = probe;
        _store = store;
        _logger = logger;
    }

    public Task<GameLibrary> LoadAsync(CancellationToken cancellationToken)
    {
        return _store.LoadAsync(cancellationToken);
    }

    public async Task<GameScanResult> ScanAsync(
        IProgress<GameScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var existing = await _store.LoadAsync(cancellationToken);
        var warnings = new List<string>();
        var candidates = new List<GameCandidate>();

        for (var index = 0; index < _scanners.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scanner = _scanners[index];
            progress?.Report(new GameScanProgress(
                "launchers",
                scanner.Launcher.ToString(),
                index,
                _scanners.Count));

            try
            {
                var found = await scanner.ScanAsync(cancellationToken);
                candidates.AddRange(found);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var warning = $"{scanner.Launcher}: {exception.Message}";
                warnings.Add(warning);
                LogLauncherScanFailed(_logger, exception, scanner.Launcher);
            }
        }

        var unique = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.InstallDirectory))
            .Where(candidate => Directory.Exists(candidate.InstallDirectory))
            .GroupBy(
                candidate => Path.GetFullPath(candidate.InstallDirectory),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(candidate => LauncherPriority(candidate.Launcher))
                .First())
            .OrderBy(candidate => candidate.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var entries = new List<GameEntry>();
        var completed = 0;
        using var gate = new SemaphoreSlim(4);

        var tasks = unique.Select(async candidate =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var entry = await _probe.ProbeAsync(candidate, cancellationToken);
                lock (entries)
                {
                    entries.Add(entry);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                lock (warnings)
                {
                    warnings.Add($"{candidate.Name}: {exception.Message}");
                }

                LogGameProbeFailed(_logger, exception, candidate.Name);
            }
            finally
            {
                gate.Release();
                var done = Interlocked.Increment(ref completed);
                progress?.Report(new GameScanProgress("games", candidate.Name, done, unique.Count));
            }
        });

        await Task.WhenAll(tasks);

        var manualDirectories = new HashSet<string>(
            existing.Manual.Select(game => Path.GetFullPath(game.InstallDirectory)),
            StringComparer.OrdinalIgnoreCase);

        var scanned = entries
            .Where(entry => !manualDirectories.Contains(Path.GetFullPath(entry.InstallDirectory)))
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var library = new GameLibrary
        {
            SchemaVersion = 1,
            LastScanUtc = DateTimeOffset.UtcNow,
            Scanned = scanned,
            Manual = existing.Manual,
            IgnoredIds = existing.IgnoredIds,
        };

        await SaveAsync(library, cancellationToken);
        progress?.Report(new GameScanProgress("done", string.Empty, scanned.Count, scanned.Count));

        return new GameScanResult(library, warnings);
    }

    public async Task<GameLibrary> AddManualAsync(string exePath, CancellationToken cancellationToken)
    {
        var entry = await _probe.ProbeManualAsync(exePath, cancellationToken);
        if (entry is null)
        {
            throw new ArgumentException("The selected file is not a usable game executable.", nameof(exePath));
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var library = await _store.LoadAsync(cancellationToken);
            var manual = library.Manual
                .Where(game => !string.Equals(
                    Path.GetFullPath(game.TargetDirectory),
                    Path.GetFullPath(entry.TargetDirectory),
                    StringComparison.OrdinalIgnoreCase))
                .Append(entry)
                .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var updated = new GameLibrary
            {
                SchemaVersion = library.SchemaVersion,
                LastScanUtc = library.LastScanUtc,
                Scanned = library.Scanned,
                Manual = manual,
                IgnoredIds = library.IgnoredIds.Where(id => !string.Equals(id, entry.Id, StringComparison.OrdinalIgnoreCase)).ToList(),
            };

            await _store.SaveAsync(updated, cancellationToken);
            return updated;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<GameLibrary> RemoveAsync(string gameId, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var library = await _store.LoadAsync(cancellationToken);
            var exists = library.Manual
                .Concat(library.Scanned)
                .Any(game => string.Equals(game.Id, gameId, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                return library;
            }

            // Manual entries stay in the library so the removed-games view can
            // restore them; the ignored id hides them from the normal list.
            var ignored = library.IgnoredIds
                .Append(gameId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var updated = new GameLibrary
            {
                SchemaVersion = library.SchemaVersion,
                LastScanUtc = library.LastScanUtc,
                Scanned = library.Scanned,
                Manual = library.Manual,
                IgnoredIds = ignored,
            };

            await _store.SaveAsync(updated, cancellationToken);
            return updated;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<GameLibrary> SetIgnoredAsync(
        string gameId,
        bool ignored,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var library = await _store.LoadAsync(cancellationToken);
            var ids = new HashSet<string>(library.IgnoredIds, StringComparer.OrdinalIgnoreCase);

            if (ignored)
            {
                ids.Add(gameId);
            }
            else
            {
                ids.Remove(gameId);
            }

            var updated = new GameLibrary
            {
                SchemaVersion = library.SchemaVersion,
                LastScanUtc = library.LastScanUtc,
                Scanned = library.Scanned,
                Manual = library.Manual,
                IgnoredIds = ids.ToList(),
            };

            await _store.SaveAsync(updated, cancellationToken);
            return updated;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task SaveAsync(GameLibrary library, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _store.SaveAsync(library, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Launcher scan failed: {Launcher}")]
    private static partial void LogLauncherScanFailed(
        ILogger logger,
        Exception exception,
        LauncherKind launcher);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Game probe failed: {Game}")]
    private static partial void LogGameProbeFailed(
        ILogger logger,
        Exception exception,
        string game);

    private static int LauncherPriority(LauncherKind launcher)
    {
        return launcher switch
        {
            LauncherKind.Steam => 0,
            LauncherKind.Epic => 1,
            LauncherKind.WeGame => 2,
            LauncherKind.Loose => 3,
            LauncherKind.Manual => 4,
            _ => 5,
        };
    }
}
