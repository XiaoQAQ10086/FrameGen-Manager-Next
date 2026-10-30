namespace FrameGenManagerNext.Core.Features.Maintenance;

public sealed record AssetUpdateGame(
    string GameId,
    string Name,
    string TargetDirectory,
    string? RenderExePath,
    string? InstalledVersion,
    string? CurrentVersion);
