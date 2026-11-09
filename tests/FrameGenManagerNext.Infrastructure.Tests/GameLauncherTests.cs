using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class GameLauncherTests
{
    [Fact]
    public async Task XboxGameReportsUnsupportedInsteadOfStartingAnExe()
    {
        var launcher = new GameLauncher(NullLogger<GameLauncher>.Instance);
        var game = Game(LauncherKind.Xbox, null, @"D:\XboxGames\Game\Content\game.exe");

        var result = await launcher.LaunchAsync(game, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(ErrorCode.LaunchUnsupported);
    }

    [Fact]
    public async Task MissingRenderExecutableReportsFileNotFound()
    {
        var launcher = new GameLauncher(NullLogger<GameLauncher>.Instance);
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "game.exe");
        var game = Game(LauncherKind.Manual, null, missing);

        var result = await launcher.LaunchAsync(game, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(ErrorCode.FileNotFound);
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
