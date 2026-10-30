namespace FrameGenManagerNext.Core.Features.Maintenance;

public sealed record AssetMaintenanceProgress(
    AssetFamily Family,
    string GameName,
    int Current,
    int Total,
    string Phase);
