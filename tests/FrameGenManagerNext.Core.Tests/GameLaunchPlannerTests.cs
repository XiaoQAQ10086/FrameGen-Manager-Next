using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class GameLaunchPlannerTests
{
    [Fact]
    public void SteamGameWithAppIdUsesSteamProtocol()
    {
        var game = Game(LauncherKind.Steam, "730", @"C:\Games\CS2\game\bin\win64\cs2.exe");

        GameLaunchPlanner.Resolve(game).ShouldBe(GameLaunchKind.Steam);
    }

    [Fact]
    public void SteamGameWithoutAppIdFallsBackToExecutable()
    {
        var game = Game(LauncherKind.Steam, null, @"C:\Games\CS2\game\bin\win64\cs2.exe");

        GameLaunchPlanner.Resolve(game).ShouldBe(GameLaunchKind.Executable);
    }

    [Fact]
    public void EpicGameWithAppNameUsesEpicProtocol()
    {
        var game = Game(LauncherKind.Epic, "HogwartsLegacy", @"D:\Epic\Hogwarts\Binaries\Win64\Hogwarts.exe");

        GameLaunchPlanner.Resolve(game).ShouldBe(GameLaunchKind.Epic);
    }

    [Fact]
    public void XboxGameIsNotLaunchableDirectly()
    {
        var game = Game(LauncherKind.Xbox, null, @"D:\XboxGames\Game\Content\game.exe");

        GameLaunchPlanner.Resolve(game).ShouldBe(GameLaunchKind.None);
    }

    [Fact]
    public void ManualGameRunsSelectedExecutable()
    {
        var game = Game(LauncherKind.Manual, null, @"D:\Games\MyGame\MyGame.exe");

        GameLaunchPlanner.Resolve(game).ShouldBe(GameLaunchKind.Executable);
    }

    [Fact]
    public void GameWithoutExecutableIsNotLaunchable()
    {
        var game = Game(LauncherKind.Loose, null, null);

        GameLaunchPlanner.Resolve(game).ShouldBe(GameLaunchKind.None);
    }

    private static GameEntry Game(LauncherKind launcher, string? appId, string? renderExe)
    {
        return new GameEntry(
            "game",
            "Game",
            launcher,
            @"C:\Games",
            renderExe,
            @"C:\Games",
            GraphicsApi.Direct3D11,
            appId,
            IsManual: launcher == LauncherKind.Manual);
    }
}
