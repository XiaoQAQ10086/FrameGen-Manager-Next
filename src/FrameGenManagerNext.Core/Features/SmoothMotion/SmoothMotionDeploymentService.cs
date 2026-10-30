using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Safety;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.SmoothMotion;

public sealed class SmoothMotionDeploymentService : ISmoothMotionDeploymentService
{
    public const string FamilyId = "smooth-motion";
    public const string Rtx30PackId = "smooth-motion.rtx30";
    public const string Rtx20PackId = "smooth-motion.rtx20";

    private static readonly string[] NoManifestWarnings =
    [
        "No component deployment manifest was present.",
    ];

    private readonly IGpuService _gpuService;
    private readonly IPackRepository _packs;
    private readonly IDeploymentEngine _engine;
    private readonly INvApiSmoothMotion _nvapi;
    private readonly ILogger<SmoothMotionDeploymentService> _logger;

    public SmoothMotionDeploymentService(
        IGpuService gpuService,
        IPackRepository packs,
        IDeploymentEngine engine,
        INvApiSmoothMotion nvapi,
        ILogger<SmoothMotionDeploymentService> logger)
    {
        _gpuService = gpuService;
        _packs = packs;
        _engine = engine;
        _nvapi = nvapi;
        _logger = logger;
    }

    public async Task<Result<SmoothMotionInspection>> InspectAsync(
        string game,
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken)
    {
        var route = await ResolveRouteAsync(cancellationToken);
        if (route.IsFailure)
        {
            return Result<SmoothMotionInspection>.Fail(route.Error!);
        }

        var plan = await BuildPlanAsync(route.Value.Route, route.Value.PackId, targetDirectory, cancellationToken);
        if (plan.IsFailure)
        {
            return Result<SmoothMotionInspection>.Fail(plan.Error!);
        }

        var fileState = await _engine.InspectAsync(plan.Value, cancellationToken);
        if (fileState.IsFailure)
        {
            return Result<SmoothMotionInspection>.Fail(fileState.Error!);
        }

        var setting = await _nvapi.GetStateAsync(exePath, cancellationToken);
        return Result<SmoothMotionInspection>.Ok(new SmoothMotionInspection(
            fileState.Value.State,
            route.Value.Route,
            route.Value.PackId,
            setting));
    }

    public async Task<Result<SmoothMotionReport>> EnableAsync(
        string game,
        string targetDirectory,
        string exePath,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var route = await ResolveRouteAsync(cancellationToken);
        if (route.IsFailure)
        {
            return Result<SmoothMotionReport>.Fail(route.Error!);
        }

        var plan = await BuildPlanAsync(route.Value.Route, route.Value.PackId, targetDirectory, cancellationToken);
        if (plan.IsFailure)
        {
            return Result<SmoothMotionReport>.Fail(plan.Error!);
        }

        var deployment = await _engine.DeployAsync(plan.Value, progress, cancellationToken);
        if (deployment.IsFailure)
        {
            return Result<SmoothMotionReport>.Fail(deployment.Error!);
        }

        var setting = await _nvapi.SetAsync(game, targetDirectory, exePath, enabled: true, cancellationToken);
        if (!setting.Ok)
        {
            _logger.LogWarning("Smooth Motion NVAPI write failed; rolling back component files: {Message}", setting.Message);
            await _engine.RestoreAsync(FamilyId, targetDirectory, progress: null, CancellationToken.None);
            return Result<SmoothMotionReport>.Fail(new AppError(
                ErrorCode.NvApiCallFailed,
                setting.Message));
        }

        return Result<SmoothMotionReport>.Ok(new SmoothMotionReport(
            deployment.Value,
            setting,
            "Smooth Motion enabled."));
    }

    public async Task<Result<SmoothMotionChangeResult>> DisableAsync(
        string game,
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken)
    {
        var setting = await _nvapi.SetAsync(game, targetDirectory, exePath, enabled: false, cancellationToken);
        if (!setting.Ok)
        {
            return Result<SmoothMotionChangeResult>.Fail(new AppError(
                ErrorCode.NvApiCallFailed,
                setting.Message));
        }

        // Closing Smooth Motion means the component should leave the game folder too;
        // otherwise the proxy DLL keeps loading even though the driver switch is off.
        // The original driver value stays in the sm-profiles record, so a later
        // Restore still puts the profile back the way it was.
        var files = await _engine.RestoreAsync(
            FamilyId,
            targetDirectory,
            progress: null,
            cancellationToken);
        if (files.IsFailure && files.Error!.Code != ErrorCode.ManifestMissing)
        {
            return Result<SmoothMotionChangeResult>.Fail(AppError.Create(
                files.Error.Code,
                ("game", game),
                ("target", targetDirectory),
                ("reason", files.Error.Diagnostic ?? "component files could not be removed")));
        }

        return Result<SmoothMotionChangeResult>.Ok(setting);
    }

    public async Task<Result<SmoothMotionRestoreReport>> RestoreAsync(
        string game,
        string targetDirectory,
        string exePath,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var setting = await _nvapi.RestoreAsync(game, targetDirectory, exePath, cancellationToken);
        var fileRestore = await _engine.RestoreAsync(FamilyId, targetDirectory, progress, cancellationToken);

        if (!setting.Ok)
        {
            return Result<SmoothMotionRestoreReport>.Fail(new AppError(
                ErrorCode.NvApiCallFailed,
                setting.Message));
        }

        if (fileRestore.IsFailure)
        {
            if (fileRestore.Error!.Code == ErrorCode.ManifestMissing)
            {
                return Result<SmoothMotionRestoreReport>.Ok(new SmoothMotionRestoreReport(
                    new RestoreReport(0, 0, 0, Array.Empty<string>(), NoManifestWarnings),
                    setting,
                    "Smooth Motion settings restored; no component manifest was present."));
            }

            return Result<SmoothMotionRestoreReport>.Fail(fileRestore.Error!);
        }

        return Result<SmoothMotionRestoreReport>.Ok(new SmoothMotionRestoreReport(
            fileRestore.Value,
            setting,
            "Smooth Motion restored."));
    }

    private async Task<Result<(GpuRoute Route, string PackId)>> ResolveRouteAsync(
        CancellationToken cancellationToken)
    {
        var gpu = await _gpuService.GetAsync(cancellationToken);
        var gate = GpuCompatibility.EvaluateSmoothMotion(gpu.Route, gpu.DriverVersion);

        return gate switch
        {
            GpuFeatureGate.Supported => Result<(GpuRoute, string)>.Ok((
                gpu.Route,
                gpu.Route == GpuRoute.Sm75 ? Rtx20PackId : Rtx30PackId)),
            GpuFeatureGate.DriverTooOld => Result<(GpuRoute, string)>.Fail(AppError.Create(
                ErrorCode.DriverTooOld,
                ("route", gpu.Route.ToString()),
                ("driver", gpu.DriverVersion ?? "unknown"))),
            GpuFeatureGate.GpuNotNeeded => Result<(GpuRoute, string)>.Fail(AppError.Create(
                ErrorCode.GpuNotNeeded,
                ("route", gpu.Route.ToString()))),
            GpuFeatureGate.GpuNotSupported => Result<(GpuRoute, string)>.Fail(AppError.Create(
                ErrorCode.GpuUnsupported,
                ("route", gpu.Route.ToString()))),
            _ => Result<(GpuRoute, string)>.Fail(AppError.Create(
                ErrorCode.GpuUnsupported,
                ("route", gpu.Route.ToString()),
                ("driver", gpu.DriverVersion ?? "unknown"),
                ("reason", "Smooth Motion requires a recognized RTX 20/30 GPU and driver 616.64 or newer"))),
        };
    }

    private async Task<Result<DeploymentPlan>> BuildPlanAsync(
        GpuRoute route,
        string packId,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Result<DeploymentPlan>.Fail(new AppError(ErrorCode.InvalidPath, "target directory is empty"));
        }

        string target;
        try
        {
            target = Path.GetFullPath(targetDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<DeploymentPlan>.Fail(new AppError(ErrorCode.InvalidPath, exception.Message));
        }

        if (SafePath.IsProtectedSystemDirectory(target))
        {
            return Result<DeploymentPlan>.Fail(new AppError(ErrorCode.ProtectedPath, target));
        }

        var location = await _packs.FindAsync(packId, cancellationToken);
        if (location is null)
        {
            return Result<DeploymentPlan>.Fail(AppError.Create(ErrorCode.PackNotFound, ("pack", packId)));
        }

        var schema = AssetPackValidator.ValidateSchema(location.Pack, AppInfo.Version);
        if (!schema.IsValid)
        {
            var diagnostic = string.Join(
                "; ",
                schema.Errors.Select(error => $"{error.Code} {error.Path}: {error.Message}"));
            return Result<DeploymentPlan>.Fail(AppError.Create(
                ErrorCode.PackInvalid,
                ("pack", packId),
                ("reason", diagnostic)));
        }

        var files = new List<DeploymentFilePlan>();
        foreach (var file in location.Pack.Files)
        {
            var source = SafePath.CombineUnderRoot(location.PayloadDirectory, file.Source);
            if (source.IsFailure || !File.Exists(source.Value))
            {
                return Result<DeploymentPlan>.Fail(AppError.Create(
                    ErrorCode.PackInvalid,
                    ("pack", packId),
                    ("file", file.Source),
                    ("reason", "source file is missing")));
            }

            var info = new FileInfo(source.Value);
            if (file.Size is null ||
                info.Length != file.Size.Value ||
                string.IsNullOrWhiteSpace(file.Sha256))
            {
                return Result<DeploymentPlan>.Fail(AppError.Create(
                    ErrorCode.PackInvalid,
                    ("pack", packId),
                    ("file", file.Source),
                    ("reason", "source metadata does not match the manifest")));
            }

            files.Add(new DeploymentFilePlan(
                file.Target,
                source.Value,
                Content: null,
                file.Sha256,
                file.Size.Value,
                file.Role));
        }

        return Result<DeploymentPlan>.Ok(new DeploymentPlan(
            FamilyId,
            location.Pack.Id,
            location.Pack.Version,
            target,
            files,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["route"] = route.ToString(),
                ["pack"] = packId,
            }));
    }
}
