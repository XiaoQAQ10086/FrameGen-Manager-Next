using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using FrameGenManagerNext.Domain;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/XiaoQAQ10086/FrameGen-Manager-Next/releases/latest";
    private const string CloudDownloadUrl = "https://pan.quark.cn/s/adea7ed4f2b2";

    private static readonly HttpClient UpdateHttpClient = CreateUpdateClient();

    private CancellationTokenSource? _updateCancellation;

    private async Task<object?> CheckForUpdatesAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("FrameGen-Manager-Next");
            using var response = await UpdateHttpClient.SendAsync(request, timeout.Token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new { available = false };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new { available = false, error = $"Update feed returned {(int)response.StatusCode}." };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            var root = document.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
            var latest = NormalizeVersion(tag);
            if (!TryParseVersion(latest, out var latestVersion) ||
                !TryParseVersion(AppInfo.Version, out var currentVersion))
            {
                return new { available = false, error = "Could not parse version." };
            }

            var asset = SelectAsset(root);
            var notes = root.TryGetProperty("body", out var bodyValue) ? bodyValue.GetString() : null;
            var htmlUrl = root.TryGetProperty("html_url", out var htmlValue) ? htmlValue.GetString() : null;

            return new
            {
                available = latestVersion > currentVersion,
                currentVersion = AppInfo.Version,
                latestVersion = latest,
                notes = notes ?? string.Empty,
                htmlUrl,
                cloudUrl = CloudDownloadUrl,
                assetName = asset?.Name,
                assetSize = asset?.Size,
                downloadUrl = asset?.DownloadUrl,
            };
        }
        catch (OperationCanceledException)
        {
            return new { available = false, error = "Update check timed out." };
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            _logger.LogDebug(exception, "Update check failed");
            return new { available = false, error = exception.Message };
        }
    }

    private async Task<object?> DownloadUpdateAsync(JsonElement payload)
    {
        var url = payload.TryGetProperty("url", out var urlValue) ? urlValue.GetString() : null;
        var fileName = payload.TryGetProperty("fileName", out var nameValue) ? nameValue.GetString() : null;
        var version = payload.TryGetProperty("version", out var versionValue) ? versionValue.GetString() : null;

        if (!TryValidateDownloadUrl(url) || string.IsNullOrWhiteSpace(fileName))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InvalidArgument, "invalid update download URL");
        }

        if (_updateCancellation is not null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileLocked, "another update is already downloading");
        }

        var safeVersion = SafeSegment(version ?? "latest");
        var safeName = Path.GetFileName(fileName);
        var directory = Path.Combine(Path.GetTempPath(), "FrameGen-Manager-Next", "updates", safeVersion);
        var destination = Path.Combine(directory, safeName);
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            Directory.CreateDirectory(directory);
            using var response = await UpdateHttpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellation.Token);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? 0;
            await using var input = await response.Content.ReadAsStreamAsync(cancellation.Token);
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 128 * 1024,
                useAsync: true);

            var buffer = new byte[128 * 1024];
            long received = 0;
            var speedWindow = Stopwatch.StartNew();
            long speedWindowBytes = 0;
            var lastReport = Stopwatch.StartNew();

            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellation.Token);
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellation.Token);
                received += read;
                speedWindowBytes += read;

                if (lastReport.ElapsedMilliseconds >= 180)
                {
                    var seconds = Math.Max(0.001, speedWindow.Elapsed.TotalSeconds);
                    SendEvent("update.progress", new
                    {
                        received,
                        total,
                        percent = total > 0 ? Math.Min(100, received * 100.0 / total) : 0,
                        speed = speedWindowBytes / seconds,
                    });
                    speedWindow.Restart();
                    speedWindowBytes = 0;
                    lastReport.Restart();
                }
            }

            SendEvent("update.progress", new
            {
                received,
                total = total > 0 ? total : received,
                percent = 100,
                speed = 0,
            });

            _logger.LogInformation(
                "Update downloaded: version={Version} file={File} bytes={Bytes}",
                version,
                destination,
                received);

            return new
            {
                downloaded = true,
                path = destination,
                fileName = safeName,
                version,
            };
        }
        catch (OperationCanceledException)
        {
            DeleteDirectoryBestEffort(directory);
            _logger.LogInformation("Update download cancelled and temporary files removed");
            return new { cancelled = true };
        }
        catch
        {
            DeleteDirectoryBestEffort(directory);
            throw;
        }
        finally
        {
            if (ReferenceEquals(_updateCancellation, cancellation))
            {
                _updateCancellation = null;
            }
        }
    }

    private object? CancelUpdate()
    {
        _updateCancellation?.Cancel();
        return new { cancelled = true };
    }

    private static object? OpenUpdatePage(JsonElement payload)
    {
        var requested = payload.TryGetProperty("url", out var value) ? value.GetString() : null;
        var url = TryValidateDownloadUrl(requested) ? requested : CloudDownloadUrl;
        Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
        return new { opened = true };
    }

    private static UpdateAsset? SelectAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var candidates = new List<UpdateAsset>();
        foreach (var item in assets.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
            var url = item.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
            var size = item.TryGetProperty("size", out var sizeValue) ? sizeValue.GetInt64() : 0;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var lower = name.ToLowerInvariant();
            if (!lower.EndsWith(".zip", StringComparison.Ordinal) &&
                !lower.EndsWith(".exe", StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add(new UpdateAsset(name, url, size));
        }

        return candidates.FirstOrDefault(asset =>
                   asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase)) ??
               candidates.FirstOrDefault();
    }

    private static bool TryValidateDownloadUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("pan.quark.cn", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string? value)
    {
        return (value ?? string.Empty).Trim().TrimStart('v', 'V');
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var normalized = value.Split('-', '+')[0];
        return Version.TryParse(normalized, out version!);
    }

    private static string SafeSegment(string value)
    {
        var safe = new string(value
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_')
            .ToArray())
            .Trim('.');
        return string.IsNullOrWhiteSpace(safe) ? "latest" : safe;
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
            // A locked file will be cleaned on the next update attempt.
        }
    }

    private static HttpClient CreateUpdateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FrameGen-Manager-Next");
        return client;
    }

    private sealed record UpdateAsset(string Name, string DownloadUrl, long Size);
}
