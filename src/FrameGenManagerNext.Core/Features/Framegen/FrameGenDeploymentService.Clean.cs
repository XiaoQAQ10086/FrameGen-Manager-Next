using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Framegen;

public sealed partial class FrameGenDeploymentService
{
    /// <summary>
    /// Cleans a FrameGen deployment that has no manifest. Candidates come from the
    /// full pack file table, so every proxy slot is covered and only files whose
    /// content still matches the pack are deleted.
    /// </summary>
    public async Task<Result<RestoreReport>> CleanAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Result<RestoreReport>.Fail(new AppError(ErrorCode.InvalidPath, "target directory is empty"));
        }

        var location = await _packs.FindAsync(PackId, cancellationToken);
        if (location is null)
        {
            return Result<RestoreReport>.Fail(AppError.Create(ErrorCode.PackNotFound, ("pack", PackId)));
        }

        var schema = AssetPackValidator.ValidateSchema(location.Pack, AppInfo.Version);
        if (!schema.IsValid)
        {
            var diagnostic = string.Join("; ", schema.Errors.Select(error => $"{error.Code} {error.Path}: {error.Message}"));
            return Result<RestoreReport>.Fail(AppError.Create(
                ErrorCode.PackInvalid,
                ("pack", PackId),
                ("reason", diagnostic)));
        }

        var pack = location.Pack;
        var candidates = new List<CleanupCandidate>();
        foreach (var file in pack.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Sha256))
            {
                continue;
            }

            candidates.Add(new CleanupCandidate(file.Target, file.Sha256));
        }

        // The old tool may have deployed a different asset version; its manifest
        // carries the hashes actually written, so cleaning can still prove ownership.
        var legacy = await _legacy.FindAsync(targetDirectory, cancellationToken);
        if (legacy is not null)
        {
            var known = new HashSet<string>(
                candidates.Select(candidate => $"{candidate.RelativePath}|{candidate.Sha256}"),
                StringComparer.OrdinalIgnoreCase);
            foreach (var file in legacy.Files)
            {
                if (known.Add($"{file.RelativePath}|{file.DeployedSha256}"))
                {
                    candidates.Add(new CleanupCandidate(file.RelativePath, file.DeployedSha256));
                }
            }
        }

        _logger.LogInformation(
            "Cleaning unrecorded FrameGen deployment in {Target} with {Count} candidate(s)",
            targetDirectory,
            candidates.Count);

        return await _engine.CleanAsync(
            FamilyId,
            targetDirectory,
            candidates,
            pack.OwnedDirectories,
            pack.Ini?.Target,
            progress,
            cancellationToken);
    }
}
