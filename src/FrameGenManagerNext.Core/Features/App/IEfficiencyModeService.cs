namespace FrameGenManagerNext.Core.Features.App;

/// <summary>
/// Windows 11 efficiency mode: EcoQoS plus idle priority while the window is not
/// in the foreground. Restores normal priority when the user returns to the app.
/// </summary>
public interface IEfficiencyModeService
{
    bool SetBackground(bool background);
}
