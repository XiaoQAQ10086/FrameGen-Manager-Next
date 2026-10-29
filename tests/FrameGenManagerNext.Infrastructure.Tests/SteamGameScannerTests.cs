using FrameGenManagerNext.Infrastructure.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class SteamGameScannerTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-steam-tests");

    [Fact]
    public void ScansLibraryFoldersAndAppManifests()
    {
        var root = _directory.CreateDirectory("steam");
        var secondLibrary = _directory.CreateDirectory("library2");
        var app1 = _directory.CreateDirectory("steam/steamapps/common/TestGame");
        var app2 = _directory.CreateDirectory("library2/steamapps/common/SecondGame");

        _directory.WriteFile(
            "steam/steamapps/libraryfolders.vdf",
            $$"""
              "libraryfolders"
              {
                  "0" { "path" "{{Escape(root)}}" }
                  "1" { "path" "{{Escape(secondLibrary)}}" }
              }
              """);

        _directory.WriteFile(
            "steam/steamapps/appmanifest_123.acf",
            """
            "AppState"
            {
                "appid" "123"
                "name" "Test Game"
                "installdir" "TestGame"
            }
            """);

        _directory.WriteFile(
            "library2/steamapps/appmanifest_456.acf",
            """
            "AppState"
            {
                "appid" "456"
                "name" "Second Game"
                "installdir" "SecondGame"
            }
            """);

        var scanner = new SteamGameScanner(NullLogger<SteamGameScanner>.Instance);
        var games = scanner.ScanFromRoot(root);

        games.Count.ShouldBe(2);
        games.ShouldContain(game => game.AppId == "123" && game.InstallDirectory == app1);
        games.ShouldContain(game => game.AppId == "456" && game.InstallDirectory == app2);
    }

    [Fact]
    public void SkipsSteamRuntimePackages()
    {
        var root = _directory.CreateDirectory("steam");
        _directory.CreateDirectory("steam/steamapps/common/Steamworks Common Redistributables");
        _directory.WriteFile(
            "steam/steamapps/appmanifest_228980.acf",
            """
            "AppState"
            {
                "appid" "228980"
                "name" "Steamworks Common Redistributables"
                "installdir" "Steamworks Common Redistributables"
            }
            """);

        var scanner = new SteamGameScanner(NullLogger<SteamGameScanner>.Instance);
        var games = scanner.ScanFromRoot(root);

        games.ShouldBeEmpty();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static string Escape(string path)
    {
        return path.Replace("\\", "\\\\", StringComparison.Ordinal);
    }
}
