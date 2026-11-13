using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Anr;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Anr;

public sealed partial class AnrPackageService : IAnrPackageService, IDisposable
{
    private const string LatestReleaseApi = "https://api.github.com/repos/apex-compute/ANR/releases/latest";
    private static readonly string[] ProxyPrefixes =
    [
        "https://ghfast.top/",
        "https://gh-proxy.com/",
        "https://ghproxy.net/",
    ];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppPaths _paths;
    private readonly ILogger<AnrPackageService> _logger;
    private readonly HttpClient _httpClient;

    public AnrPackageService(AppPaths paths, ILogger<AnrPackageService> logger)
    {
        _paths = paths;
        _logger = logger;
        _httpClient = CreateClient();
    }

    private string RootDirectory => Path.Combine(_paths.PacksDirectory, "anr-fg");

    public async Task<Result<AnrPackageInfo>> EnsureLatestAsync(
        IProgress<AnrDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var latest = await FetchLatestAsync(cancellationToken);
        if (latest.IsFailure)
        {
            var cached = await GetCachedAsync(cancellationToken);
            return cached is not null
                ? Result<AnrPackageInfo>.Ok(cached)
                : Result<AnrPackageInfo>.Fail(latest.Error!);
        }

        var asset = latest.Value.Asset;
        var version = latest.Value.Version;
        var existing = await GetCachedAsync(cancellationToken);
        if (existing is not null &&
            string.Equals(existing.Version, version, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AnrPackageInfo>.Ok(existing);
        }

        return await DownloadAndExtractAsync(asset, version, progress, cancellationToken);
    }

    public async Task<Result<AnrReleaseInfo>> CheckLatestAsync(CancellationToken cancellationToken)
    {
        var latest = await FetchLatestAsync(cancellationToken);
        return latest.IsSuccess
            ? Result<AnrReleaseInfo>.Ok(new AnrReleaseInfo(latest.Value.Version))
            : Result<AnrReleaseInfo>.Fail(latest.Error!);
    }

    private async Task<Result<LatestRelease>> FetchLatestAsync(CancellationToken cancellationToken)
    {
        var api = await FetchLatestFromApiAsync(cancellationToken);
        if (api.IsSuccess)
        {
            return api;
        }

        _logger.LogInformation(
            "GitHub API unavailable ({Reason}); falling back to the release redirect",
            api.Error?.Diagnostic);
        return await FetchLatestFromWebAsync(cancellationToken);
    }

    private async Task<Result<LatestRelease>> FetchLatestFromApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("FrameGen-Manager-Next", "1.0"));
            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ??
                        Environment.GetEnvironmentVariable("GH_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            using var apiTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            apiTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await _httpClient.SendAsync(request, apiTimeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Result<LatestRelease>.Fail(AppError.Create(
                    ErrorCode.NetworkError,
                    ("status", ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture))));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(apiTimeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: apiTimeout.Token);
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var asset = FindFullAsset(root);
            if (asset is null)
            {
                return Result<LatestRelease>.Fail(new AppError(
                    ErrorCode.DownloadFailed,
                    "the latest ANR release has no anr-fg-*-full.zip asset"));
            }

            var version = VersionFromAsset(asset.Name) ?? tag ?? "latest";
            return Result<LatestRelease>.Ok(new LatestRelease(asset, version));
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger.LogWarning(exception, "Could not query the latest ANR release");
            return Result<LatestRelease>.Fail(new AppError(ErrorCode.NetworkError, exception.Message));
        }
    }

    /// <summary>
    /// API-free path used when the unauthenticated GitHub API quota is exhausted:
    /// follow the releases/latest redirect for the tag, then read SHA256SUMS.txt
    /// from the same release. It needs only normal HTTPS requests.
    /// </summary>
    private async Task<Result<LatestRelease>> FetchLatestFromWebAsync(CancellationToken cancellationToken)
    {
        const string latestPage = "https://github.com/apex-compute/ANR/releases/latest";
        try
        {
            using var pageTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pageTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var pageRequest = new HttpRequestMessage(HttpMethod.Get, latestPage);
            pageRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("FrameGen-Manager-Next", "1.0"));
            using var page = await _httpClient.SendAsync(
                pageRequest,
                HttpCompletionOption.ResponseHeadersRead,
                pageTimeout.Token);
            if (!page.IsSuccessStatusCode)
            {
                return Result<LatestRelease>.Fail(AppError.Create(
                    ErrorCode.NetworkError,
                    ("status", ((int)page.StatusCode).ToString(CultureInfo.InvariantCulture))));
            }

            var finalUri = page.RequestMessage?.RequestUri;
            var tag = finalUri?.Segments.LastOrDefault()?.Trim('/');
            if (string.IsNullOrWhiteSpace(tag))
            {
                return Result<LatestRelease>.Fail(new AppError(
                    ErrorCode.NetworkError,
                    "could not resolve the latest ANR release tag"));
            }

            var version = tag.StartsWith("FG-", StringComparison.OrdinalIgnoreCase)
                ? tag[3..]
                : tag;
            var assetName = $"anr-fg-{version}-full.zip";
            var sumsUrl =
                $"https://github.com/apex-compute/ANR/releases/download/{tag}/SHA256SUMS.txt";
            using var sumsTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sumsTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            var sums = await _httpClient.GetStringAsync(sumsUrl, sumsTimeout.Token);
            var sha = FindChecksum(sums, assetName);
            if (sha is null)
            {
                return Result<LatestRelease>.Fail(new AppError(
                    ErrorCode.DownloadFailed,
                    "SHA256SUMS.txt does not list the ANR full package"));
            }

            var url =
                $"https://github.com/apex-compute/ANR/releases/download/{tag}/{assetName}";
            return Result<LatestRelease>.Ok(new LatestRelease(
                new ReleaseAsset(assetName, url, 0, sha),
                version));
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(exception, "Could not resolve the latest ANR release without the API");
            return Result<LatestRelease>.Fail(new AppError(ErrorCode.NetworkError, exception.Message));
        }
    }

    private static string? FindChecksum(string sums, string assetName)
    {
        foreach (var line in sums.Split('\n'))
        {
            var parts = line.Trim().Split(
                [' ', '\t'],
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var name = parts[^1].TrimStart('*');
            if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase) &&
                parts[0].Length == 64)
            {
                return parts[0];
            }
        }

        return null;
    }

    public async Task<Result<AnrPackageInfo>> ImportAsync(
        string zipPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return Result<AnrPackageInfo>.Fail(new AppError(ErrorCode.FileNotFound, "ANR zip not found"));
        }

        string version;
        try
        {
            version = ReadVersionFromZip(zipPath) ?? VersionFromAsset(Path.GetFileName(zipPath)) ?? "imported";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return Result<AnrPackageInfo>.Fail(new AppError(ErrorCode.PackInvalid, exception.Message));
        }

        return await ExtractAsync(zipPath, version, cancellationToken);
    }

    public Task<AnrPackageInfo?> GetCachedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(RootDirectory))
        {
            return Task.FromResult<AnrPackageInfo?>(null);
        }

        var currentPath = Path.Combine(RootDirectory, "current.json");
        if (File.Exists(currentPath))
        {
            try
            {
                var current = JsonSerializer.Deserialize<CurrentPackage>(
                    File.ReadAllText(currentPath),
                    JsonOptions);
                if (current is not null &&
                    Directory.Exists(current.PayloadDirectory))
                {
                    var kind = DetectKind(current.PayloadDirectory);
                    if (kind is not null)
                    {
                        return Task.FromResult<AnrPackageInfo?>(new AnrPackageInfo(
                            current.Version,
                            current.PayloadDirectory,
                            current.ZipPath,
                            kind.Value));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                _logger.LogDebug(exception, "Ignoring invalid ANR current.json");
            }
        }

        return Task.FromResult<AnrPackageInfo?>(null);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<Result<AnrPackageInfo>> DownloadAndExtractAsync(
        ReleaseAsset asset,
        string version,
        IProgress<AnrDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RootDirectory);
        var tempDirectory = Path.Combine(RootDirectory, "download");
        Directory.CreateDirectory(tempDirectory);
        var tempPath = Path.Combine(tempDirectory, asset.Name + ".part");
        var zipPath = Path.Combine(RootDirectory, asset.Name);

        // Mirrors first: direct GitHub release downloads are frequently reset in
        // mainland China. Every source is still verified against the official SHA.
        var sources = ProxyPrefixes.Select(prefix => prefix + asset.Url).ToList();
        sources.Add(asset.Url);
        string? lastError = null;

        foreach (var source in sources)
        {
            try
            {
                progress?.Report(new AnrDownloadProgress("download", 0, asset.Size, source));
                using var sourceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                sourceTimeout.CancelAfter(TimeSpan.FromSeconds(60));
                await DownloadAsync(source, tempPath, asset.Size, progress, sourceTimeout.Token);
                var hash = await HashAsync(tempPath, cancellationToken);
                if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    lastError = "SHA-256 mismatch";
                    File.Delete(tempPath);
                    continue;
                }

                File.Move(tempPath, zipPath, overwrite: true);
                return await ExtractAsync(zipPath, version, cancellationToken);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or IOException or TaskCanceledException)
            {
                lastError = exception.Message;
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        return Result<AnrPackageInfo>.Fail(new AppError(
            ErrorCode.DownloadFailed,
            lastError ?? "could not download ANR"));
    }

    private async Task DownloadAsync(
        string url,
        string path,
        long totalBytes,
        IProgress<AnrDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var effectiveTotal = totalBytes > 0
            ? totalBytes
            : response.Content.Headers.ContentLength ?? 0;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);
        var buffer = new byte[1024 * 1024];
        long received = 0;
        var started = Stopwatch.GetTimestamp();
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var speed = elapsed > 0 ? received / elapsed : 0;
            progress?.Report(new AnrDownloadProgress(
                "download",
                received,
                effectiveTotal > 0 ? effectiveTotal : received,
                url,
                speed));
        }
    }

    private async Task<Result<AnrPackageInfo>> ExtractAsync(
        string zipPath,
        string version,
        CancellationToken cancellationToken)
    {
        var safeVersion = new string(version.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_').ToArray());
        var versionDirectory = Path.Combine(RootDirectory, safeVersion);
        var payload = Path.Combine(versionDirectory, "payload");
        var temp = payload + ".extracting";
        try
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }

            Directory.CreateDirectory(temp);
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    var relative = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (relative.Length == 0 || relative.EndsWith('/'))
                    {
                        continue;
                    }

                    if (relative.Split('/').Any(segment => segment == "..") ||
                        Path.IsPathRooted(relative))
                    {
                        return Result<AnrPackageInfo>.Fail(new AppError(
                            ErrorCode.UnsafePath,
                            $"unsafe zip entry: {entry.FullName}"));
                    }

                    var destination = Path.Combine(temp, relative.Replace('/', Path.DirectorySeparatorChar));
                    var parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    entry.ExtractToFile(destination, overwrite: true);
                }
            }

            var kind = DetectKind(temp);
            if (kind is null)
            {
                return Result<AnrPackageInfo>.Fail(new AppError(
                    ErrorCode.PackInvalid,
                    "ANR zip is neither the full package (game/dxgi.dll) nor the addon package (Apex Neural Rendering FG)"));
            }

            if (Directory.Exists(payload))
            {
                Directory.Delete(payload, recursive: true);
            }

            Directory.Move(temp, payload);
            var info = new AnrPackageInfo(version, payload, zipPath, kind.Value);
            await WriteCurrentAsync(info, cancellationToken);
            return Result<AnrPackageInfo>.Ok(info);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Result<AnrPackageInfo>.Fail(new AppError(ErrorCode.PackInvalid, exception.Message));
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                try
                {
                    Directory.Delete(temp, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup.
                }
            }
        }
    }

    private async Task WriteCurrentAsync(AnrPackageInfo info, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RootDirectory);
        var path = Path.Combine(RootDirectory, "current.json");
        var current = new CurrentPackage(info.Version, info.PayloadDirectory, info.ZipPath);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(current, JsonOptions),
            cancellationToken);
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FrameGen-Manager-Next", "1.0"));
        return client;
    }
    private sealed record CurrentPackage(string Version, string PayloadDirectory, string ZipPath);
}
