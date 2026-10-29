using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FrameGenManagerNext.Infrastructure.Library;

/// <summary>
/// Best-effort WeGame scan. WeGame has no stable public manifest format; this reads the
/// install root from the registry and inspects the rail_apps / WeGameApps folders.
/// </summary>
public sealed class WeGameGameScanner : IGameScanner
{
    private static readonly string[] ChildRoots = ["rail_apps", "WeGameApps"];

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Download",
        "Downloads",
        "Temp",
        "Cache",
        "Logs",
        "Common",
    };

    private readonly ILogger<WeGameGameScanner> _logger;

    public WeGameGameScanner(ILogger<WeGameGameScanner> logger)
    {
        _logger = logger;
    }

    public LauncherKind Launcher => LauncherKind.WeGame;

    public Task<IReadOnlyList<GameCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var root = FindWeGameRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            return Task.FromResult<IReadOnlyList<GameCandidate>>([]);
        }

        return Task.Run(() => ScanFromRoot(root), cancellationToken);
    }

    public IReadOnlyList<GameCandidate> ScanFromRoot(string weGameRoot)
    {
        var results = new List<GameCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var childRootName in ChildRoots)
        {
            var childRoot = Path.Combine(weGameRoot, childRootName);
            if (!Directory.Exists(childRoot))
            {
                continue;
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(childRoot))
                {
                    var name = Path.GetFileName(directory);
                    if (string.IsNullOrWhiteSpace(name) ||
                        SkippedDirectories.Contains(name) ||
                        !seen.Add(Path.GetFullPath(directory)) ||
                        !ContainsExecutable(directory, maxDepth: 3))
                    {
                        continue;
                    }

                    results.Add(new GameCandidate(name, directory, LauncherKind.WeGame, name));
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not enumerate WeGame apps in {Root}", childRoot);
            }
        }

        return results
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool ContainsExecutable(string directory, int maxDepth)
    {
        var stack = new Stack<(string Directory, int Depth)>();
        stack.Push((directory, 0));
        var inspected = 0;

        while (stack.Count > 0 && inspected < 2_000)
        {
            var (current, depth) = stack.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(current);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                inspected++;
                if (inspected >= 2_000)
                {
                    break;
                }

                if (Directory.Exists(entry))
                {
                    if (depth < maxDepth && !SkippedDirectories.Contains(Path.GetFileName(entry)))
                    {
                        stack.Push((entry, depth + 1));
                    }

                    continue;
                }

                if (string.Equals(Path.GetExtension(entry), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? FindWeGameRoot()
    {
        foreach (var valueName in new[] { "InstallPath", "InstallPathNew", "InstallRoot", "WeGameInstallPath" })
        {
            using var currentUser = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\WeGame");
            if (currentUser?.GetValue(valueName) is string currentUserPath &&
                Directory.Exists(currentUserPath))
            {
                return Path.GetFullPath(currentUserPath);
            }

            using var localMachine = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Tencent\WeGame");
            if (localMachine?.GetValue(valueName) is string localMachinePath &&
                Directory.Exists(localMachinePath))
            {
                return Path.GetFullPath(localMachinePath);
            }
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Tencent",
            "WeGame");
        return Directory.Exists(fallback) ? fallback : null;
    }
}
