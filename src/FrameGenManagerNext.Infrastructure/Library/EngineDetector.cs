using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Infrastructure.Library;

/// <summary>
/// Detects the engine from files that the game ships, not from the publisher
/// name. REFramework is only relevant for RE Engine; MT Framework titles such as
/// Monster Hunter: World must not get dinput8.dll from that pack.
/// </summary>
public static class EngineDetector
{
    private static readonly string[] ReEngineMarkers =
    [
        "re_chunk_000.pak",
        "re_dlc_000.pak",
    ];

    private static readonly string[] MtFrameworkExecutables =
    [
        "mhw",
        "mhworld",
        "re5",
        "re6",
        "dmc4",
    ];

    public static GameEngine Detect(
        string installDirectory,
        string? targetDirectory,
        string? exePath = null)
    {
        foreach (var directory in CandidateDirectories(installDirectory, targetDirectory))
        {
            if (HasReEngineMarker(directory))
            {
                return GameEngine.ReEngine;
            }
        }

        var exeName = Path.GetFileNameWithoutExtension(exePath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(exeName) &&
            MtFrameworkExecutables.Contains(exeName, StringComparer.OrdinalIgnoreCase))
        {
            return GameEngine.MtFramework;
        }

        return GameEngine.Unknown;
    }

    private static IEnumerable<string> CandidateDirectories(
        string installDirectory,
        string? targetDirectory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { targetDirectory, installDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var directory = start;
            for (var depth = 0; depth < 4 && !string.IsNullOrWhiteSpace(directory); depth++)
            {
                if (seen.Add(directory))
                {
                    yield return directory;
                }

                directory = Path.GetDirectoryName(directory);
            }
        }
    }

    private static bool HasReEngineMarker(string directory)
    {
        try
        {
            if (ReEngineMarkers.Any(marker =>
                    File.Exists(Path.Combine(directory, marker))))
            {
                return true;
            }

            return Directory.EnumerateFiles(
                    directory,
                    "re_chunk_000.pak.patch_*.pak",
                    SearchOption.TopDirectoryOnly)
                .Any();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
