using System.Text.Json;
using FrameGenManagerNext.Infrastructure.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class EpicGameScannerTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-epic-tests");

    [Fact]
    public void ScansEpicManifests()
    {
        var install = _directory.CreateDirectory("epic-game");
        var manifests = _directory.CreateDirectory("manifests");

        var manifest = new
        {
            DisplayName = "Epic Game",
            InstallLocation = install,
            AppName = "EpicGame",
        };

        _directory.WriteFile(
            "manifests/EpicGame.item",
            JsonSerializer.Serialize(manifest));

        var scanner = new EpicGameScanner(NullLogger<EpicGameScanner>.Instance);
        var games = scanner.ScanFromDirectory(manifests);

        games.Count.ShouldBe(1);
        games[0].Name.ShouldBe("Epic Game");
        games[0].InstallDirectory.ShouldBe(install);
        games[0].AppId.ShouldBe("EpicGame");
    }

    [Fact]
    public void SkipsMissingInstallAndUnrealEngine()
    {
        var manifests = _directory.CreateDirectory("manifests");

        _directory.WriteFile(
            "manifests/missing.item",
            JsonSerializer.Serialize(new
            {
                DisplayName = "Missing Game",
                InstallLocation = _directory.GetPath("does-not-exist"),
                AppName = "Missing",
            }));

        _directory.WriteFile(
            "manifests/unreal.item",
            JsonSerializer.Serialize(new
            {
                DisplayName = "Unreal Engine",
                InstallLocation = _directory.CreateDirectory("unreal"),
                AppName = "UnrealEngine",
            }));

        var scanner = new EpicGameScanner(NullLogger<EpicGameScanner>.Instance);
        scanner.ScanFromDirectory(manifests).ShouldBeEmpty();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
