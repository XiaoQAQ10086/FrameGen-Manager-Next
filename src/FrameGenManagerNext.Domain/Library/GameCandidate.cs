namespace FrameGenManagerNext.Domain.Library;

public sealed record GameCandidate(
    string Name,
    string InstallDirectory,
    LauncherKind Launcher,
    string? AppId);
