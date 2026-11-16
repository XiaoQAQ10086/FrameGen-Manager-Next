namespace FrameGenManagerNext.Core.Features.Dlss5;

public static class Dlss5ModuleCleanupPolicy
{
    /// <summary>
    /// A proxy module can only be deleted when it was not part of the game or the
    /// deployment manifest before this uninstall, and its contents are the exact
    /// ReShade module from the bundled setup. This prevents deleting a ReShade-based
    /// third-party mod (for example Snowy Moon) that uses the same proxy name.
    /// </summary>
    public static bool CanDelete(
        bool existedBefore,
        bool recordedInManifest,
        string? currentHash,
        string? expectedHash)
    {
        if (existedBefore || recordedInManifest)
        {
            return false;
        }

        return currentHash is not null &&
               expectedHash is not null &&
               string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
