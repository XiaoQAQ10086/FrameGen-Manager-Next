using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Library;

/// <summary>
/// Scans well-known loose-game folders on fixed drives. Read-only and capped to keep startup bounded.
/// </summary>
public sealed class LooseGameScanner : IGameScanner
{
    private const int MaxRoots = 500;
    private const int MaxChildrenPerRoot = 300;
    private const int MaxGames = 80;
    private const int MaxWalkEntries = 4_000;
    private const int MaxDepth = 3;

    private static readonly HashSet<string> LibraryRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "games",
        "game",
        "my games",
        "steamlibrary",
        "gog games",
        "epic games",
        "xboxgames",
        "origin games",
        "repacks",
        "游戏",
        "单机游戏",
        "游戏库",
    };

    private static readonly HashSet<string> NonGameDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "steamapps",
        "workshop",
        "downloading",
        "shadercache",
        "temp",
        "tmp",
        "backup",
        "backups",
        "redist",
        "redistributable",
        "directx",
        "vcredist",
        "content",
        "engine",
        "__macosx",
        "save",
        "saves",
        "gamesave",
        "gamesaves",
        "config",
        "mods",
        "mod",
        "logs",
        "log",
        "cache",
        "common",
    };

    private static readonly string[] NonGameExeStems =
    [
        "qq",
        "qqnt",
        "qqprotect",
        "wechat",
        "weixin",
        "tim",
        "wegame",
        "wegamex",
        "qqmusic",
        "qqplayer",
        "qqlive",
        "tencentvideo",
        "tencentmeeting",
        "txmeeting",
        "qqbrowser",
        "qqpinyin",
        "wechatocr",
        "tencentdocs",
    ];

    private static readonly string[] InstallerExeStems =
    [
        "unins",
        "uninstall",
        "setup",
        "install",
        "vcredist",
        "vc_redist",
        "dxsetup",
        "dxwebsetup",
        "oalinst",
        "crashpad",
        "crashreport",
        "crashhandler",
        "report",
        "launcher_installer",
        "update",
        "updater",
        "patcher",
        "helper",
    ];

    private readonly ILogger<LooseGameScanner> _logger;

    public LooseGameScanner(ILogger<LooseGameScanner> logger)
    {
        _logger = logger;
    }

    public LauncherKind Launcher => LauncherKind.Loose;

    public Task<IReadOnlyList<GameCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        return Task.Run(Scan, cancellationToken);
    }

    private IReadOnlyList<GameCandidate> Scan()
    {
        var roots = FindLibraryRoots();
        var games = new List<GameCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (games.Count >= MaxGames)
            {
                break;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(root).Take(MaxChildrenPerRoot).ToList();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not enumerate loose root {Root}", root);
                continue;
            }

            foreach (var child in children)
            {
                if (games.Count >= MaxGames)
                {
                    break;
                }

                var name = Path.GetFileName(child);
                if (string.IsNullOrWhiteSpace(name) ||
                    NonGameDirectories.Contains(name) ||
                    !seen.Add(Path.GetFullPath(child)) ||
                    !HasGameExecutable(child))
                {
                    continue;
                }

                games.Add(new GameCandidate(name, child, LauncherKind.Loose, AppId: null));
            }
        }

        return games
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private List<string> FindLibraryRoots()
    {
        var roots = new List<string>();
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not enumerate drives for loose games");
            return roots;
        }

        foreach (var drive in drives)
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            try
            {
                var children = Directory
                    .EnumerateDirectories(drive.RootDirectory.FullName)
                    .Take(MaxRoots);

                foreach (var child in children)
                {
                    if (LibraryRootNames.Contains(Path.GetFileName(child)))
                    {
                        roots.Add(child);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not enumerate drive {Drive}", drive.Name);
            }
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private bool HasGameExecutable(string gameDirectory)
    {
        var stack = new Stack<(string Directory, int Depth)>();
        stack.Push((gameDirectory, 0));
        var inspected = 0;

        while (stack.Count > 0 && inspected < MaxWalkEntries)
        {
            var (directory, depth) = stack.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                inspected++;
                if (inspected >= MaxWalkEntries)
                {
                    break;
                }

                if (Directory.Exists(entry))
                {
                    if (depth < MaxDepth &&
                        !NonGameDirectories.Contains(Path.GetFileName(entry)) &&
                        !IsReparsePoint(entry))
                    {
                        stack.Push((entry, depth + 1));
                    }

                    continue;
                }

                if (!string.Equals(Path.GetExtension(entry), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var stem = Path.GetFileNameWithoutExtension(entry);
                if (NonGameExeStems.Any(marker => string.Equals(stem, marker, StringComparison.OrdinalIgnoreCase)) ||
                    InstallerExeStems.Any(marker => stem.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                try
                {
                    if (new FileInfo(entry).Length >= 64 * 1024)
                    {
                        return true;
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _logger.LogDebug(exception, "Could not inspect {File}", entry);
                }
            }
        }

        return false;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            return true;
        }
    }
}
