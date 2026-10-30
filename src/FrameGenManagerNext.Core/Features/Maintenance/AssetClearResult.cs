namespace FrameGenManagerNext.Core.Features.Maintenance;

public sealed record AssetClearResult(
    int ClearedGames,
    bool Cancelled,
    IReadOnlyList<AssetClearFailure> Failures,
    IReadOnlyList<string> Warnings)
{
    public int FailedGames => Failures.Count;

    public bool IsClean => !Cancelled && Failures.Count == 0 && Warnings.Count == 0;
}
