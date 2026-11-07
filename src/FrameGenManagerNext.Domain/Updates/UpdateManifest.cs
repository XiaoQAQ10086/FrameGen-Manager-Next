namespace FrameGenManagerNext.Domain.Updates;

/// <summary>
/// Version and installer metadata fetched from the update feed. The installer
/// must match <see cref="InstallerSha256"/> and <see cref="InstallerSize"/> before
/// it is allowed to run; mirrors are only transport, never trust anchors.
/// </summary>
public sealed record UpdateManifest(
    string Version,
    string InstallerName,
    long InstallerSize,
    string InstallerSha256,
    IReadOnlyList<string> InstallerUrls,
    string? Notes = null,
    string? PageUrl = null);
