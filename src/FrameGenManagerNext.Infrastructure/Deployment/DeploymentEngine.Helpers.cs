using System.Security.Cryptography;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Safety;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed partial class DeploymentEngine
{
    private static Result<string> ValidateTarget(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Result<string>.Fail(new AppError(ErrorCode.InvalidPath, "target directory is empty"));
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(targetDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<string>.Fail(new AppError(ErrorCode.InvalidPath, exception.Message));
        }

        if (!Directory.Exists(fullPath))
        {
            return Result<string>.Fail(new AppError(ErrorCode.InvalidPath, $"target directory does not exist: {fullPath}"));
        }

        if (SafePath.IsProtectedSystemDirectory(fullPath))
        {
            return Result<string>.Fail(AppError.Create(
                ErrorCode.ProtectedPath,
                ("target", fullPath)));
        }

        return Result<string>.Ok(fullPath);
    }

    private static Result ValidatePlan(DeploymentPlan plan, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(plan.FamilyId) ||
            string.IsNullOrWhiteSpace(plan.PackId) ||
            string.IsNullOrWhiteSpace(plan.PackVersion) ||
            plan.Files is null ||
            plan.Files.Count == 0)
        {
            return Result.Fail(new AppError(ErrorCode.InvalidArgument, "deployment plan is incomplete"));
        }

        if (!string.Equals(
                Path.GetFullPath(plan.TargetDirectory),
                targetDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(new AppError(ErrorCode.InvalidPath, "plan target does not match the resolved target"));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in plan.Files)
        {
            var relative = NormalizeRelative(file.RelativePath);
            if (relative is null)
            {
                return Result.Fail(new AppError(ErrorCode.InvalidPath, file.RelativePath));
            }

            if (!seen.Add(relative))
            {
                return Result.Fail(AppError.Create(
                    ErrorCode.Conflict,
                    ("path", relative),
                    ("reason", "duplicate target path")));
            }

            if (!IsValidSha256(file.Sha256) || file.Size < 0)
            {
                return Result.Fail(new AppError(ErrorCode.InvalidArgument, "file hash or size is invalid"));
            }

            if ((file.SourcePath is null) == (file.Content is null))
            {
                return Result.Fail(new AppError(
                    ErrorCode.InvalidArgument,
                    "file must have exactly one of source path or content"));
            }

            if (file.SourcePath is not null)
            {
                if (!File.Exists(file.SourcePath))
                {
                    return Result.Fail(AppError.Create(
                        ErrorCode.PackInvalid,
                        ("file", file.SourcePath),
                        ("reason", "source file is missing")));
                }

                if (new FileInfo(file.SourcePath).Length != file.Size)
                {
                    return Result.Fail(AppError.Create(
                        ErrorCode.PackInvalid,
                        ("file", file.SourcePath),
                        ("reason", "source size does not match the plan")));
                }
            }
            else if (file.Content!.LongLength != file.Size)
            {
                return Result.Fail(new AppError(ErrorCode.InvalidArgument, "content size does not match the plan"));
            }
        }

        return Result.Ok();
    }

    private static async Task<Result> VerifyPlannedFileAsync(
        DeploymentFilePlan file,
        CancellationToken cancellationToken)
    {
        if (file.SourcePath is not null)
        {
            var hash = await HashFileAsync(file.SourcePath, cancellationToken);
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail(AppError.Create(
                    ErrorCode.PackHashMismatch,
                    ("file", file.SourcePath)));
            }

            return Result.Ok();
        }

        if (file.Content is null)
        {
            return Result.Fail(new AppError(ErrorCode.InvalidArgument, "file has no content"));
        }

        var contentHash = Convert.ToHexString(SHA256.HashData(file.Content)).ToLowerInvariant();
        if (!string.Equals(contentHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(new AppError(ErrorCode.PackHashMismatch, "generated content hash mismatch"));
        }

        return Result.Ok();
    }

    private static async Task<List<string>> CleanupStaleAsync(
        DeploymentManifest? previous,
        DeploymentPlan plan,
        string targetDirectory,
        string backupDirectory,
        CancellationToken cancellationToken)
    {
        if (previous is null ||
            previous.State is DeploymentManifestState.Restored or DeploymentManifestState.RolledBack)
        {
            return [];
        }

        var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in plan.Files)
        {
            var relative = NormalizeRelative(file.RelativePath);
            if (relative is not null)
            {
                planned.Add(relative);
            }
        }

        var warnings = new List<string>();
        foreach (var record in previous.Files)
        {
            if (planned.Contains(record.RelativePath))
            {
                continue;
            }

            var path = GetTargetPath(targetDirectory, record.RelativePath);
            if (path.IsFailure || !File.Exists(path.Value))
            {
                continue;
            }

            try
            {
                if (IsFileLocked(path.Value))
                {
                    warnings.Add($"Stale file is locked: {record.RelativePath}");
                    continue;
                }

                var hash = await HashFileAsync(path.Value, cancellationToken);
                if (!string.Equals(hash, record.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Stale file was modified and left in place: {record.RelativePath}");
                    continue;
                }

                if (record.ExistedBefore && record.BackupRelativePath is { Length: > 0 } backupRelative)
                {
                    var backupPath = BackupStore.GetBackupFilePath(backupDirectory, backupRelative);
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, path.Value, overwrite: true);
                        continue;
                    }
                }

                File.Delete(path.Value);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Could not remove stale file {record.RelativePath}: {exception.Message}");
            }
        }

        return warnings;
    }

    private async Task RollbackAsync(
        IReadOnlyList<DeployedFileRecord> written,
        string targetDirectory,
        string backupDirectory)
    {
        foreach (var record in written.Reverse())
        {
            var path = GetTargetPath(targetDirectory, record.RelativePath);
            if (path.IsFailure)
            {
                continue;
            }

            try
            {
                if (record.ExistedBefore && record.BackupRelativePath is { Length: > 0 } backupRelative)
                {
                    var backupPath = BackupStore.GetBackupFilePath(backupDirectory, backupRelative);
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, path.Value, overwrite: true);
                        continue;
                    }
                }

                if (File.Exists(path.Value))
                {
                    File.Delete(path.Value);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Rollback failed for {Path}", path.Value);
            }
        }

        await Task.CompletedTask;
    }

    private async Task SaveRolledBackAsync(DeploymentManifest manifest)
    {
        try
        {
            var rolledBack = CreateManifest(manifest, manifest.TargetDirectory, DeploymentManifestState.RolledBack);
            await _backups.SaveManifestAsync(rolledBack, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not persist rolled-back manifest");
        }
    }

    private static DeploymentManifest CreateManifest(
        DeploymentPlan plan,
        string targetDirectory,
        DeploymentManifestState state,
        IReadOnlyList<DeployedFileRecord> files)
    {
        return new DeploymentManifest
        {
            FamilyId = plan.FamilyId,
            PackId = plan.PackId,
            PackVersion = plan.PackVersion,
            TargetDirectory = targetDirectory,
            CreatedUtc = DateTimeOffset.UtcNow,
            State = state,
            Files = files.ToList(),
            Options = new Dictionary<string, string>(plan.Options, StringComparer.Ordinal),
        };
    }

    private static DeploymentManifest CreateManifest(
        DeploymentManifest manifest,
        string targetDirectory,
        DeploymentManifestState state)
    {
        return new DeploymentManifest
        {
            FamilyId = manifest.FamilyId,
            PackId = manifest.PackId,
            PackVersion = manifest.PackVersion,
            TargetDirectory = targetDirectory,
            CreatedUtc = manifest.CreatedUtc,
            State = state,
            Files = manifest.Files,
            Options = manifest.Options,
        };
    }

    private static void RemoveEmptyParents(DeploymentManifest manifest, string targetDirectory)
    {
        foreach (var record in manifest.Files)
        {
            var path = GetTargetPath(targetDirectory, record.RelativePath);
            if (path.IsFailure)
            {
                continue;
            }

            var parent = Path.GetDirectoryName(path.Value);
            while (!string.IsNullOrWhiteSpace(parent) &&
                   SafePath.IsUnderRoot(targetDirectory, parent) &&
                   !string.Equals(
                       Path.TrimEndingDirectorySeparator(parent),
                       Path.TrimEndingDirectorySeparator(targetDirectory),
                       StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(parent) || Directory.EnumerateFileSystemEntries(parent).Any())
                    {
                        break;
                    }

                    Directory.Delete(parent);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    break;
                }

                parent = Path.GetDirectoryName(parent);
            }
        }
    }

    private static List<string> CollectLeftovers(
        DeploymentManifest manifest,
        string targetDirectory)
    {
        var recorded = new HashSet<string>(
            manifest.Files.Select(file => file.RelativePath),
            StringComparer.OrdinalIgnoreCase);

        var segments = manifest.Files
            .Select(file => file.RelativePath.Split('/')[0])
            .Where(segment => !segment.Contains('.', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var leftovers = new List<string>();
        foreach (var segment in segments)
        {
            var directory = GetTargetPath(targetDirectory, segment);
            if (directory.IsFailure || !Directory.Exists(directory.Value))
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory.Value, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(targetDirectory, file).Replace('\\', '/');
                    if (!recorded.Contains(relative))
                    {
                        leftovers.Add(relative);
                    }

                    if (leftovers.Count >= 20)
                    {
                        return leftovers;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                leftovers.Add($"{segment}: could not enumerate leftovers ({exception.Message})");
            }
        }

        return leftovers;
    }

    private static Result<string> GetTargetPath(string targetDirectory, string relativePath)
    {
        return SafePath.CombineUnderRoot(targetDirectory, relativePath);
    }

    private static string? NormalizeRelative(string? relativePath)
    {
        var result = SafePath.NormalizeRelativePath(relativePath);
        return result.IsSuccess ? result.Value : null;
    }

    private static bool IsFileLocked(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsValidSha256(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static AppError MapIoError(Exception exception, string targetDirectory)
    {
        var code = exception is UnauthorizedAccessException
            ? ErrorCode.AccessDenied
            : ErrorCode.FileLocked;

        return AppError.Create(
            code,
            ("target", targetDirectory),
            ("reason", exception.Message));
    }
}
