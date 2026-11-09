using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Library;

/// <summary>
/// Decides how a library entry should be started. Launcher-aware games use their
/// platform protocol so Steam/Epic can set up DRM and online services; everything
/// else runs the render executable directly.
/// </summary>
public static class GameLaunchPlanner
{
    public static GameLaunchKind Resolve(GameEntry game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return game.Launcher switch
        {
            LauncherKind.Steam when int.TryParse(game.AppId, out _) => GameLaunchKind.Steam,
            LauncherKind.Epic when !string.IsNullOrWhiteSpace(game.AppId) => GameLaunchKind.Epic,
            LauncherKind.Xbox => GameLaunchKind.None,
            _ when !string.IsNullOrWhiteSpace(game.RenderExePath) => GameLaunchKind.Executable,
            _ => GameLaunchKind.None,
        };
    }
}
