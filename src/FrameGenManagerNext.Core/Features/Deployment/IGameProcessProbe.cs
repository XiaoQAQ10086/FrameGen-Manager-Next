namespace FrameGenManagerNext.Core.Features.Deployment;

public interface IGameProcessProbe
{
    /// <summary>
    /// Returns true when a process with the executable's file name is running.
    /// A path match is preferred, but an inaccessible path falls back to the name
    /// so a protected game process is not treated as stopped.
    /// </summary>
    bool IsRunning(string? exePath);
}
