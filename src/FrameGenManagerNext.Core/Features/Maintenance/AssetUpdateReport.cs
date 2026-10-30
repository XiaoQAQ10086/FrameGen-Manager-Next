namespace FrameGenManagerNext.Core.Features.Maintenance;

public sealed record AssetUpdateReport(IReadOnlyList<AssetUpdateGroup> Groups)
{
    public bool HasUpdates => Groups.Count > 0;

    public int TotalGames => Groups.Sum(group => group.Games.Count);
}
