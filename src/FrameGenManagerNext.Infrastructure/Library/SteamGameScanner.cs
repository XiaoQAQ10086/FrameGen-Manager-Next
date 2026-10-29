using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FrameGenManagerNext.Infrastructure.Library;

public sealed class SteamGameScanner : IGameScanner
{
    private static readonly HashSet<string> JunkNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "steamworks common redistributables",
        "steamvr",
        "steamvr beta",
        "proton experimental",
        "proton hotfix",
        "proton - experimental",
        "steam linux runtime",
    };

    private readonly ILogger<SteamGameScanner> _logger;

    public SteamGameScanner(ILogger<SteamGameScanner> logger)
    {
        _logger = logger;
    }

    public LauncherKind Launcher => LauncherKind.Steam;

    public Task<IReadOnlyList<GameCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var roots = FindSteamRoots();
        if (roots.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<GameCandidate>>([]);
        }

        return Task.Run(() => ScanRoots(roots), cancellationToken);
    }

    public IReadOnlyList<GameCandidate> ScanFromRoot(string steamRoot)
    {
        var results = new List<GameCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var libraryRoots = new List<string> { steamRoot };

        var libraryFolders = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFolders))
        {
            try
            {
                var vdf = ValveKeyValues.Parse(File.ReadAllText(libraryFolders));
                var paths = vdf.FindStringValues("path").ToList();
                if (paths.Count == 0)
                {
                    paths.AddRange(vdf.Values
                        .Where(pair => pair.Key.All(char.IsAsciiDigit) && pair.Value is string)
                        .Select(pair => (string)pair.Value));
                }

                libraryRoots.AddRange(paths);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not parse Steam libraryfolders.vdf");
            }
        }

        foreach (var libraryRoot in libraryRoots
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(TryGetFullPath)
                     .OfType<string>()
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var steamApps = Path.Combine(libraryRoot, "steamapps");
            if (!Directory.Exists(steamApps))
            {
                continue;
            }

            try
            {
                foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
                {
                    try
                    {
                        var appState = ReadAppState(manifest);
                        if (appState is null)
                        {
                            continue;
                        }

                        var appId = GetString(appState, "appid");
                        var name = GetString(appState, "name");
                        var installDir = GetString(appState, "installdir");
                        if (string.IsNullOrWhiteSpace(name) ||
                            string.IsNullOrWhiteSpace(installDir) ||
                            IsJunk(name))
                        {
                            continue;
                        }

                        var installPath = Path.Combine(steamApps, "common", installDir);
                        if (!Directory.Exists(installPath) || !seen.Add(Path.GetFullPath(installPath)))
                        {
                            continue;
                        }

                        results.Add(new GameCandidate(name, installPath, LauncherKind.Steam, appId));
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "Could not parse Steam manifest {Manifest}", manifest);
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not enumerate Steam apps in {Root}", steamApps);
            }
        }

        return results
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<GameCandidate> ScanRoots(IReadOnlyList<string> roots)
    {
        var games = new Dictionary<string, GameCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            foreach (var game in ScanFromRoot(root))
            {
                var key = Path.GetFullPath(game.InstallDirectory);
                games.TryAdd(key, game);
            }
        }

        return games.Values
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static ValveKeyValues? ReadAppState(string manifestPath)
    {
        var vdf = ValveKeyValues.Parse(File.ReadAllText(manifestPath));
        if (vdf.Values.TryGetValue("AppState", out var appState) && appState is ValveKeyValues nested)
        {
            return nested;
        }

        return vdf;
    }

    private static string? GetString(ValveKeyValues values, string key)
    {
        return values.Values.TryGetValue(key, out var value) ? value as string : null;
    }

    private static bool IsJunk(string name)
    {
        var trimmed = name.Trim();
        if (JunkNames.Contains(trimmed))
        {
            return true;
        }

        if (trimmed.StartsWith("steam linux runtime ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.StartsWith("proton ", StringComparison.OrdinalIgnoreCase))
        {
            const int prefixLength = 7;
            return trimmed.Length > prefixLength && char.IsAsciiDigit(trimmed[prefixLength]);
        }

        return false;
    }

    private static List<string> FindSteamRoots()
    {
        var roots = new List<string>();

        AddIfDirectory(roots, ReadRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"));
        AddIfDirectory(roots, ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"));
        AddIfDirectory(roots, ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"));

        var steamExe = ReadRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamExe");
        if (!string.IsNullOrWhiteSpace(steamExe))
        {
            AddIfDirectory(roots, Path.GetDirectoryName(steamExe));
        }

        foreach (var hive in new[] { Registry.LocalMachine })
        {
            foreach (var uninstallPath in new[]
                     {
                         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                         @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                     })
            {
                AddSteamUninstallRoots(roots, hive, uninstallPath);
            }
        }

        AddIfDirectory(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam"));
        AddIfDirectory(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam"));

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddSteamUninstallRoots(
        List<string> roots,
        RegistryKey hive,
        string uninstallPath)
    {
        try
        {
            using var uninstall = hive.OpenSubKey(uninstallPath);
            if (uninstall is null)
            {
                return;
            }

            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var entry = uninstall.OpenSubKey(name);
                var displayName = entry?.GetValue("DisplayName") as string;
                if (!string.Equals(displayName?.Trim(), "Steam", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var location = entry?.GetValue("InstallLocation") as string;
                if (string.IsNullOrWhiteSpace(location))
                {
                    continue;
                }

                if (Directory.Exists(Path.Combine(location, "steamapps")) ||
                    File.Exists(Path.Combine(location, "steam.exe")))
                {
                    AddIfDirectory(roots, location);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Registry entries are best-effort.
        }
    }

    private static string? ReadRegistryString(RegistryKey hive, string path, string valueName)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void AddIfDirectory(List<string> roots, string? path)
    {
        var fullPath = TryGetFullPath(path);
        if (fullPath is not null && Directory.Exists(fullPath))
        {
            roots.Add(fullPath);
        }
    }

    private static string? TryGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
