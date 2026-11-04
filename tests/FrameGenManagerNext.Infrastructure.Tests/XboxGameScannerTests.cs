using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class XboxGameScannerTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-xbox-scan-tests");

    [Fact]
    public void DetectsContentAndPackagedXboxGames()
    {
        _directory.WriteFile("XboxGames/Forza/Content/Forza.exe", "game");
        _directory.WriteFile("XboxGames/Packaged/MicrosoftGame.config", "config");
        _directory.WriteFile("XboxGames/NotAGame/readme.txt", "not a game");

        var games = new XboxGameScanner(NullLogger<XboxGameScanner>.Instance)
            .ScanFromRoot(_directory.GetPath("XboxGames"));

        games.Count.ShouldBe(2);
        games.Select(game => game.Name).ShouldBe(["Forza", "Packaged"]);
        games.ShouldAllBe(game => game.Launcher == LauncherKind.Xbox);
    }

    [Fact]
    public async Task ProbeFindsExecutableInsideXboxContent()
    {
        var exe = _directory.WriteFile("XboxGames/Forza/Content/bin/Forza.exe", new string('x', 128 * 1024));
        var candidate = new GameCandidate(
            "Forza",
            _directory.GetPath("XboxGames/Forza"),
            LauncherKind.Xbox,
            AppId: null);

        var entry = await new GameProbe(NullLogger<GameProbe>.Instance).ProbeAsync(
            candidate,
            TestContext.Current.CancellationToken);

        entry.RenderExePath.ShouldBe(exe);
        entry.Launcher.ShouldBe(LauncherKind.Xbox);
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
