using FrameGenManagerNext.Domain;
using Microsoft.Win32;

namespace FrameGenManagerNext.Infrastructure.Installation;

/// <summary>
/// Removes data that Velopack's uninstaller does not own: the LocalAppData
/// profile (config, backups, covers, WebView2 cache), update temp files and
/// the Start-menu tile metadata created for the installed executable.
/// </summary>
public static class UninstallCleanup
{
    private const string PackId = "FrameGenManagerNext";
    private const string TilePropertiesPath =
        @"Software\Microsoft\Windows\CurrentVersion\Start\TileProperties\W~velopack." + PackId;

    private static readonly string[] LegacyDataDirectoryNames = ["FrameGenNext"];

    public static void Run()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            DeleteDirectory(Path.Combine(localAppData, AppInfo.DataDirectoryName));
            foreach (var legacy in LegacyDataDirectoryNames)
            {
                DeleteDirectory(Path.Combine(localAppData, legacy));
            }
        }

        DeleteDirectory(Path.Combine(Path.GetTempPath(), AppInfo.DataDirectoryName));
        DeleteStartTileMetadata();
    }

    private static void DeleteStartTileMetadata()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(TilePropertiesPath, throwOnMissingSubKey: false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Windows may recreate this shell cache later; it contains no user data.
        }
    }

    private static void DeleteDirectory(string directory)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == 3)
                {
                    return;
                }

                ClearReadOnlyAttributes(directory);
                Thread.Sleep(150);
            }
        }
    }

    private static void ClearReadOnlyAttributes(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Continue with the remaining files; Directory.Delete retries later.
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The delete retry remains the best effort.
        }
    }
}
