using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Infrastructure.Packs;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class PackJsonSerializerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FrameGenPackJsonTests",
        Guid.NewGuid().ToString("N"));

    public PackJsonSerializerTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void LoadsValidPack()
    {
        var path = WritePack(ValidJson());

        var result = PackJsonSerializer.LoadFromFile(path);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.Id.ShouldBe("framegen.sm86");
        result.Value.Trust.ShouldBe(AssetPackTrust.Bundled);
        result.Value.Files.Count.ShouldBe(1);
    }

    [Fact]
    public void MissingFileReturnsPackNotFound()
    {
        var result = PackJsonSerializer.LoadFromFile(Path.Combine(_directory, "missing.json"));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(ErrorCode.PackNotFound);
    }

    [Fact]
    public void InvalidJsonReturnsPackInvalid()
    {
        var path = WritePack("{ not json");

        var result = PackJsonSerializer.LoadFromFile(path);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(ErrorCode.PackInvalid);
    }

    [Fact]
    public void UnknownPropertyIsRejected()
    {
        var json = ValidJson().Replace(
            "\"trust\": \"bundled\"",
            "\"trust\": \"bundled\", \"surprise\": true",
            StringComparison.Ordinal);

        var path = WritePack(json);

        var result = PackJsonSerializer.LoadFromFile(path);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(ErrorCode.PackInvalid);
    }

    [Fact]
    public void SerializeRoundTrips()
    {
        var path = WritePack(ValidJson());
        var loaded = PackJsonSerializer.LoadFromFile(path);
        loaded.IsSuccess.ShouldBeTrue(loaded.Error?.ToString());

        var json = PackJsonSerializer.Serialize(loaded.Value);
        var roundTripPath = WritePack(json);
        var roundTrip = PackJsonSerializer.LoadFromFile(roundTripPath);

        roundTrip.IsSuccess.ShouldBeTrue(roundTrip.Error?.ToString());
        roundTrip.Value.Id.ShouldBe(loaded.Value.Id);
        roundTrip.Value.Files[0].Sha256.ShouldBe(loaded.Value.Files[0].Sha256);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WritePack(string json)
    {
        var path = Path.Combine(_directory, "pack.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string ValidJson()
    {
        var hash = new string('a', 64);
        return $$"""
            {
              "schemaVersion": 1,
              "id": "framegen.sm86",
              "version": "0.4.2",
              "displayName": "FrameGen SM86",
              "minAppVersion": "2.0.0",
              "gpuRoutes": ["Sm86"],
              "files": [
                {
                  "source": "version.dll",
                  "target": "version.dll",
                  "role": "proxy",
                  "proxySlot": true,
                  "sha256": "{{hash}}",
                  "size": 123
                }
              ],
              "steps": [],
              "trust": "bundled"
            }
            """;
    }
}
