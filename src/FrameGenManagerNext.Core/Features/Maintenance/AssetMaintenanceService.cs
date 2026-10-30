using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Maintenance;

public sealed class AssetMaintenanceService : IAssetMaintenanceService
{
    private readonly IGameLibraryService _library;
    private readonly IDeploymentEngine _engine;
    private readonly IPackRepository _packs;
    private readonly IFrameGenDeploymentService _frameGen;
    private readonly ISmoothMotionDeploymentService _smoothMotion;
    private readonly IDlss5DeploymentService _dlss5;
    private readonly ILogger<AssetMaintenanceService> _logger;

    public AssetMaintenanceService(
        IGameLibraryService library,
        IDeploymentEngine engine,
        IPackRepository packs,
        IFrameGenDeploymentService frameGen,
        ISmoothMotionDeploymentService smoothMotion,
        IDlss5DeploymentService dlss5,
        ILogger<AssetMaintenanceService> logger)
    {
        _library = library;
        _engine = engine;
        _packs = packs;
        _frameGen = frameGen;
        _smoothMotion = smoothMotion;
        _dlss5 = dlss5;
        _logger = logger;
    }

    public async Task<AssetUpdateReport> DetectAsync(CancellationToken cancellationToken)
    {
        var library = await _library.LoadAsync(cancellationToken);
        var games = library.EnumerateVisibleGames().ToList();
        var packVersions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<AssetUpdateGroup>();

        foreach (var family in Enum.GetValues<AssetFamily>())
        {
            var affected = new List<AssetUpdateGame>();
            string? currentVersion = null;

            foreach (var game in games)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var manifest = await _engine.LoadManifestAsync(
                    FamilyId(family),
                    game.TargetDirectory,
                    cancellationToken);
                if (manifest is null ||
                    manifest.State is DeploymentManifestState.Restored or DeploymentManifestState.RolledBack)
                {
                    continue;
                }

                var packId = string.IsNullOrWhiteSpace(manifest.PackId)
                    ? DefaultPackId(family)
                    : manifest.PackId;
                var version = await GetPackVersionAsync(packId, packVersions, cancellationToken);
                if (version is not null &&
                    string.Equals(version, manifest.PackVersion, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                affected.Add(new AssetUpdateGame(
                    game.Id,
                    game.Name,
                    game.TargetDirectory,
                    game.RenderExePath,
                    manifest.PackVersion,
                    version));
                currentVersion ??= version;
            }

            if (affected.Count > 0)
            {
                groups.Add(new AssetUpdateGroup(
                    family,
                    currentVersion,
                    affected
                        .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()));
            }
        }

        return new AssetUpdateReport(groups);
    }

    public async Task<AssetClearResult> ClearAsync(
        AssetUpdateReport report,
        IProgress<AssetMaintenanceProgress>? progress,
        CancellationToken cancellationToken)
    {
        var failures = new List<AssetClearFailure>();
        var warnings = new List<string>();
        var total = report.TotalGames;
        var current = 0;
        var cleared = 0;

        foreach (var group in report.Groups)
        {
            foreach (var game in group.Games)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new AssetClearResult(cleared, Cancelled: true, failures, warnings);
                }

                current++;
                progress?.Report(new AssetMaintenanceProgress(
                    group.Family,
                    game.Name,
                    current,
                    total,
                    "clearing"));

                AssetClearFailure? failure;
                try
                {
                    failure = group.Family switch
                    {
                        AssetFamily.FrameGen => await ClearFrameGenAsync(game, cancellationToken),
                        AssetFamily.SmoothMotion => await ClearSmoothMotionAsync(game, cancellationToken),
                        AssetFamily.Dlss5 => await ClearDlss5Async(game, cancellationToken),
                        _ => new AssetClearFailure(
                            group.Family,
                            game.GameId,
                            game.Name,
                            game.TargetDirectory,
                            ErrorCode.InternalError,
                            "Unknown asset family.",
                            []),
                    };
                }
                catch (OperationCanceledException)
                {
                    return new AssetClearResult(cleared, Cancelled: true, failures, warnings);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not clear {Family} deployment for {Game}", group.Family, game.Name);
                    failure = new AssetClearFailure(
                        group.Family,
                        game.GameId,
                        game.Name,
                        game.TargetDirectory,
                        exception is UnauthorizedAccessException ? ErrorCode.AccessDenied : ErrorCode.InternalError,
                        exception.Message,
                        []);
                }

                if (failure is null)
                {
                    cleared++;
                }
                else
                {
                    failures.Add(failure);
                }
            }
        }

        progress?.Report(new AssetMaintenanceProgress(
            AssetFamily.FrameGen,
            string.Empty,
            total,
            total,
            "done"));

        return new AssetClearResult(cleared, Cancelled: false, failures, warnings);
    }

    private async Task<AssetClearFailure?> ClearFrameGenAsync(
        AssetUpdateGame game,
        CancellationToken cancellationToken)
    {
        var result = await _frameGen.RestoreAsync(game.TargetDirectory, progress: null, cancellationToken);
        if (result.IsFailure)
        {
            return Failure(game, AssetFamily.FrameGen, result.Error!.Code, result.Error.Diagnostic ?? "restore failed");
        }

        var remaining = RemainingFiles(result.Value);
        if (remaining.Count > 0)
        {
            return Failure(
                game,
                AssetFamily.FrameGen,
                ErrorCode.Conflict,
                "Restore left files or warnings.",
                remaining);
        }

        await DiscardAsync(AssetFamily.FrameGen, game.TargetDirectory, cancellationToken);
        return null;
    }

    private async Task<AssetClearFailure?> ClearSmoothMotionAsync(
        AssetUpdateGame game,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(game.RenderExePath))
        {
            return Failure(
                game,
                AssetFamily.SmoothMotion,
                ErrorCode.FileNotFound,
                "Render executable is unknown; clean this directory manually.");
        }

        var result = await _smoothMotion.RestoreAsync(
            game.Name,
            game.TargetDirectory,
            game.RenderExePath,
            progress: null,
            cancellationToken);
        if (result.IsFailure)
        {
            return Failure(
                game,
                AssetFamily.SmoothMotion,
                result.Error!.Code,
                result.Error.Diagnostic ?? "restore failed");
        }

        var remaining = RemainingFiles(result.Value.FileRestore);
        if (remaining.Count > 0)
        {
            return Failure(
                game,
                AssetFamily.SmoothMotion,
                ErrorCode.Conflict,
                "Restore left files or warnings.",
                remaining);
        }

        await DiscardAsync(AssetFamily.SmoothMotion, game.TargetDirectory, cancellationToken);
        return null;
    }

    private async Task<AssetClearFailure?> ClearDlss5Async(
        AssetUpdateGame game,
        CancellationToken cancellationToken)
    {
        var result = await _dlss5.UninstallAsync(
            game.TargetDirectory,
            game.RenderExePath,
            progress: null,
            cancellationToken);
        if (result.IsFailure)
        {
            return Failure(
                game,
                AssetFamily.Dlss5,
                result.Error!.Code,
                result.Error.Diagnostic ?? "uninstall failed");
        }

        if (!result.Value.IsClean)
        {
            return Failure(
                game,
                AssetFamily.Dlss5,
                ErrorCode.Conflict,
                "Uninstall left files behind.",
                result.Value.Remaining);
        }

        return null;
    }

    private async Task DiscardAsync(
        AssetFamily family,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var result = await _engine.DiscardBackupAsync(FamilyId(family), targetDirectory, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Could not discard {Family} deployment metadata for {Target}: {Code}",
                family,
                targetDirectory,
                result.Error?.Code);
        }
    }

    private static AssetClearFailure Failure(
        AssetUpdateGame game,
        AssetFamily family,
        ErrorCode code,
        string detail,
        IReadOnlyList<string>? remaining = null)
    {
        return new AssetClearFailure(
            family,
            game.GameId,
            game.Name,
            game.TargetDirectory,
            code,
            detail,
            remaining ?? []);
    }

    private static List<string> RemainingFiles(RestoreReport report)
    {
        var remaining = new List<string>(report.Leftovers);
        if (report.Conflicts > 0)
        {
            remaining.Add($"{report.Conflicts} modified file(s) were left in place.");
        }

        remaining.AddRange(report.Warnings);
        return remaining;
    }

    private async Task<string?> GetPackVersionAsync(
        string packId,
        Dictionary<string, string?> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(packId, out var cached))
        {
            return cached;
        }

        try
        {
            var location = await _packs.FindAsync(packId, cancellationToken);
            var version = location?.Pack.Version;
            cache[packId] = version;
            return version;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Could not read asset pack {Pack}", packId);
            cache[packId] = null;
            return null;
        }
    }

    private static string FamilyId(AssetFamily family)
    {
        return family switch
        {
            AssetFamily.FrameGen => FrameGenDeploymentService.FamilyId,
            AssetFamily.SmoothMotion => SmoothMotionDeploymentService.FamilyId,
            AssetFamily.Dlss5 => Dlss5DeploymentService.FamilyId,
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };
    }

    private static string DefaultPackId(AssetFamily family)
    {
        return family switch
        {
            AssetFamily.FrameGen => FrameGenDeploymentService.PackId,
            AssetFamily.SmoothMotion => SmoothMotionDeploymentService.Rtx30PackId,
            AssetFamily.Dlss5 => Dlss5DeploymentService.PackId,
            _ => string.Empty,
        };
    }
}
