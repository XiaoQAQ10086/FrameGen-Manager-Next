using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Safety;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Reframework;

public sealed class ReframeworkDeploymentService : IReframeworkDeploymentService
{
    public const string FamilyId = "reframework";
    public const string PackId = "reframework";

    private readonly IPackRepository _packs;
    private readonly IDeploymentEngine _engine;
    private readonly ILogger<ReframeworkDeploymentService> _logger;

    public ReframeworkDeploymentService(
        IPackRepository packs,
        IDeploymentEngine engine,
        ILogger<ReframeworkDeploymentService> logger)
    {
        _packs = packs;
        _engine = engine;
        _logger = logger;
    }

    public async Task<Result<DeploymentInspection>> InspectAsync(
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var plan = await BuildPlanAsync(targetDirectory, cancellationToken);
        return plan.IsFailure
            ? Result<DeploymentInspection>.Fail(plan.Error!)
            : await _engine.InspectAsync(plan.Value, cancellationToken);
    }

    public async Task<Result<DeploymentReport>> InstallAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var plan = await BuildPlanAsync(targetDirectory, cancellationToken);
        if (plan.IsFailure)
        {
            return Result<DeploymentReport>.Fail(plan.Error!);
        }

        _logger.LogInformation("Installing REFramework into {Target}", plan.Value.TargetDirectory);
        return await _engine.DeployAsync(plan.Value, progress, cancellationToken);
    }

    public async Task<Result<RestoreReport>> UninstallAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        return await _engine.RestoreAsync(
            FamilyId,
            targetDirectory,
            progress,
            cancellationToken);
    }

    private async Task<Result<DeploymentPlan>> BuildPlanAsync(
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

        var location = await _packs.FindAsync(PackId, cancellationToken);
        if (location is null)
        {
            return Result<DeploymentPlan>.Fail(AppError.Create(ErrorCode.PackNotFound, ("pack", PackId)));
        }

        var schema = AssetPackValidator.ValidateSchema(location.Pack, AppInfo.Version);
        if (!schema.IsValid)
        {
            var diagnostic = string.Join(
                "; ",
                schema.Errors.Select(error => $"{error.Code} {error.Path}: {error.Message}"));
            return Result<DeploymentPlan>.Fail(AppError.Create(
                ErrorCode.PackInvalid,
                ("pack", PackId),
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
                    ("pack", PackId),
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
                    ("pack", PackId),
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
                ["component"] = "reframework",
            }));
    }
}
