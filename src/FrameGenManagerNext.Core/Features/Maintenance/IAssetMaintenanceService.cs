namespace FrameGenManagerNext.Core.Features.Maintenance;

public interface IAssetMaintenanceService
{
    Task<AssetUpdateReport> DetectAsync(CancellationToken cancellationToken);

    Task<AssetClearResult> ClearAsync(
        AssetUpdateReport report,
        IProgress<AssetMaintenanceProgress>? progress,
        CancellationToken cancellationToken);
}
