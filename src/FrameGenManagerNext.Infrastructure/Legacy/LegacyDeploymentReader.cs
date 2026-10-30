using System.Text.Json;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Safety;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Legacy;

/// <summary>
/// Reads manifests written by the old tool from %APPDATA%\FrameGen-Manager\backups
/// (and the earlier %APPDATA%\DLSSG-Manager\backups). Nothing is ever written there.
/// </summary>
public sealed class LegacyDeploymentReader : ILegacyDeploymentReader
{
    private readonly ILogger<LegacyDeploymentReader> _logger;
    private readonly IReadOnlyList<string>? _backupRoots;
    private readonly object _gate = new();
    private IReadOnlyList<LegacyDeploymentInfo>? _cache;

    public LegacyDeploymentReader(ILogger<LegacyDeploymentReader> logger)
        : this(logger, backupRoots: null)
    {
    }

    public LegacyDeploymentReader(
        ILogger<LegacyDeploymentReader> logger,
        IReadOnlyList<string>? backupRoots)
    {
        _logger = logger;
        _backupRoots = backupRoots;
    }

    public Task<LegacyDeploymentInfo?> FindAsync(
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Task.FromResult<LegacyDeploymentInfo?>(null);
        }

        string normalized;
        try
        {
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Task.FromResult<LegacyDeploymentInfo?>(null);
        }

        var match = EnsureLoaded().FirstOrDefault(info =>
            string.Equals(info.GameDirectory, normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    private IReadOnlyList<LegacyDeploymentInfo> EnsureLoaded()
    {
        lock (_gate)
        {
            return _cache ??= Load();
        }
    }

    private List<LegacyDeploymentInfo> Load()
    {
        var results = new List<LegacyDeploymentInfo>();
        var roots = _backupRoots;
        if (roots is null)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                return results;
            }

            roots =
            [
                Path.Combine(appData, "FrameGen-Manager", "backups"),
                Path.Combine(appData, "DLSSG-Manager", "backups"),
            ];
        }

        foreach (var backups in roots)
        {
            if (!Directory.Exists(backups))
            {
                continue;
            }

            try
            {
                foreach (var manifestPath in Directory.EnumerateFiles(
                             backups,
                             "manifest.json",
                             SearchOption.AllDirectories))
                {
                    var parsed = Parse(manifestPath);
                    if (parsed is not null)
                    {
                        results.Add(parsed);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not scan legacy backups at {Path}", backups);
            }
        }

        return results;
    }

    private LegacyDeploymentInfo? Parse(string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var gameDir = root.TryGetProperty("game_dir", out var dirElement)
                ? dirElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(gameDir))
            {
                return null;
            }

            string normalized;
            try
            {
                normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDir));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }

            var files = new List<LegacyDeployedFile>();
            if (root.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in filesElement.EnumerateArray())
                {
                    var relative = file.TryGetProperty("rel_path", out var relElement)
                        ? relElement.GetString()
                        : null;
                    var hash = file.TryGetProperty("deployed_sha256", out var hashElement)
                        ? hashElement.GetString()
                        : null;

                    var normalizedRelative = SafePath.NormalizeRelativePath(relative);
                    if (normalizedRelative.IsFailure ||
                        string.IsNullOrWhiteSpace(hash) ||
                        hash.Length != 64)
                    {
                        continue;
                    }

                    files.Add(new LegacyDeployedFile(normalizedRelative.Value, hash.ToLowerInvariant()));
                }
            }

            var proxy = root.TryGetProperty("proxy_name", out var proxyElement)
                ? proxyElement.GetString()
                : null;
            var installing = root.TryGetProperty("installing", out var installingElement) &&
                             installingElement.ValueKind == JsonValueKind.True;

            return new LegacyDeploymentInfo(manifestPath, proxy, installing, files)
            {
                GameDirectory = normalized,
            };
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not read legacy manifest {Path}", manifestPath);
            return null;
        }
    }
}
