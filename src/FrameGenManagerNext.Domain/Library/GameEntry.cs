namespace FrameGenManagerNext.Domain.Library;

public sealed record GameEntry(
    string Id,
    string Name,
    LauncherKind Launcher,
    string InstallDirectory,
    string? RenderExePath,
    string TargetDirectory,
    GraphicsApi GraphicsApi,
    string? AppId,
    bool IsManual);
