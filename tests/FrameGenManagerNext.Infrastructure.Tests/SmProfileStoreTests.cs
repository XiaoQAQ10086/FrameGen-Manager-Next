using FrameGenManagerNext.Infrastructure.Paths;
using FrameGenManagerNext.Infrastructure.SmoothMotion;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class SmProfileStoreTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-sm-profile-tests");

    [Fact]
    public void SaveLoadAndDeleteRoundTrip()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        var store = new SmProfileStore(paths, NullLogger<SmProfileStore>.Instance);
        var target = _directory.CreateDirectory("game");
        var record = new SmProfileRecord(
            "Test Game",
            Path.Combine(target, "Game.exe"),
            "Test Game Profile",
            Ours: true,
            Had: true,
            HadValue: 0,
            target);

        store.Save(record);
        var loaded = store.Load(target);

        loaded.ShouldNotBeNull();
        loaded.Game.ShouldBe("Test Game");
        loaded.Profile.ShouldBe("Test Game Profile");
        loaded.HadValue.ShouldBe(0u);
        loaded.Ours.ShouldBeTrue();

        store.Delete(target);
        store.Load(target).ShouldBeNull();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
