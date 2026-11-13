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
        CancellationToken cancellationToken,
        IReadOnlyList<string>? ownedDirectories = null,
        bool force = false)
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
        var conflictedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conflictedFiles = new List<string>();
        // Files that held original content before deployment belong to the game and must
        // survive the owned-directory sweep once they have been restored.
        var keepPaths = manifest.Files
            .Where(record => record.ExistedBefore)
            .Select(record => GetTargetPath(target.Value, record.RelativePath))
            .Where(path => path.IsSuccess)
            .Select(path => path.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var record in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetTargetPath(target.Value, record.RelativePath);
            if (path.IsFailure)
            {
                warnings.Add($"Invalid recorded path: {record.RelativePath}");
                conflicts++;
                conflictedFiles.Add(record.RelativePath);
                continue;
            }

            var backupPath = record.BackupRelativePath is { Length: > 0 } backupRelative
                ? BackupStore.GetBackupFilePath(backupDirectory, backupRelative)
                : null;

            if (File.Exists(path.Value))
            {
                string hash;
                try
                {
                    hash = await HashFileAsync(path.Value, cancellationToken);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or TimeoutException)
                {
                    conflicts++;
                    conflictedPaths.Add(path.Value);
                    conflictedFiles.Add(record.RelativePath);
                    warnings.Add($"Could not read {record.RelativePath}: {exception.Message}");
                    continue;
                }

                if (!string.Equals(hash, record.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    // Steam/game repair or a manual restore may have put the original
                    // file back already. That is a completed restore, not a conflict.
                    if (record.ExistedBefore &&
                        backupPath is not null &&
                        File.Exists(backupPath))
                    {
                        var originalHash = await HashFileAsync(backupPath, cancellationToken);
                        if (string.Equals(hash, originalHash, StringComparison.OrdinalIgnoreCase))
                        {
                            restored++;
                            continue;
                        }
                    }

                    if (force)
                    {
                        // The user explicitly asked to force the restore. A runtime-written
                        // config/log or a proxy replaced by another mod must not block the
                        // uninstall forever.
                        if (record.ExistedBefore && backupPath is not null && File.Exists(backupPath))
                        {
                            if (TryFileAction(
                                    () => File.Copy(backupPath, path.Value, overwrite: true),
                                    record.RelativePath,
                                    warnings))
                            {
                                restored++;
                            }
                            else
                            {
                                conflicts++;
                                conflictedPaths.Add(path.Value);
                                conflictedFiles.Add(record.RelativePath);
                            }
                        }
                        else if (record.ExistedBefore)
                        {
                            // The backup needed to put the original back is gone. The
                            // user explicitly chose force, so remove our file instead of
                            // leaving the deployment stuck forever.
                            if (TryFileAction(
                                    () => File.Delete(path.Value),
                                    record.RelativePath,
                                    warnings))
                            {
                                removed++;
                                warnings.Add($"Original backup is missing; deleted {record.RelativePath}.");
                            }
                            else
                            {
                                conflicts++;
                                conflictedPaths.Add(path.Value);
                                conflictedFiles.Add(record.RelativePath);
                            }
                        }
                        else
                        {
                            if (TryFileAction(
                                    () => File.Delete(path.Value),
                                    record.RelativePath,
                                    warnings))
                            {
                                removed++;
                            }
                            else
                            {
                                conflicts++;
                                conflictedPaths.Add(path.Value);
                                conflictedFiles.Add(record.RelativePath);
                            }
                        }

                        continue;
                    }

                    conflicts++;
                    conflictedPaths.Add(path.Value);
                    conflictedFiles.Add(record.RelativePath);
                    warnings.Add($"Modified file left in place: {record.RelativePath}");
                    continue;
                }

                if (record.ExistedBefore)
                {
                    if (backupPath is not null && File.Exists(backupPath))
                    {
                        if (TryFileAction(
                                () => File.Copy(backupPath, path.Value, overwrite: true),
                                record.RelativePath,
                                warnings))
                        {
                            restored++;
                        }
                        else
                        {
                            conflicts++;
                            conflictedPaths.Add(path.Value);
                            conflictedFiles.Add(record.RelativePath);
                        }
                    }
                    else
                    {
                        conflicts++;
                        conflictedPaths.Add(path.Value);
                        conflictedFiles.Add(record.RelativePath);
                        warnings.Add($"Original backup is missing: {record.RelativePath}");
                    }
                }
                else
                {
                    if (TryFileAction(
                            () => File.Delete(path.Value),
                            record.RelativePath,
                            warnings))
                    {
                        removed++;
                    }
                    else
                    {
                        conflicts++;
                        conflictedPaths.Add(path.Value);
                        conflictedFiles.Add(record.RelativePath);
                    }
                }
            }
            else if (record.ExistedBefore)
            {
                if (backupPath is not null && File.Exists(backupPath))
                {
                    var parent = Path.GetDirectoryName(path.Value);
                    if (TryFileAction(
                            () =>
                            {
                                if (!string.IsNullOrWhiteSpace(parent))
                                {
                                    Directory.CreateDirectory(parent);
                                }

                                File.Copy(backupPath, path.Value, overwrite: true);
                            },
                            record.RelativePath,
                            warnings))
                    {
                        restored++;
                    }
                    else
                    {
                        conflicts++;
                        conflictedPaths.Add(path.Value);
                        conflictedFiles.Add(record.RelativePath);
                    }
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
        var protectedPaths = new HashSet<string>(conflictedPaths, StringComparer.OrdinalIgnoreCase);
        protectedPaths.UnionWith(keepPaths);
        SweepOwnedDirectories(ownedDirectories, target.Value, protectedPaths, warnings);
        var leftovers = CollectLeftovers(manifest, target.Value);
        // Warnings are informational (for example, an original backup is missing
        // but the forced restore removed the file). Only files we could not remove
        // or restore keep the deployment in Partial.
        var clean = conflicts == 0 && leftovers.Count == 0;
        var finalState = clean
            ? DeploymentManifestState.Restored
            : DeploymentManifestState.Partial;
        var restoredManifest = CreateManifest(manifest, target.Value, finalState);
        await _backups.SaveManifestAsync(restoredManifest, cancellationToken);
        progress?.Report(new DeploymentProgress("done", string.Empty, manifest.Files.Count, manifest.Files.Count));

        return Result<RestoreReport>.Ok(new RestoreReport(
            restored,
            removed,
            conflicts,
            leftovers,
            warnings,
            conflictedFiles));
    }

    private static bool TryFileAction(
        Action action,
        string relativePath,
        List<string> warnings)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not update {relativePath}: {exception.Message}");
            return false;
        }
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
