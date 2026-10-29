using FrameGenManagerNext.Domain.Library;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class RenderExeLocatorTests
{
    private const long TenMegabytes = 10 * 1024 * 1024;

    [Fact]
    public void PicksUnrealShippingExecutable()
    {
        var candidates = new[]
        {
            new GameExeCandidate("C:\\Game\\Binaries\\Win64\\MyGame-Win64-Shipping.exe", "Binaries/Win64", TenMegabytes),
            new GameExeCandidate("C:\\Game\\MyGame.exe", "", TenMegabytes),
            new GameExeCandidate("C:\\Game\\Launcher.exe", "", TenMegabytes),
        };

        var result = RenderExeLocator.FindBest(candidates, "MyGame");

        result.ShouldNotBeNull();
        result.FileName.ShouldBe("MyGame-Win64-Shipping.exe");
    }

    [Fact]
    public void IgnoresCrashAndLauncherHelpers()
    {
        var candidates = new[]
        {
            new GameExeCandidate("C:\\Game\\UnityCrashHandler64.exe", "", TenMegabytes),
            new GameExeCandidate("C:\\Game\\Launcher.exe", "", TenMegabytes),
        };

        RenderExeLocator.FindBest(candidates, "Game").ShouldBeNull();
    }

    [Fact]
    public void PrefersExecutableMatchingInstallDirectory()
    {
        var candidates = new[]
        {
            new GameExeCandidate("C:\\Game\\Other.exe", "", TenMegabytes),
            new GameExeCandidate("C:\\Game\\MyGame.exe", "", TenMegabytes),
        };

        var result = RenderExeLocator.FindBest(candidates, "MyGame");

        result.ShouldNotBeNull();
        result.FileName.ShouldBe("MyGame.exe");
    }

    [Fact]
    public void LargerFileWinsOnTie()
    {
        var candidates = new[]
        {
            new GameExeCandidate("C:\\Game\\A.exe", "", TenMegabytes),
            new GameExeCandidate("C:\\Game\\B.exe", "", TenMegabytes * 4),
        };

        var result = RenderExeLocator.FindBest(candidates, "Game");

        result.ShouldNotBeNull();
        result.FileName.ShouldBe("B.exe");
    }

    [Fact]
    public void EmptyListReturnsNull()
    {
        RenderExeLocator.FindBest([], "Game").ShouldBeNull();
    }
}
