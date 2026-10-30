namespace FrameGenManagerNext.Domain.Library;

public sealed class GameLibrary
{
    public int SchemaVersion { get; init; } = 1;

    public DateTimeOffset? LastScanUtc { get; init; }

    public List<GameEntry> Scanned { get; init; } = [];

    public List<GameEntry> Manual { get; init; } = [];

    public List<string> IgnoredIds { get; init; } = [];

    /// <summary>
    /// User-corrected render executable per game id. A scan may pick the wrong
    /// subfolder; this keeps the correction across rescans and restarts.
    /// </summary>
    public Dictionary<string, string> TargetOverrides { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

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

            var effective = ApplyTargetOverride(game);
            if (seenDirectories.Add(Path.GetFullPath(effective.InstallDirectory)))
            {
                yield return effective;
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
                yield return ApplyTargetOverride(game);
            }
        }
    }

    private GameEntry ApplyTargetOverride(GameEntry game)
    {
        if (!TargetOverrides.TryGetValue(game.Id, out var exePath) ||
            string.IsNullOrWhiteSpace(exePath))
        {
            return game;
        }

        var directory = Path.GetDirectoryName(exePath);
        return string.IsNullOrWhiteSpace(directory)
            ? game
            : game with { RenderExePath = exePath, TargetDirectory = directory };
    }
}
