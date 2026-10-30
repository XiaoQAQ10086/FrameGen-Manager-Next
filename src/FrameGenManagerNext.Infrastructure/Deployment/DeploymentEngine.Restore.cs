using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed partial class DeploymentEngine
{
    public async Task<Result<RestoreReport>> RestoreAsync(
        string familyId,
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        var target = ValidateTarget(targetDirectory);
        if (target.IsFailure)
        {
            return Result<RestoreReport>.Fail(target.Error!);
        }

        var lockDirectory = Path.Combine(_paths.StateDirectory, "locks");
        using var targetLock = TargetLock.TryAcquire(lockDirectory, familyId, target.Value);
        if (targetLock is null)
        {
            return Result<RestoreReport>.Fail(AppError.Create(
                ErrorCode.FileLocked,
                ("target", target.Value),
                ("reason", "another operation is already running")));
        }

        var manifest = await _backups.LoadManifestAsync(familyId, target.Value, cancellationToken);
        if (manifest is null)
        {
            return Result<RestoreReport>.Fail(AppError.Create(
                ErrorCode.ManifestMissing,
                ("target", target.Value)));
        }

        if (manifest.State is DeploymentManifestState.Restored)
        {
            return Result<RestoreReport>.Ok(new RestoreReport(
                0,
                0,
                0,
                Array.Empty<string>(),
                AlreadyRestoredWarnings));
        }

        var backupDirectory = _backups.GetBackupDirectory(familyId, target.Value);
        var warnings = new List<string>();
        var restored = 0;
        var removed = 0;
        var conflicts = 0;

        foreach (var record in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetTargetPath(target.Value, record.RelativePath);
            if (path.IsFailure)
            {
                warnings.Add($"Invalid recorded path: {record.RelativePath}");
                conflicts++;
                continue;
            }

            var backupPath = record.BackupRelativePath is { Length: > 0 } backupRelative
                ? BackupStore.GetBackupFilePath(backupDirectory, backupRelative)
                : null;

            if (File.Exists(path.Value))
            {
                var hash = await HashFileAsync(path.Value, cancellationToken);
                if (!string.Equals(hash, record.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts++;
                    warnings.Add($"Modified file left in place: {record.RelativePath}");
                    continue;
                }

                if (record.ExistedBefore)
                {
                    if (backupPath is not null && File.Exists(backupPath))
                    {
                        File.Copy(backupPath, path.Value, overwrite: true);
                        restored++;
                    }
                    else
                    {
                        warnings.Add($"Original backup is missing: {record.RelativePath}");
                    }
                }
                else
                {
                    File.Delete(path.Value);
                    removed++;
                }
            }
            else if (record.ExistedBefore)
            {
                if (backupPath is not null && File.Exists(backupPath))
                {
                    var parent = Path.GetDirectoryName(path.Value);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    File.Copy(backupPath, path.Value, overwrite: true);
                    restored++;
                }
                else
                {
                    warnings.Add($"Original backup is missing: {record.RelativePath}");
                }
            }

            progress?.Report(new DeploymentProgress(
                "restoring",
                record.RelativePath,
                restored + removed,
                manifest.Files.Count));
        }

        RemoveEmptyParents(manifest, target.Value);
        var leftovers = CollectLeftovers(manifest, target.Value);
        var clean = conflicts == 0 && warnings.Count == 0 && leftovers.Count == 0;
        var finalState = clean
            ? DeploymentManifestState.Restored
            : DeploymentManifestState.Partial;
        var restoredManifest = CreateManifest(manifest, target.Value, finalState);
        await _backups.SaveManifestAsync(restoredManifest, cancellationToken);
        progress?.Report(new DeploymentProgress("done", string.Empty, manifest.Files.Count, manifest.Files.Count));

        return Result<RestoreReport>.Ok(new RestoreReport(restored, removed, conflicts, leftovers, warnings));
    }

    public Task<Result> DiscardBackupAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        cancellationToken.ThrowIfCancellationRequested();

        var target = ValidateTarget(targetDirectory);
        if (target.IsFailure)
        {
            return Task.FromResult(Result.Fail(target.Error!));
        }

        var backupDirectory = _backups.GetBackupDirectory(familyId, target.Value);
        if (!Directory.Exists(backupDirectory))
        {
            return Task.FromResult(Result.Ok());
        }

        try
        {
            Directory.Delete(backupDirectory, recursive: true);
            _logger.LogInformation("Discarded deployment backup at {Path}", backupDirectory);
            return Task.FromResult(Result.Ok());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not discard deployment backup at {Path}", backupDirectory);
            return Task.FromResult(Result.Fail(new AppError(
                exception is UnauthorizedAccessException ? ErrorCode.AccessDenied : ErrorCode.FileLocked,
                exception.Message)));
        }
    }
}
