using FrameGenManagerNext.Infrastructure.Legacy;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class LegacyDeploymentReaderTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-legacy-tests");

    [Fact]
    public async Task FindsManifestByGameDirectory()
    {
        var game = _directory.CreateDirectory("game");
        var backups = _directory.CreateDirectory("backups");
        var hash = new string('a', 64);
        _directory.WriteFile(
            "backups/abc123/manifest.json",
            $$"""
            {
              "game_key": "abc123",
              "game_dir": {{System.Text.Json.JsonSerializer.Serialize(game)}},
              "deployed_at": "2026-01-01T00:00:00Z",
              "proxy_name": "version.dll",
              "installing": false,
              "files": [
                { "rel_path": "version.dll", "existed_before": false, "deployed_sha256": "{{hash}}" }
              ]
            }
            """);

        var reader = new LegacyDeploymentReader(
            NullLogger<LegacyDeploymentReader>.Instance,
            [backups]);

        var found = await reader.FindAsync(game, TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found.ProxyName.ShouldBe("version.dll");
        found.Files.Count.ShouldBe(1);
        found.Files[0].RelativePath.ShouldBe("version.dll");
        found.Files[0].DeployedSha256.ShouldBe(hash);
    }

    [Fact]
    public async Task IgnoresManifestForAnotherDirectory()
    {
        var game = _directory.CreateDirectory("game");
        var other = _directory.CreateDirectory("other");
        var backups = _directory.CreateDirectory("backups");
        _directory.WriteFile(
            "backups/abc123/manifest.json",
            $$"""
            {
              "game_dir": {{System.Text.Json.JsonSerializer.Serialize(other)}},
              "proxy_name": "version.dll",
              "files": []
            }
            """);

        var reader = new LegacyDeploymentReader(
            NullLogger<LegacyDeploymentReader>.Instance,
            [backups]);

        var found = await reader.FindAsync(game, TestContext.Current.CancellationToken);

        found.ShouldBeNull();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
