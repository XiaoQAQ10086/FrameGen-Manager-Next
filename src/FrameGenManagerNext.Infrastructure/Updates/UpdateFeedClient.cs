using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Updates;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Updates;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Updates;

public sealed class UpdateFeedClient : IUpdateFeed
{
    private static readonly string[] DefaultManifestUrls =
    [
        "https://raw.githubusercontent.com/XiaoQAQ10086/FrameGen-Manager-Next/main/docs/update.json",
        "https://ghfast.top/https://raw.githubusercontent.com/XiaoQAQ10086/FrameGen-Manager-Next/main/docs/update.json",
        "https://gh-proxy.com/https://raw.githubusercontent.com/XiaoQAQ10086/FrameGen-Manager-Next/main/docs/update.json",
        "https://ghproxy.net/https://raw.githubusercontent.com/XiaoQAQ10086/FrameGen-Manager-Next/main/docs/update.json",
    ];

    private static readonly string[] ProxyPrefixes =
    [
        "https://ghfast.top/",
        "https://gh-proxy.com/",
        "https://ghproxy.net/",
    ];

    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
        "raw.githubusercontent.com",
        "ghfast.top",
        "gh-proxy.com",
        "ghproxy.net",
        "gitee.com",
    };

    private readonly ILogger<UpdateFeedClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _manifestUrls;
    private readonly string _stagingDirectory;
    private readonly bool _allowLoopbackHttp;

    public UpdateFeedClient(
        ILogger<UpdateFeedClient> logger,
        HttpClient? httpClient = null,
        IReadOnlyList<string>? manifestUrls = null,
        string? stagingDirectory = null,
        bool allowLoopbackHttp = false)
    {
        _logger = logger;
        _httpClient = httpClient ?? CreateClient();
        _manifestUrls = manifestUrls is { Count: > 0 }
            ? manifestUrls
            : ResolveManifestUrls();
        _stagingDirectory = stagingDirectory ?? AppPaths.UpdateStagingDirectory;
        _allowLoopbackHttp = allowLoopbackHttp;
    }

    public async Task<Result<UpdateManifest>> CheckAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var url in _manifestUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                using var response = await _httpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"{HostOf(url)}: HTTP {(int)response.StatusCode}");
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var document = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: timeout.Token);
                var manifest = ParseManifest(document.RootElement);
                if (manifest.IsFailure)
                {
                    errors.Add($"{HostOf(url)}: {manifest.Error!.Diagnostic}");
                    continue;
                }

                _logger.LogInformation(
                    "Update manifest loaded from {Source} version={Version}",
                    HostOf(url),
                    manifest.Value.Version);
                return manifest;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or JsonException or IOException or OperationCanceledException)
            {
                errors.Add($"{HostOf(url)}: {exception.Message}");
                _logger.LogDebug(exception, "Update manifest source failed: {Url}", url);
            }
        }

        return Result<UpdateManifest>.Fail(new AppError(
            ErrorCode.NetworkError,
            $"update manifest unavailable ({string.Join("; ", errors.Take(4))})"));
    }

    public async Task<Result<string>> DownloadInstallerAsync(
        UpdateManifest manifest,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var directory = Path.Combine(
            _stagingDirectory,
            SafeSegment(manifest.Version));
        var destination = Path.Combine(directory, manifest.InstallerName);

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<string>.Fail(new AppError(ErrorCode.AccessDenied, exception.Message));
        }

        var errors = new List<string>();
        foreach (var url in ExpandSources(manifest.InstallerUrls))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await TryDownloadAsync(
                manifest,
                url,
                destination,
                progress,
                cancellationToken);
            if (result)
            {
                _logger.LogInformation(
                    "Update installer downloaded source={Source} version={Version} bytes={Bytes}",
                    HostOf(url),
                    manifest.Version,
                    manifest.InstallerSize);
                return Result<string>.Ok(destination);
            }

            errors.Add(HostOf(url));
        }

        DeleteDirectoryBestEffort(directory);
        return Result<string>.Fail(new AppError(
            ErrorCode.DownloadFailed,
            $"all update sources failed ({string.Join(", ", errors)})"));
    }

    private async Task<bool> TryDownloadAsync(
        UpdateManifest manifest,
        string url,
        string destination,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var partial = destination + ".part";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            using var response = await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Update source {Source} returned HTTP {Status}",
                    HostOf(url),
                    (int)response.StatusCode);
                return false;
            }

            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long received = 0;
            var speedWindow = System.Diagnostics.Stopwatch.StartNew();
            long speedWindowBytes = 0;
            var lastReport = System.Diagnostics.Stopwatch.StartNew();

            await using (var output = new FileStream(
                partial,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 128 * 1024,
                useAsync: true))
            {
                while (true)
                {
                    var read = await input.ReadAsync(buffer, timeout.Token);
                    if (read == 0)
                    {
                        break;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    hash.AppendData(buffer, 0, read);
                    received += read;
                    speedWindowBytes += read;

                    if (lastReport.ElapsedMilliseconds >= 180)
                    {
                        var seconds = Math.Max(0.001, speedWindow.Elapsed.TotalSeconds);
                        progress?.Report(new UpdateDownloadProgress(
                            received,
                            manifest.InstallerSize,
                            speedWindowBytes / seconds,
                            HostOf(url)));
                        speedWindow.Restart();
                        speedWindowBytes = 0;
                        lastReport.Restart();
                    }
                }

                await output.FlushAsync(timeout.Token);
            }

            var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (received != manifest.InstallerSize ||
                !string.Equals(actualHash, manifest.InstallerSha256, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Update source {Source} failed verification: size={Size} expected={ExpectedSize} sha256={Hash} expectedHash={ExpectedHash}",
                    HostOf(url),
                    received,
                    manifest.InstallerSize,
                    actualHash,
                    manifest.InstallerSha256);
                TryDelete(partial);
                return false;
            }

            File.Move(partial, destination, overwrite: true);
            progress?.Report(new UpdateDownloadProgress(
                received,
                manifest.InstallerSize,
                0,
                HostOf(url)));
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryDelete(partial);
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _logger.LogDebug(exception, "Update download source failed: {Source}", HostOf(url));
            TryDelete(partial);
            return false;
        }
    }

    private Result<UpdateManifest> ParseManifest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("version", out var versionValue) ||
            !root.TryGetProperty("installer", out var installer) ||
            installer.ValueKind != JsonValueKind.Object)
        {
            return Fail("manifest is missing version/installer");
        }

        var version = versionValue.GetString();
        var name = StringProperty(installer, "name");
        var size = LongProperty(installer, "size");
        var sha = StringProperty(installer, "sha256");
        if (string.IsNullOrWhiteSpace(version) ||
            string.IsNullOrWhiteSpace(name) ||
            Path.GetFileName(name) != name ||
            !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            size is null or <= 0 ||
            string.IsNullOrWhiteSpace(sha) ||
            sha.Length != 64 ||
            !sha.All(Uri.IsHexDigit))
        {
            return Fail("manifest installer metadata is invalid");
        }

        if (!installer.TryGetProperty("urls", out var urlsValue) ||
            urlsValue.ValueKind != JsonValueKind.Array)
        {
            return Fail("manifest installer urls are missing");
        }

        var urls = new List<string>();
        foreach (var item in urlsValue.EnumerateArray())
        {
            var url = item.GetString();
            if (!IsAllowedUrl(url))
            {
                return Fail($"manifest installer url is not allowed: {url}");
            }

            urls.Add(url!);
        }

        if (urls.Count == 0)
        {
            return Fail("manifest installer urls are empty");
        }

        var notes = StringProperty(root, "notes");
        var pageUrl = StringProperty(root, "pageUrl");
        if (pageUrl is not null && !IsAllowedUrl(pageUrl))
        {
            pageUrl = null;
        }

        return Result<UpdateManifest>.Ok(new UpdateManifest(
            version.Trim(),
            name,
            size.Value,
            sha.ToLowerInvariant(),
            urls,
            notes,
            pageUrl));
    }

    private static IEnumerable<string> ExpandSources(IReadOnlyList<string> urls)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in urls)
        {
            if (!seen.Add(url))
            {
                continue;
            }

            var isGitHub = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                           uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);
            if (isGitHub)
            {
                // Mainland users usually reach the proxies faster than GitHub.
                // The SHA-256 from the manifest is still checked for every source.
                foreach (var prefix in ProxyPrefixes)
                {
                    var proxied = prefix + url;
                    if (seen.Add(proxied))
                    {
                        yield return proxied;
                    }
                }
            }

            yield return url;
        }
    }

    private bool IsAllowedUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (_allowLoopbackHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)
        {
            return true;
        }

        return uri.Scheme == Uri.UriSchemeHttps && AllowedHosts.Contains(uri.Host);
    }

    private static string? StringProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? LongProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static Result<UpdateManifest> Fail(string diagnostic)
    {
        return Result<UpdateManifest>.Fail(new AppError(ErrorCode.NetworkError, diagnostic));
    }

    private static string HostOf(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "unknown";
    }

    private static string SafeSegment(string value)
    {
        var safe = new string(value
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_')
            .ToArray())
            .Trim('.');
        return string.IsNullOrWhiteSpace(safe) ? "latest" : safe;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The next update attempt clears the staging directory.
        }
    }

    private static void DeleteDirectoryBestEffort(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A locked installer will be replaced on the next attempt.
        }
    }

    private static IReadOnlyList<string> ResolveManifestUrls()
    {
        // Local QA can point the updater at a temporary manifest without changing
        // the production feed. Multiple URLs are separated by ';'.
        var overrideValue = Environment.GetEnvironmentVariable("FGM_UPDATE_MANIFEST_URLS");
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            var urls = overrideValue
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .ToList();
            if (urls.Count > 0)
            {
                return urls;
            }
        }

        return DefaultManifestUrls;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FrameGen-Manager-Next");
        return client;
    }
}
