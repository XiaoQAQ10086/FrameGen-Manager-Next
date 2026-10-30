using FrameGenManagerNext.Domain.Library;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class GameLibraryTests
{
    [Fact]
    public void RemovedGamesAreHiddenButEnumerable()
    {
        var manual = Game("manual:1", @"C:\Games\Manual", isManual: true);
        var scanned = Game("steam:1", @"C:\Games\Scanned", isManual: false);
        var library = new GameLibrary
        {
            Manual = [manual],
            Scanned = [scanned],
            IgnoredIds = ["manual:1"],
        };

        library.EnumerateVisibleGames().ShouldBe([scanned]);
        library.EnumerateRemovedGames().ShouldBe([manual]);
    }

    [Fact]
    public void RemovedGamesAreDeduplicatedById()
    {
        var scanned = Game("steam:1", @"C:\Games\Scanned", isManual: false);
        var duplicate = scanned with { Name = "Duplicate" };
        var library = new GameLibrary
        {
            Manual = [duplicate],
            Scanned = [scanned],
            IgnoredIds = ["steam:1"],
        };

        library.EnumerateRemovedGames().ShouldBe([duplicate]);
    }

    [Fact]
    public void TargetOverrideChangesTheRenderExecutableAndDirectory()
    {
        var scanned = Game("steam:1", @"C:\Games\Scanned", isManual: false);
        var library = new GameLibrary
        {
            Scanned = [scanned],
            TargetOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["steam:1"] = @"C:\Games\Scanned\Binaries\Win64\Actual.exe",
            },
        };

        var effective = library.EnumerateVisibleGames().Single();

        effective.RenderExePath.ShouldBe(@"C:\Games\Scanned\Binaries\Win64\Actual.exe");
        effective.TargetDirectory.ShouldBe(@"C:\Games\Scanned\Binaries\Win64");
        effective.Name.ShouldBe(scanned.Name);
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
}
