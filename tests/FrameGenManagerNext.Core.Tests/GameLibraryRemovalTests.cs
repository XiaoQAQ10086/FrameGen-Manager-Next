using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class GameLibraryRemovalTests
{
    [Fact]
    public async Task RemovingAndRestoringKeepsTheGame()
    {
        var manual = Game("manual:1", @"C:\Games\Manual", isManual: true);
        var scanned = Game("steam:1", @"C:\Games\Scanned", isManual: false);
        var store = new MemoryStore(new GameLibrary
        {
            Manual = [manual],
            Scanned = [scanned],
        });
        using var service = new GameLibraryService(
            [],
            new UnusedProbe(),
            store,
            NullLogger<GameLibraryService>.Instance);

        await service.RemoveAsync(manual.Id, TestContext.Current.CancellationToken);

        store.Library.Manual.ShouldContain(manual);
        store.Library.EnumerateVisibleGames().ShouldBe([scanned]);
        store.Library.EnumerateRemovedGames().ShouldBe([manual]);

        await service.SetIgnoredAsync(manual.Id, ignored: false, TestContext.Current.CancellationToken);

        store.Library.EnumerateVisibleGames().ShouldContain(manual);
        store.Library.EnumerateRemovedGames().ShouldBeEmpty();
    }

    [Fact]
    public async Task RemovingScannedGameHidesItUntilRestored()
    {
        var scanned = Game("steam:1", @"C:\Games\Scanned", isManual: false);
        var store = new MemoryStore(new GameLibrary { Scanned = [scanned] });
        using var service = new GameLibraryService(
            [],
            new UnusedProbe(),
            store,
            NullLogger<GameLibraryService>.Instance);

        await service.RemoveAsync(scanned.Id, TestContext.Current.CancellationToken);

        store.Library.EnumerateVisibleGames().ShouldBeEmpty();
        store.Library.EnumerateRemovedGames().ShouldBe([scanned]);

        await service.SetIgnoredAsync(scanned.Id, ignored: false, TestContext.Current.CancellationToken);

        store.Library.EnumerateVisibleGames().ShouldBe([scanned]);
        store.Library.EnumerateRemovedGames().ShouldBeEmpty();
    }

    private static GameEntry Game(string id, string directory, bool isManual)
    {
        return new GameEntry(
            id,
            id,
            isManual ? LauncherKind.Manual : LauncherKind.Steam,
            directory,
            Path.Combine(directory, "game.exe"),
            directory,
            GraphicsApi.Direct3D12,
            AppId: null,
            isManual);
    }

    private sealed class MemoryStore(GameLibrary library) : IGameLibraryStore
    {
        public GameLibrary Library { get; private set; } = library;

        public Task<GameLibrary> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Library);
        }

        public Task SaveAsync(GameLibrary library, CancellationToken cancellationToken)
        {
            Library = library;
            return Task.CompletedTask;
        }
    }

    private sealed class UnusedProbe : IGameProbe
    {
        public Task<GameEntry> ProbeAsync(GameCandidate candidate, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<GameEntry?> ProbeManualAsync(string exePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
