namespace FrameGenManagerNext.Domain.Library;

public sealed class GameLibrary
{
    public int SchemaVersion { get; init; } = 1;

    public DateTimeOffset? LastScanUtc { get; init; }

    public List<GameEntry> Scanned { get; init; } = [];

    public List<GameEntry> Manual { get; init; } = [];

    public List<string> IgnoredIds { get; init; } = [];

    public IEnumerable<GameEntry> EnumerateVisibleGames()
    {
        var ignored = new HashSet<string>(IgnoredIds, StringComparer.OrdinalIgnoreCase);
        var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in Manual.Concat(Scanned))
        {
            if (ignored.Contains(game.Id))
            {
                continue;
            }

            if (seenDirectories.Add(Path.GetFullPath(game.InstallDirectory)))
            {
                yield return game;
            }
        }
    }

    public IEnumerable<GameEntry> EnumerateRemovedGames()
    {
        var ignored = new HashSet<string>(IgnoredIds, StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in Manual.Concat(Scanned))
        {
            if (ignored.Contains(game.Id) && seenIds.Add(game.Id))
            {
                yield return game;
            }
        }
    }
}
