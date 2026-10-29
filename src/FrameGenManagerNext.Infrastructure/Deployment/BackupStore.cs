using System.Text.Json;
using System.Text.Json.Serialization;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Safety;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed class BackupStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private readonly AppPaths _paths;
    private readonly ILogger<BackupStore> _logger;

    public BackupStore(AppPaths paths, ILogger<BackupStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public string GetBackupDirectory(string familyId, string targetDirectory)
    {
        var family = SafePath.NormalizeRelativePath(familyId);
        var segment = family.IsSuccess
            ? family.Value.Replace('/', '_')
            : "unknown";

        return Path.Combine(_paths.BackupsDirectory, segment, DeploymentKey.ForPath(targetDirectory));
    }

    public async Task<DeploymentManifest?> LoadManifestAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        var manifestPath = Path.Combine(GetBackupDirectory(familyId, targetDirectory), "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return await JsonSerializer.DeserializeAsync<DeploymentManifest>(
                stream,
                Options,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Deployment manifest is corrupt: {Path}", manifestPath);
            TryQuarantine(manifestPath);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not read deployment manifest: {Path}", manifestPath);
            return null;
        }
    }

    public async Task SaveManifestAsync(
        DeploymentManifest manifest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _paths.EnsureCreated();

        var directory = GetBackupDirectory(manifest.FamilyId, manifest.TargetDirectory);
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "manifest.json");
        var tempPath = manifestPath + ".tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // A stale temp manifest is harmless.
                }
            }
        }
    }

    public static string GetBackupFilePath(string backupDirectory, string backupRelativePath)
    {
        var path = SafePath.CombineUnderRoot(backupDirectory, backupRelativePath);
        if (path.IsFailure)
        {
            throw new IOException(path.Error?.Diagnostic ?? "Invalid backup path.");
        }

        return path.Value;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static void TryQuarantine(string manifestPath)
    {
        try
        {
            File.Move(manifestPath, manifestPath + ".bad", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep the original file if it cannot be moved.
        }
    }
}
