using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed partial class DeploymentEngine : IDeploymentEngine
{
    private static readonly string[] AlreadyRestoredWarnings =
    [
        "This deployment was already restored.",
    ];

    private readonly AppPaths _paths;
    private readonly BackupStore _backups;
    private readonly ILogger<DeploymentEngine> _logger;

    public DeploymentEngine(
        AppPaths paths,
        BackupStore backups,
        ILogger<DeploymentEngine> logger)
    {
        _paths = paths;
        _backups = backups;
        _logger = logger;
    }

    public async Task<Result<DeploymentInspection>> InspectAsync(
        DeploymentPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var target = ValidateTarget(plan.TargetDirectory);
        if (target.IsFailure)
        {
            return Result<DeploymentInspection>.Fail(target.Error!);
        }

        var planValidation = ValidatePlan(plan, target.Value);
        if (planValidation.IsFailure)
        {
            return Result<DeploymentInspection>.Fail(planValidation.Error!);
        }

        var manifest = await _backups.LoadManifestAsync(plan.FamilyId, target.Value, cancellationToken);
        if (manifest is null || manifest.State is DeploymentManifestState.Restored or DeploymentManifestState.RolledBack)
        {
            var matches = 0;
            foreach (var file in plan.Files)
            {
                var path = GetTargetPath(target.Value, file.RelativePath);
                if (path.IsSuccess && File.Exists(path.Value))
                {
                    var hash = await HashFileAsync(path.Value, cancellationToken);
                    if (string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                    }
                }
            }

            var state = matches > 0 ? DeploymentState.InstalledWithoutManifest : DeploymentState.NotDeployed;
            var details = matches > 0
                ? new[] { $"{matches} file(s) match the current pack but no manifest is present." }
                : Array.Empty<string>();

            return Result<DeploymentInspection>.Ok(new DeploymentInspection(
                state,
                manifest?.PackVersion,
                HasManifest: manifest is not null,
                details,
                Options: null));
        }

        var detailsList = new List<string>();
        var hasConflict = false;
        var hasMissing = false;
        var hasOutdated = manifest.PackId != plan.PackId ||
                          manifest.PackVersion != plan.PackVersion;

        if (manifest.State is DeploymentManifestState.Installing or DeploymentManifestState.Partial)
        {
            hasMissing = true;
            detailsList.Add("The previous deployment did not complete.");
        }

        var planned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in plan.Files)
        {
            var relative = NormalizeRelative(file.RelativePath);
            if (relative is not null)
            {
                planned[relative] = file.Sha256;
            }
        }

        var recorded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in manifest.Files)
        {
            recorded.Add(record.RelativePath);
            var path = GetTargetPath(target.Value, record.RelativePath);
            if (path.IsFailure)
            {
                hasConflict = true;
                detailsList.Add($"Invalid recorded path: {record.RelativePath}");
                continue;
            }

            if (!File.Exists(path.Value))
            {
                hasMissing = true;
                detailsList.Add($"Missing: {record.RelativePath}");
                continue;
            }

            var hash = await HashFileAsync(path.Value, cancellationToken);
            if (!string.Equals(hash, record.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                hasConflict = true;
                detailsList.Add($"Modified: {record.RelativePath}");
            }

            if (planned.TryGetValue(record.RelativePath, out var expectedHash) &&
                !string.Equals(expectedHash, record.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                hasOutdated = true;
                detailsList.Add($"Pack changed: {record.RelativePath}");
            }
        }

        foreach (var file in plan.Files)
        {
            var relative = NormalizeRelative(file.RelativePath);
            if (relative is not null && !recorded.Contains(relative))
            {
                hasOutdated = true;
                detailsList.Add($"New in pack: {relative}");
            }
        }

        var finalState = hasConflict
            ? DeploymentState.Conflict
            : hasMissing
                ? DeploymentState.Partial
                : hasOutdated
                    ? DeploymentState.Outdated
                    : DeploymentState.Installed;

        return Result<DeploymentInspection>.Ok(new DeploymentInspection(
            finalState,
            manifest.PackVersion,
            HasManifest: true,
            detailsList.Take(20).ToList(),
            manifest.Options));
    }

    public async Task<IReadOnlyDictionary<string, string>?> LoadOptionsAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        var target = ValidateTarget(targetDirectory);
        if (target.IsFailure)
        {
            return null;
        }

        var manifest = await _backups.LoadManifestAsync(familyId, target.Value, cancellationToken);
        if (manifest is null ||
            manifest.State is DeploymentManifestState.Restored or DeploymentManifestState.RolledBack ||
            manifest.Options.Count == 0)
        {
            return null;
        }

        return manifest.Options;
    }

    public Task<DeploymentManifest?> LoadManifestAsync(
        string familyId,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        var target = ValidateTarget(targetDirectory);
        return target.IsFailure
            ? Task.FromResult<DeploymentManifest?>(null)
            : _backups.LoadManifestAsync(familyId, target.Value, cancellationToken);
    }

    public async Task<Result<DeploymentReport>> DeployAsync(
        DeploymentPlan plan,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var target = ValidateTarget(plan.TargetDirectory);
        if (target.IsFailure)
        {
            return Result<DeploymentReport>.Fail(target.Error!);
        }

        var planValidation = ValidatePlan(plan, target.Value);
        if (planValidation.IsFailure)
        {
            return Result<DeploymentReport>.Fail(planValidation.Error!);
        }

        var lockDirectory = Path.Combine(_paths.StateDirectory, "locks");
        using var targetLock = TargetLock.TryAcquire(lockDirectory, plan.FamilyId, target.Value);
        if (targetLock is null)
        {
            return Result<DeploymentReport>.Fail(AppError.Create(
                ErrorCode.FileLocked,
                ("target", target.Value),
                ("reason", "another deployment is already running")));
        }

        var previous = await _backups.LoadManifestAsync(plan.FamilyId, target.Value, cancellationToken);
        var backupDirectory = _backups.GetBackupDirectory(plan.FamilyId, target.Value);
        var records = new List<DeployedFileRecord>(plan.Files.Count);
        var backupsCreated = 0;

        try
        {
            for (var index = 0; index < plan.Files.Count; index++)
            {
                var file = plan.Files[index];
                var relative = NormalizeRelative(file.RelativePath)!;
                var path = GetTargetPath(target.Value, relative);
                if (path.IsFailure)
                {
                    return Result<DeploymentReport>.Fail(path.Error!);
                }

                if (Directory.Exists(path.Value))
                {
                    return Result<DeploymentReport>.Fail(AppError.Create(
                        ErrorCode.Conflict,
                        ("path", relative),
                        ("reason", "a directory exists at the file target")));
                }

                var previousRecord = previous?.Files.FirstOrDefault(record =>
                    string.Equals(record.RelativePath, relative, StringComparison.OrdinalIgnoreCase));

                string? backupRelative = null;
                var existedBefore = false;

                if (previousRecord?.BackupRelativePath is { Length: > 0 } previousBackup)
                {
                    var previousBackupPath = BackupStore.GetBackupFilePath(backupDirectory, previousBackup);
                    if (File.Exists(previousBackupPath))
                    {
                        backupRelative = previousBackup;
                        existedBefore = previousRecord.ExistedBefore;
                    }
                }

                if (File.Exists(path.Value))
                {
                    if (IsFileLocked(path.Value))
                    {
                        return Result<DeploymentReport>.Fail(AppError.Create(
                            ErrorCode.FileLocked,
                            ("path", relative)));
                    }

                    var currentHash = await HashFileAsync(path.Value, cancellationToken);
                    var isOurs = string.Equals(currentHash, file.Sha256, StringComparison.OrdinalIgnoreCase) ||
                                 (previousRecord is not null &&
                                  string.Equals(currentHash, previousRecord.Sha256, StringComparison.OrdinalIgnoreCase));

                    if (backupRelative is null && !isOurs)
                    {
                        backupRelative = "files/" + relative;
                        var backupPath = BackupStore.GetBackupFilePath(backupDirectory, backupRelative);
                        var backupParent = Path.GetDirectoryName(backupPath);
                        if (!string.IsNullOrWhiteSpace(backupParent))
                        {
                            Directory.CreateDirectory(backupParent);
                        }

                        File.Copy(path.Value, backupPath, overwrite: true);
                        existedBefore = true;
                        backupsCreated++;
                    }
                }

                records.Add(new DeployedFileRecord(
                    relative,
                    file.Sha256,
                    file.Size,
                    existedBefore,
                    backupRelative,
                    file.Role));

                progress?.Report(new DeploymentProgress("planning", relative, index + 1, plan.Files.Count));
            }
        }
        catch (OperationCanceledException)
        {
            return Result<DeploymentReport>.Fail(new AppError(ErrorCode.OperationCancelled));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<DeploymentReport>.Fail(MapIoError(exception, target.Value));
        }

        foreach (var file in plan.Files)
        {
            var verification = await VerifyPlannedFileAsync(file, cancellationToken);
            if (verification.IsFailure)
            {
                return Result<DeploymentReport>.Fail(verification.Error!);
            }
        }

        var manifest = CreateManifest(plan, target.Value, DeploymentManifestState.Installing, records);
        try
        {
            await _backups.SaveManifestAsync(manifest, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<DeploymentReport>.Fail(new AppError(ErrorCode.OperationCancelled));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<DeploymentReport>.Fail(MapIoError(exception, target.Value));
        }

        var written = new List<DeployedFileRecord>();
        try
        {
            for (var index = 0; index < plan.Files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = plan.Files[index];
                var record = records[index];
                var path = GetTargetPath(target.Value, record.RelativePath);
                if (path.IsFailure)
                {
                    throw new IOException(path.Error?.Diagnostic ?? "Invalid target path.");
                }

                var parent = Path.GetDirectoryName(path.Value);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                var temp = path.Value + ".fgm-" + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    if (file.SourcePath is not null)
                    {
                        File.Copy(file.SourcePath, temp, overwrite: true);
                    }
                    else if (file.Content is not null)
                    {
                        await File.WriteAllBytesAsync(temp, file.Content, cancellationToken);
                    }
                    else
                    {
                        throw new IOException("Planned file has neither content nor source.");
                    }

                    File.Move(temp, path.Value, overwrite: true);
                    var hash = await HashFileAsync(path.Value, cancellationToken);
                    if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException($"Hash verification failed for {record.RelativePath}.");
                    }

                    written.Add(record);
                }
                finally
                {
                    if (File.Exists(temp))
                    {
                        try
                        {
                            File.Delete(temp);
                        }
                        catch (IOException)
                        {
                            // Best-effort cleanup.
                        }
                    }
                }

                progress?.Report(new DeploymentProgress("writing", record.RelativePath, index + 1, plan.Files.Count));
            }

            var warnings = await CleanupStaleAsync(previous, plan, target.Value, backupDirectory, cancellationToken);
            var installed = CreateManifest(plan, target.Value, DeploymentManifestState.Installed, records);
            await _backups.SaveManifestAsync(installed, cancellationToken);
            progress?.Report(new DeploymentProgress("done", string.Empty, plan.Files.Count, plan.Files.Count));

            return Result<DeploymentReport>.Ok(new DeploymentReport(
                DeploymentState.Installed,
                written.Count,
                backupsCreated,
                warnings));
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync(written, target.Value, backupDirectory);
            await SaveRolledBackAsync(manifest);
            return Result<DeploymentReport>.Fail(new AppError(ErrorCode.OperationCancelled));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Deployment failed for {Target}", target.Value);
            await RollbackAsync(written, target.Value, backupDirectory);
            await SaveRolledBackAsync(manifest);
            return Result<DeploymentReport>.Fail(MapIoError(exception, target.Value));
        }
    }

}
