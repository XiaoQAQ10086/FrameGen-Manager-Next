using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Updates;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Updates;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private const string CloudDownloadUrl = "https://pan.quark.cn/s/adea7ed4f2b2";
    private const string BaiduCloudUrl = "https://pan.baidu.com/s/1DDP4W7Lsqbjt5WMKY-m4jA?pwd=1234";

    private CancellationTokenSource? _updateCancellation;
    private UpdateManifest? _pendingUpdate;

    private async Task<object?> CheckForUpdatesAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try
        {
            var result = await _updateFeed.CheckAsync(timeout.Token);
            if (result.IsFailure)
            {
                return new { available = false, error = result.Error!.Diagnostic ?? "update feed unavailable" };
            }

            var manifest = result.Value;
            var latest = NormalizeVersion(manifest.Version);
            if (!TryParseVersion(latest, out var latestVersion) ||
                !TryParseVersion(AppInfo.Version, out var currentVersion))
            {
                return new { available = false, error = "Could not parse version." };
            }

            var available = latestVersion > currentVersion;
            _pendingUpdate = available ? manifest : null;
            return new
            {
                available,
                currentVersion = AppInfo.Version,
                latestVersion = latest,
                notes = manifest.Notes ?? string.Empty,
                htmlUrl = manifest.PageUrl ?? CloudDownloadUrl,
                cloudUrl = CloudDownloadUrl,
                baiduUrl = BaiduCloudUrl,
                assetName = manifest.InstallerName,
                assetSize = manifest.InstallerSize,
                sha256 = manifest.InstallerSha256,
                sources = manifest.InstallerUrls.Count,
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
        var manifest = _pendingUpdate;
        if (manifest is null)
        {
            throw new BridgeException(
                Domain.Errors.ErrorCode.InvalidArgument,
                "no update is pending; check for updates first");
        }

        var requestedVersion = payload.TryGetProperty("version", out var versionValue)
            ? versionValue.GetString()
            : null;
        if (!string.IsNullOrWhiteSpace(requestedVersion) &&
            !string.Equals(
                NormalizeVersion(requestedVersion),
                NormalizeVersion(manifest.Version),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BridgeException(
                Domain.Errors.ErrorCode.InvalidArgument,
                "update version does not match the checked manifest");
        }

        if (_updateCancellation is not null)
        {
            throw new BridgeException(
                Domain.Errors.ErrorCode.FileLocked,
                "another update is already downloading");
        }

        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        try
        {
            var progress = new Progress<UpdateDownloadProgress>(update =>
            {
                var total = update.Total > 0 ? update.Total : update.Received;
                var percent = total > 0 ? Math.Min(100, update.Received * 100.0 / total) : 0;
                SendEvent("update.progress", new
                {
                    received = update.Received,
                    total,
                    percent,
                    speed = update.BytesPerSecond,
                    source = update.Source,
                });
            });

            var result = await _updateFeed.DownloadInstallerAsync(
                manifest,
                progress,
                cancellation.Token);
            if (result.IsFailure)
            {
                throw new BridgeException(
                    result.Error!.Code,
                    result.Error.Diagnostic ?? "update download failed");
            }

            _logger.LogInformation(
                "Update downloaded: version={Version} file={File}",
                manifest.Version,
                result.Value);
            return new
            {
                downloaded = true,
                installer = !_environment.IsPortable && IsInstallerAsset(manifest.InstallerName),
                path = result.Value,
                fileName = manifest.InstallerName,
                version = manifest.Version,
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update download cancelled");
            return new { cancelled = true };
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

    private object? InstallDownloadedUpdate(JsonElement payload)
    {
        var requested = payload.TryGetProperty("path", out var value) ? value.GetString() : null;
        if (!TryResolveDownloadedInstaller(requested, out var installerPath))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InstallerFailed, "invalid update installer path");
        }

        if (_environment.IsPortable)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InstallerFailed, "automatic installation is disabled in portable mode");
        }

        var installDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var parentDirectory = Directory.GetParent(installDirectory)?.FullName;
        if (string.Equals(Path.GetFileName(installDirectory), "current", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(parentDirectory))
        {
            installDirectory = parentDirectory;
        }

        var command = $"/c timeout /t 5 /nobreak >nul & start \"\" \"{installerPath}\" /SILENT /DIR=\"{installDirectory}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = command,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        });

        _logger.LogInformation(
            "Starting downloaded update installer {Path} into {InstallDirectory}",
            installerPath,
            installDirectory);
        _ = ShutdownForUpdateAsync();
        return new { installing = true };
    }

    private static bool TryResolveDownloadedInstaller(string? value, out string installerPath)
    {
        installerPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(value);
            var root = Path.GetFullPath(AppPaths.UpdateStagingDirectory) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(fullPath) ||
                !IsInstallerAsset(Path.GetFileName(fullPath)))
            {
                return false;
            }

            installerPath = fullPath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static async Task ShutdownForUpdateAsync()
    {
        await Task.Delay(350).ConfigureAwait(false);
        var application = System.Windows.Application.Current;
        if (application is not null)
        {
            await application.Dispatcher.InvokeAsync(application.Shutdown);
        }
    }

    private static bool IsInstallerAsset(string fileName)
    {
        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
               fileName.Contains("Setup", StringComparison.OrdinalIgnoreCase);
    }

    private static object? OpenUpdatePage(JsonElement payload)
    {
        var requested = payload.TryGetProperty("url", out var value) ? value.GetString() : null;
        var url = TryValidateDownloadUrl(requested) ? requested : CloudDownloadUrl;
        Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
        return new { opened = true };
    }

    private static bool TryValidateDownloadUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("pan.quark.cn", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("pan.baidu.com", StringComparison.OrdinalIgnoreCase);
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
}
