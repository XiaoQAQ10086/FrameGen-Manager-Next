using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Library;

public sealed class SteamCoverService : IGameCoverService
{
    private static readonly string[] CoverSuffixes =
    [
        "_library_600x900_2x.jpg",
        "_library_600x900_2x.png",
        "_library_600x900.jpg",
        "_library_600x900.png",
        "_header.jpg",
        "_library_hero.jpg",
    ];

    private static readonly string[] CdnHosts =
    [
        "https://cdn.cloudflare.steamstatic.com",
        "https://cdn.akamai.steamstatic.com",
        "https://steamcdn-a.akamaihd.net",
    ];

    private static readonly string[] CdnPaths =
    [
        "/steam/apps/{0}/library_600x900_2x.jpg",
        "/steam/apps/{0}/library_600x900.jpg",
        "/steam/apps/{0}/header.jpg",
    ];

    private static readonly HttpClient Client = CreateClient();

    private readonly AppPaths _paths;
    private readonly ILogger<SteamCoverService> _logger;

    public SteamCoverService(AppPaths paths, ILogger<SteamCoverService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<string?> GetCoverPathAsync(GameEntry game, CancellationToken cancellationToken, bool allowDownload = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(game.AppId) ||
            game.Launcher != LauncherKind.Steam)
        {
            return null;
        }

        var appId = game.AppId.Trim();
        var coverDirectory = Path.Combine(_paths.CacheDirectory, "covers");
        var highResolution = Path.Combine(coverDirectory, appId + "_2x.jpg");
        if (IsUsableImage(highResolution))
        {
            return highResolution;
        }

        var steamRoot = FindSteamRoot(game.InstallDirectory);
        if (steamRoot is not null)
        {
            var local = FindLocalCover(steamRoot, appId);
            if (local is not null)
            {
                return local;
            }
        }

        if (!allowDownload || !appId.All(char.IsAsciiDigit))
        {
            // Large libraries skip network downloads while building state: a missing
            // remote cover must not block the first paint for hundreds of games.
            return null;
        }

        var downloaded = await DownloadCoverAsync(appId, highResolution, cancellationToken);
        if (downloaded is not null)
        {
            return downloaded;
        }

        var cached = Path.Combine(coverDirectory, appId + ".jpg");
        if (IsUsableImage(cached))
        {
            return cached;
        }

        return steamRoot is null ? null : FindLocalCover(steamRoot, appId);
    }

    private async Task<string?> DownloadCoverAsync(
        string appId,
        string destination,
        CancellationToken cancellationToken)
    {
        foreach (var pathTemplate in CdnPaths)
        {
            var path = string.Format(System.Globalization.CultureInfo.InvariantCulture, pathTemplate, appId);
            foreach (var host in CdnHosts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var response = await Client.GetAsync(host + path, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    if (!LooksLikeJpeg(bytes))
                    {
                        continue;
                    }

                    var directory = Path.GetDirectoryName(destination)!;
                    Directory.CreateDirectory(directory);
                    var temp = destination + ".part";
                    await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
                    File.Move(temp, destination, overwrite: true);
                    return destination;
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or TaskCanceledException or IOException)
                {
                    _logger.LogDebug(exception, "Could not download Steam cover {AppId} from {Host}", appId, host);
                }
            }
        }

        return null;
    }

    private static string? FindLocalCover(string steamRoot, string appId)
    {
        foreach (var suffix in CoverSuffixes)
        {
            var fileName = appId + suffix;
            var grid = FindNewestFile(
                Path.Combine(steamRoot, "userdata"),
                Path.Combine("config", "grid"),
                fileName);
            if (grid is not null)
            {
                return grid;
            }

            var cache = Path.Combine(steamRoot, "appcache", "librarycache", fileName);
            if (File.Exists(cache))
            {
                return cache;
            }
        }

        return null;
    }

    private static string? FindSteamRoot(string installDirectory)
    {
        try
        {
            var directory = new DirectoryInfo(installDirectory);
            for (var depth = 0; depth < 6 && directory is not null; depth++)
            {
                if (directory.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                {
                    return directory.Parent?.FullName;
                }

                directory = directory.Parent;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    private static string? FindNewestFile(string userdata, string relativeDirectory, string fileName)
    {
        try
        {
            if (!Directory.Exists(userdata))
            {
                return null;
            }

            return Directory.EnumerateDirectories(userdata)
                .Select(user => Path.Combine(user, relativeDirectory, fileName))
                .Where(File.Exists)
                .OrderByDescending(path => new FileInfo(path).LastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsUsableImage(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length > 1024;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool LooksLikeJpeg(byte[] bytes)
    {
        return bytes.Length is > 1024 and < 8 * 1024 * 1024 &&
               bytes[0] == 0xFF &&
               bytes[1] == 0xD8;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FrameGenManagerNext/2.0");
        return client;
    }
}
