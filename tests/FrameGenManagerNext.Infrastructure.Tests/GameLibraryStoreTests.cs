using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Library;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class GameLibraryStoreTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-library-store-tests");

    [Fact]
    public async Task SaveAndLoadRoundTrips()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        var store = new GameLibraryStore(paths, NullLogger<GameLibraryStore>.Instance);
        var scanned = new GameEntry(
            "steam:1",
            "Test Game",
            LauncherKind.Steam,
            @"C:\Games\TestGame",
            @"C:\Games\TestGame\game.exe",
            @"C:\Games\TestGame",
            GraphicsApi.Direct3D12,
            "1",
            IsManual: false);
        var manual = new GameEntry(
            "manual:1",
            "Manual Game",
            LauncherKind.Manual,
            @"C:\Manual",
            @"C:\Manual\game.exe",
            @"C:\Manual",
            GraphicsApi.Direct3D11,
            AppId: null,
            IsManual: true);

        var library = new GameLibrary
        {
            LastScanUtc = DateTimeOffset.UtcNow,
            Scanned = [scanned],
            Manual = [manual],
            IgnoredIds = ["steam:2"],
        };

        await store.SaveAsync(library, TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        loaded.Scanned.Count.ShouldBe(1);
        loaded.Scanned[0].Name.ShouldBe("Test Game");
        loaded.Scanned[0].GraphicsApi.ShouldBe(GraphicsApi.Direct3D12);
        loaded.Manual.Count.ShouldBe(1);
        loaded.Manual[0].IsManual.ShouldBeTrue();
        loaded.IgnoredIds.ShouldContain("steam:2");

        var json = await File.ReadAllTextAsync(
            Path.Combine(paths.LibraryDirectory, "library.json"),
            TestContext.Current.CancellationToken);
        json.ShouldContain("\"launcher\": \"steam\"");
        json.ShouldContain("\"graphicsApi\": \"direct3D12\"");
    }

    [Fact]
    public async Task CorruptCacheIsQuarantined()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        Directory.CreateDirectory(paths.LibraryDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(paths.LibraryDirectory, "library.json"),
            "{ not json",
            TestContext.Current.CancellationToken);

        var store = new GameLibraryStore(paths, NullLogger<GameLibraryStore>.Instance);
        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        loaded.Scanned.ShouldBeEmpty();
        loaded.Manual.ShouldBeEmpty();
        File.Exists(Path.Combine(paths.LibraryDirectory, "library.json.bad")).ShouldBeTrue();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
