namespace FrameGenManagerNext.Core.Features.SmoothMotion;

public interface INvApiSmoothMotion
{
    Task<SmoothMotionSettingState> GetStateAsync(
        string exePath,
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
