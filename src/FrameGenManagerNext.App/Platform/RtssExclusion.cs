using System.IO;

namespace FrameGenManagerNext.App.Platform;

internal readonly record struct RtssExclusionResult(
    bool Success,
    bool Changed,
    string Detail);

/// <summary>
/// RTSS/Afterburner injects RTSSHooks64.dll into msedgewebview2.exe. The bundled
/// browser profiles in RTSS exclude chrome.exe and msedge.exe but not
/// msedgewebview2.exe, which can crash the browser process. Add the same exclusion
/// profile when possible; do not touch an existing user profile.
/// </summary>
internal static class RtssExclusion
{
    private const string ProfileFileName = "msedgewebview2.exe.cfg";
    private const string ProfileContent = "[Hooking]\r\nEnableHooking\t\t= 0\r\n";

    public static RtssExclusionResult Ensure()
    {
        var root = FindRtssRoot();
        if (root is null)
        {
            return new RtssExclusionResult(true, false, "RTSS is not installed.");
        }

        var changed = false;
        var failures = new List<string>();
        foreach (var folder in new[] { "Profiles", "ProfileTemplates" })
        {
            var directory = Path.Combine(root, folder);
            var path = Path.Combine(directory, ProfileFileName);
            if (File.Exists(path))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, ProfileContent);
                changed = true;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                failures.Add($"{folder}: {exception.Message}");
            }
        }

        return failures.Count == 0
            ? new RtssExclusionResult(true, changed, changed ? "RTSS exclusion written." : "RTSS exclusion already present.")
            : new RtssExclusionResult(false, changed, string.Join("; ", failures));
    }

    private static string? FindRtssRoot()
    {
        foreach (var baseDirectory in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 })
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                continue;
            }

            var candidate = Path.Combine(baseDirectory, "RivaTuner Statistics Server");
            if (File.Exists(Path.Combine(candidate, "RTSS.exe")))
            {
                return candidate;
            }
        }

        return null;
    }
}
