using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed partial class DeploymentEngine
{
    /// <summary>
    /// Removes a deployment that has no manifest (installed by the old tool or by hand).
    /// Files are deleted only when their content still matches the pack hash, so a file
    /// the user replaced is reported instead of being destroyed.
    /// </summary>
    public async Task<Result<RestoreReport>> CleanAsync(
        string familyId,
        string targetDirectory,
        IReadOnlyList<CleanupCandidate> candidates,
        IReadOnlyList<string> ownedDirectories,
        string? iniRelativePath,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        ArgumentNullException.ThrowIfNull(candidates);

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
        if (manifest is not null &&
            manifest.State is not (DeploymentManifestState.Restored or DeploymentManifestState.RolledBack))
        {
            // A recorded deployment belongs to RestoreAsync, where the originals can be
            // put back. Deleting it here would silently discard those backups.
            return Result<RestoreReport>.Fail(AppError.Create(
                ErrorCode.Conflict,
                ("target", target.Value),
                ("reason", "a deployment record exists; use restore instead")));
        }

        var warnings = new List<string>();
        var removed = 0;
        var conflicts = 0;
        var conflictedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var leftovers = new List<string>();

        var groups = candidates
            .GroupBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < groups.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = groups[index];
            var relative = group.Key;
            var path = GetTargetPath(target.Value, relative);
            if (path.IsFailure)
            {
                warnings.Add($"Invalid recorded path: {relative}");
                continue;
            }

            if (!File.Exists(path.Value))
            {
                continue;
            }

            var hash = await HashFileAsync(path.Value, cancellationToken);
            var matches = group.Any(candidate =>
                string.Equals(hash, candidate.Sha256, StringComparison.OrdinalIgnoreCase));
            if (!matches)
            {
                conflicts++;
                conflictedPaths.Add(path.Value);
                leftovers.Add(relative);
                warnings.Add($"Modified file left in place: {relative}");
                continue;
            }

            try
            {
                File.Delete(path.Value);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                leftovers.Add(relative);
                warnings.Add($"Could not delete '{relative}': {exception.Message}");
            }

            progress?.Report(new DeploymentProgress(
                "cleaning",
                relative,
                index + 1,
                groups.Count));
        }

        if (removed == 0)
        {
            // Nothing proved this folder holds our deployment, so leave every file alone.
            return Result<RestoreReport>.Fail(AppError.Create(
                ErrorCode.NothingToUninstall,
                ("target", target.Value)));
        }

        // The INI is generated per game from the user's options, so it never matches the
        // pack hash. Delete it once at least one owned file proved this is our deployment.
        if (removed > 0 && !string.IsNullOrWhiteSpace(iniRelativePath))
        {
            var iniPath = GetTargetPath(target.Value, iniRelativePath);
            if (iniPath.IsSuccess && File.Exists(iniPath.Value))
            {
                try
                {
                    File.Delete(iniPath.Value);
                    removed++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"Could not delete '{iniRelativePath}': {exception.Message}");
                }
            }
        }

        SweepOwnedDirectories(ownedDirectories, target.Value, conflictedPaths, warnings);
        progress?.Report(new DeploymentProgress("done", string.Empty, removed, candidates.Count));
        _logger.LogInformation(
            "Cleaned unrecorded deployment in {Target}: {Removed} file(s) removed, {Conflicts} conflict(s)",
            target.Value,
            removed,
            conflicts);

        return Result<RestoreReport>.Ok(new RestoreReport(0, removed, conflicts, leftovers, warnings));
    }
}
