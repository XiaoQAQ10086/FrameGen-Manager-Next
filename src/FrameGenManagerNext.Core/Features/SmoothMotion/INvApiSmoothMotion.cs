namespace FrameGenManagerNext.Core.Features.SmoothMotion;

public interface INvApiSmoothMotion
{
    Task<SmoothMotionSettingState> GetStateAsync(
        string exePath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads several executables with a single NVAPI/DRS session. Opening one
    /// session per game makes the driver load its whole profile database repeatedly.
    /// </summary>
    Task<IReadOnlyList<SmoothMotionSettingState>> GetStatesAsync(
        IReadOnlyList<string> exePaths,
        CancellationToken cancellationToken);

    Task<SmoothMotionChangeResult> SetAsync(
        string game,
        string targetDirectory,
        string exePath,
        bool enabled,
        CancellationToken cancellationToken);

    Task<SmoothMotionChangeResult> RestoreAsync(
        string game,
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken);
}
