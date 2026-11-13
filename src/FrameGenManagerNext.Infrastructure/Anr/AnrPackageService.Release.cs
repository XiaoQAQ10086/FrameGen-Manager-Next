using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using FrameGenManagerNext.Core.Features.Anr;

namespace FrameGenManagerNext.Infrastructure.Anr;

public sealed partial class AnrPackageService
{
    private static ReleaseAsset? FindFullAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) ||
                !name.EndsWith("-full.zip", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("source", StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith("anr-fg-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = asset.TryGetProperty("browser_download_url", out var urlElement)
                ? urlElement.GetString()
                : null;
            var digest = asset.TryGetProperty("digest", out var digestElement)
                ? digestElement.GetString()
                : null;
            var size = asset.TryGetProperty("size", out var sizeElement) &&
                       sizeElement.TryGetInt64(out var parsedSize)
                ? parsedSize
                : 0;
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(digest) ||
                !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new ReleaseAsset(name, url, size, digest["sha256:".Length..]);
        }

        return null;
    }

    private static string? VersionFromAsset(string name)
    {
        var match = FullAssetRegex().Match(name);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ReadVersionFromZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.GetEntry("nr_fg_package.json");
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.TryGetProperty("version", out var version)
            ? version.GetString()
            : null;
    }

    private static AnrPackageKind? DetectKind(string payloadDirectory)
    {
        var game = Path.Combine(payloadDirectory, "game");
        if (File.Exists(Path.Combine(game, "dxgi.dll")) &&
            File.Exists(Path.Combine(game, "OptiScaler.ini")))
        {
            return AnrPackageKind.Full;
        }

        var addon = Path.Combine(payloadDirectory, "Apex Neural Rendering FG");
        if (File.Exists(Path.Combine(addon, "dlssg", "nvngx_dlssg.dll")))
        {
            return AnrPackageKind.Addon;
        }

        return null;
    }

    [GeneratedRegex(@"^anr-fg-(.+)-full\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex FullAssetRegex();

    private sealed record ReleaseAsset(string Name, string Url, long Size, string Sha256);

    private sealed record LatestRelease(ReleaseAsset Asset, string Version);
}
