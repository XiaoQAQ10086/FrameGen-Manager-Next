using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class GameProbeTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-game-probe-tests");

    [Fact]
    public async Task ProbePicksUnrealShippingExe()
    {
        var gameDirectory = _directory.CreateDirectory("game");
        _directory.WriteFile("game/Binaries/Win64/MyGame-Win64-Shipping.exe", "fake exe");
        _directory.WriteFile("game/MyGame.exe", "fake exe");
        _directory.WriteFile("game/Launcher.exe", "fake exe");
        _directory.WriteFile("game/UnityCrashHandler64.exe", "fake exe");

        var probe = new GameProbe(NullLogger<GameProbe>.Instance);
        var entry = await probe.ProbeAsync(
            new GameCandidate("My Game", gameDirectory, LauncherKind.Steam, "123"),
            TestContext.Current.CancellationToken);

        entry.Id.ShouldBe("steam:123");
        entry.RenderExePath.ShouldNotBeNull();
        Path.GetFileName(entry.RenderExePath).ShouldBe("MyGame-Win64-Shipping.exe");
        Path.GetFileName(entry.TargetDirectory).ShouldBe("Win64");
        entry.GraphicsApi.ShouldBe(GraphicsApi.Unknown);
        entry.IsManual.ShouldBeFalse();
    }

    [Fact]
    public async Task ManualProbeUsesSelectedExecutable()
    {
        var gameDirectory = _directory.CreateDirectory("manual-game");
        var exe = _directory.WriteFile("manual-game/ManualGame.exe", "fake exe");
        var probe = new GameProbe(NullLogger<GameProbe>.Instance);

        var entry = await probe.ProbeManualAsync(exe, TestContext.Current.CancellationToken);

        entry.ShouldNotBeNull();
        entry.IsManual.ShouldBeTrue();
        entry.Name.ShouldBe("ManualGame");
        entry.TargetDirectory.ShouldBe(gameDirectory);
        entry.Id.ShouldStartWith("manual:");
    }

    [Fact]
    public async Task ManualProbeRejectsNonExe()
    {
        var file = _directory.WriteFile("manual-game/readme.txt", "text");
        var probe = new GameProbe(NullLogger<GameProbe>.Instance);

        var entry = await probe.ProbeManualAsync(file, TestContext.Current.CancellationToken);

        entry.ShouldBeNull();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
