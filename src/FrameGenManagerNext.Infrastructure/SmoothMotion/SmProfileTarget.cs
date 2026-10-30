namespace FrameGenManagerNext.Infrastructure.SmoothMotion;

/// <summary>
/// One driver profile touched for a game, with the value that was there before we
/// wrote Smooth Motion. A single executable can belong to several profiles.
/// </summary>
public sealed record SmProfileTarget(
    string Profile,
    bool Ours,
    bool Had,
    uint? HadValue);
