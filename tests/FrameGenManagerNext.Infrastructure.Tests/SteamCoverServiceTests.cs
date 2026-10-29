using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Library;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class SteamCoverServiceTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-cover-tests");

    [Fact]
    public async Task FindsGridCoverFromLinkedSteamLibrary()
    {
        var root = _directory.CreateDirectory("Steam");
        var install = Path.Combine(root, "steamapps", "common", "Game");
        Directory.CreateDirectory(install);
        var cover = Path.Combine(root, "userdata", "1", "config", "grid", "730_library_600x900.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(cover)!);
        File.WriteAllBytes(cover, new byte[2048]);

        var paths = new AppPaths(_directory.GetPath("data"));
        var service = new SteamCoverService(paths, NullLogger<SteamCoverService>.Instance);
        var game = new GameEntry(
            "steam:730",
            "Counter-Strike 2",
            LauncherKind.Steam,
            install,
            null,
            install,
            GraphicsApi.Direct3D12,
            "730",
            IsManual: false);

        var found = await service.GetCoverPathAsync(game, TestContext.Current.CancellationToken);

        found.ShouldBe(cover);
    }

    [Fact]
    public async Task NonSteamGameHasNoCover()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        var service = new SteamCoverService(paths, NullLogger<SteamCoverService>.Instance);
        var game = new GameEntry(
            "epic:1",
            "Game",
            LauncherKind.Epic,
            _directory.GetPath("Game"),
            null,
            _directory.GetPath("Game"),
            GraphicsApi.Direct3D12,
            "abc",
            IsManual: false);

        var found = await service.GetCoverPathAsync(game, TestContext.Current.CancellationToken);

        found.ShouldBeNull();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
