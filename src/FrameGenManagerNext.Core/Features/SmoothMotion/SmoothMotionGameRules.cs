namespace FrameGenManagerNext.Core.Features.SmoothMotion;

/// <summary>
/// Game-folder rules that must be checked before deploying the Smooth Motion
/// proxy. Kernel anti-cheat loads its own modules into the game process and
/// blocks the proxy; leaving it in the folder can also make the game crash.
/// </summary>
public static class SmoothMotionGameRules
{
    private static readonly HashSet<string> AntiCheatDirectories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "BattlEye",
            "EasyAntiCheat",
            "EasyAntiCheat_EOS",
            "GameGuard",
            "XignCode",
        };

    private static readonly HashSet<string> AntiCheatFiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "BEService.exe",
            "BEDaisy.sys",
            "vgk.sys",
            "start_protected_game.exe",
            "x3.xem",
        };

    public static bool HasAntiCheat(string? targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory) ||
            !Directory.Exists(targetDirectory))
        {
            return false;
        }

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(targetDirectory))
            {
                if (AntiCheatDirectories.Contains(Path.GetFileName(directory)))
                {
                    return true;
                }
            }

            foreach (var file in Directory.EnumerateFiles(targetDirectory))
            {
                if (IsAntiCheatFile(Path.GetFileName(file)))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable folder cannot prove the game is clean, but treating
            // every permission error as anti-cheat would block protected Xbox
            // installs for the wrong reason. The deployment engine reports the
            // real access error later.
            return false;
        }
    }

    private static bool IsAntiCheatFile(string name)
    {
        return AntiCheatFiles.Contains(name) ||
               (name.StartsWith("BEClient", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ||
               (name.StartsWith("EasyAntiCheat", StringComparison.OrdinalIgnoreCase) &&
                (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                 name.EndsWith(".sys", StringComparison.OrdinalIgnoreCase)));
    }
}
