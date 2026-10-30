namespace FrameGenManagerNext.Core.Features.App;

/// <summary>
/// Puts the current process into Windows 11 efficiency mode (EcoQoS) so the
/// shell and background scans yield CPU time to the game.
/// </summary>
public interface IEfficiencyModeService
{
    bool TryEnable();
}
