using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Library;

/// <summary>
/// Scans Xbox app installations. Xbox games use a Content folder and may live on
/// removable drives; the generic loose scanner intentionally skips Content.
/// </summary>
public sealed class XboxGameScanner : IGameScanner
{
    private const int MaxGames = 200;

    private static readonly string[] Markers =
    [
        "MicrosoftGame.config",
        "AppxManifest.xml",
    ];

    private readonly ILogger<XboxGameScanner> _logger;

    public XboxGameScanner(ILogger<XboxGameScanner> logger)
    {
        _logger = logger;
    }

    public LauncherKind Launcher => LauncherKind.Xbox;

    public Task<IReadOnlyList<GameCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<GameCandidate>>(() => Scan(), cancellationToken);
    }

    public IReadOnlyList<GameCandidate> ScanFromRoot(string root)
    {
        var results = new List<GameCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateDirectories(root).Take(MaxGames).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not enumerate Xbox root {Root}", root);
            return results;
        }

        foreach (var child in children)
        {
            var name = Path.GetFileName(child);
            if (string.IsNullOrWhiteSpace(name) ||
                !seen.Add(Path.GetFullPath(child)) ||
                !LooksLikeXboxGame(child))
            {
                continue;
            }

            results.Add(new GameCandidate(name, child, LauncherKind.Xbox, AppId: null));
        }

        return results
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool LooksLikeXboxGame(string directory)
    {
        // Xbox app installs place the game under <Game>\Content. The marker
        // files are also present for packaged titles.
        if (Directory.Exists(Path.Combine(directory, "Content")))
        {
            return true;
        }

        foreach (var marker in Markers)
        {
            if (File.Exists(Path.Combine(directory, marker)))
            {
                return true;
            }
        }

        return false;
    }

    private List<GameCandidate> Scan()
    {
        var results = new List<GameCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady ||
                    drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                {
                    continue;
                }

                var root = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var game in ScanFromRoot(root))
                {
                    if (seen.Add(Path.GetFullPath(game.InstallDirectory)))
                    {
                        results.Add(game);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not scan Xbox drive {Drive}", drive.Name);
            }
        }

        return results
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
