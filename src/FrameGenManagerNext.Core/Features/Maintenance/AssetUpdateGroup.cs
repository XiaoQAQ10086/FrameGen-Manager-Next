namespace FrameGenManagerNext.Core.Features.Maintenance;

public sealed record AssetUpdateGroup(
    AssetFamily Family,
    string? CurrentVersion,
    IReadOnlyList<AssetUpdateGame> Games);
