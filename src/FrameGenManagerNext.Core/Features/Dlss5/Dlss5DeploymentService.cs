using System.Security.Cryptography;
using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Safety;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed partial class Dlss5DeploymentService : IDlss5DeploymentService
{
    public const string FamilyId = "dlss5";
    public const string PackId = "dlss5.reshade-gitc";

    private const string DefaultInstallerArguments = "--headless --api {api}";

    private readonly IPackRepository _packs;
    private readonly IDeploymentEngine _engine;
    private readonly IGraphicsApiProbe _apiProbe;
    private readonly IReShadeOfflineInstaller _reShadeInstaller;
    private readonly IProcessRunner _processRunner;
    private readonly IReShadeConfigService _reShadeConfig;
    private readonly ILogger<Dlss5DeploymentService> _logger;

    public Dlss5DeploymentService(
        IPackRepository packs,
        IDeploymentEngine engine,
        IGraphicsApiProbe apiProbe,
        IReShadeOfflineInstaller reShadeInstaller,
        IProcessRunner processRunner,
        IReShadeConfigService reShadeConfig,
        ILogger<Dlss5DeploymentService> logger)
    {
        _packs = packs;
        _engine = engine;
        _apiProbe = apiProbe;
        _reShadeInstaller = reShadeInstaller;
        _processRunner = processRunner;
        _reShadeConfig = reShadeConfig;
        _logger = logger;
    }

    public async Task<Result<Dlss5Inspection>> InspectAsync(
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken,
        GraphicsApi? knownApi = null)
    {
        var api = knownApi is { } apiValue && apiValue != GraphicsApi.Unknown
            ? Result<GraphicsApi>.Ok(apiValue)
            : await DetectApiAsync(exePath, false, cancellationToken);
        if (api.IsFailure)
        {
            return Result<Dlss5Inspection>.Fail(api.Error!);
        }

        var plan = await BuildPlanAsync(targetDirectory, api.Value, cancellationToken);
        if (plan.IsFailure)
        {
            return Result<Dlss5Inspection>.Fail(plan.Error!);
        }

        var fileState = await _engine.InspectAsync(plan.Value, cancellationToken);
        if (fileState.IsFailure)
        {
            return Result<Dlss5Inspection>.Fail(fileState.Error!);
        }

        var inventory = ScanTarget(plan.Value.TargetDirectory);
        return Result<Dlss5Inspection>.Ok(new Dlss5Inspection(
            fileState.Value.State,
            api.Value,
            inventory.ReShadePresent,
            inventory.ReShadeModule,
            inventory.LegacyRenoDx,
            inventory.HasLeftovers));
    }

    public async Task<Result<Dlss5Report>> InstallAsync(
        string targetDirectory,
        string exePath,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken,
        GraphicsApi? knownApi = null)
    {
        if (!File.Exists(exePath))
        {
            return Result<Dlss5Report>.Fail(new AppError(ErrorCode.InvalidPath, $"executable not found: {exePath}"));
        }

        var api = knownApi is { } apiValue && apiValue != GraphicsApi.Unknown
            ? Result<GraphicsApi>.Ok(apiValue)
            : await DetectApiAsync(exePath, true, cancellationToken);
        if (api.IsFailure)
        {
            return Result<Dlss5Report>.Fail(api.Error!);
        }

        var location = await _packs.FindAsync(PackId, cancellationToken);
        if (location is null)
        {
            return Result<Dlss5Report>.Fail(AppError.Create(ErrorCode.PackNotFound, ("pack", PackId)));
        }

        var schema = AssetPackValidator.ValidateSchema(location.Pack, AppInfo.Version);
        if (!schema.IsValid)
        {
            var diagnostic = string.Join("; ", schema.Errors.Select(error => $"{error.Code} {error.Path}: {error.Message}"));
            return Result<Dlss5Report>.Fail(AppError.Create(
                ErrorCode.PackInvalid,
                ("pack", PackId),
                ("reason", diagnostic)));
        }

        var plan = await BuildPlanAsync(targetDirectory, api.Value, cancellationToken);
        if (plan.IsFailure)
        {
            return Result<Dlss5Report>.Fail(plan.Error!);
        }

        var inventory = ScanTarget(plan.Value.TargetDirectory);
        if (inventory.LegacyRenoDx)
        {
            return Result<Dlss5Report>.Fail(AppError.Create(
                ErrorCode.LegacyInstallPresent,
                ("target", plan.Value.TargetDirectory),
                ("reason", "legacy RenoDX files must be removed before installing the GITC pack")));
        }

        var installer = ResolveInstaller(location);
        if (installer.IsFailure)
        {
            return Result<Dlss5Report>.Fail(installer.Error!);
        }

        var deployment = await _engine.DeployAsync(plan.Value, progress, cancellationToken);
        if (deployment.IsFailure)
        {
            return Result<Dlss5Report>.Fail(deployment.Error!);
        }

        _logger.LogInformation(
            "Installing ReShade module offline from {Installer}",
            installer.Value.InstallerPath);

        // The ReShade setup normally contacts the network to download compatibility
        // data. Extracting the module from the bundled setup avoids that wait entirely.
        var reShade = await _reShadeInstaller.InstallAsync(
            installer.Value.InstallerPath,
            plan.Value.TargetDirectory,
            exePath,
            api.Value,
            progress,
            cancellationToken);

        if (reShade.IsFailure)
        {
            _logger.LogWarning(
                "Offline ReShade install failed: {Code} {Reason}",
                reShade.Error!.Code,
                reShade.Error.Diagnostic);

            progress?.Report(new DeploymentProgress("failed", "ReShade", 0, 0));

            await _engine.RestoreAsync(FamilyId, plan.Value.TargetDirectory, progress: null, CancellationToken.None);
            return Result<Dlss5Report>.Fail(reShade.Error);
        }

        var warnings = new List<string>(deployment.Value.Warnings);
        try
        {
            await _reShadeConfig.MergeSearchPathsAsync(plan.Value.TargetDirectory, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"ReShade.ini search paths could not be merged: {exception.Message}");
            _logger.LogWarning(exception, "Could not merge ReShade.ini search paths");
        }

        return Result<Dlss5Report>.Ok(new Dlss5Report(
            new DeploymentReport(
                deployment.Value.State,
                deployment.Value.FilesWritten,
                deployment.Value.FilesBackedUp,
                warnings),
            api.Value,
            $"ReShade module '{reShade.Value}' was installed from the bundled setup (offline)."));
    }

    public async Task<Result<Dlss5RestoreReport>> RestoreAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var restore = await _engine.RestoreAsync(FamilyId, targetDirectory, progress, cancellationToken);
        return restore.IsFailure
            ? Result<Dlss5RestoreReport>.Fail(restore.Error!)
            : Result<Dlss5RestoreReport>.Ok(new Dlss5RestoreReport(
                restore.Value,
                "DLSS5 pack files were restored. ReShade module/INI/log files are left in place; use the official ReShade uninstaller if they are not needed."));
    }

    private async Task<Result<GraphicsApi>> DetectApiAsync(
        string exePath,
        bool requireKnown,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return Result<GraphicsApi>.Fail(new AppError(ErrorCode.InvalidPath, $"executable not found: {exePath}"));
        }

        var api = await _apiProbe.DetectAsync(exePath, cancellationToken);
        if (requireKnown && api == GraphicsApi.Unknown)
        {
            return Result<GraphicsApi>.Fail(new AppError(
                ErrorCode.InvalidArgument,
                "The graphics API could not be detected; pass a game EXE with a recognizable API."));
        }

        return Result<GraphicsApi>.Ok(api);
    }

    private async Task<Result<DeploymentPlan>> BuildPlanAsync(
        string targetDirectory,
        GraphicsApi api,
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
            if (file.Size is null || info.Length != file.Size.Value || string.IsNullOrWhiteSpace(file.Sha256))
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
                ["api"] = api.ToString(),
            }));
    }

    private static Result<InstallerInfo> ResolveInstaller(PackLocation location)
    {
        var step = location.Pack.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.Type, "runInstaller", StringComparison.OrdinalIgnoreCase));
        if (step?.Arguments is null)
        {
            return Result<InstallerInfo>.Fail(new AppError(ErrorCode.PackInvalid, "pack has no runInstaller step"));
        }

        var arguments = new Dictionary<string, string>(step.Arguments, StringComparer.OrdinalIgnoreCase);
        if (!arguments.TryGetValue("source", out var source) ||
            string.IsNullOrWhiteSpace(source) ||
            !arguments.TryGetValue("sha256", out var expectedHash) ||
            string.IsNullOrWhiteSpace(expectedHash))
        {
            return Result<InstallerInfo>.Fail(new AppError(ErrorCode.PackInvalid, "installer step is incomplete"));
        }

        var path = SafePath.CombineUnderRoot(location.PayloadDirectory, source);
        if (path.IsFailure || !File.Exists(path.Value))
        {
            return Result<InstallerInfo>.Fail(new AppError(ErrorCode.PackInvalid, "installer file is missing"));
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path.Value))).ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            return Result<InstallerInfo>.Fail(new AppError(ErrorCode.PackHashMismatch, "installer hash mismatch"));
        }

        var template = arguments.TryGetValue("arguments", out var custom) && !string.IsNullOrWhiteSpace(custom)
            ? custom
            : DefaultInstallerArguments;

        return Result<InstallerInfo>.Ok(new InstallerInfo(path.Value, template));
    }

    private sealed record InstallerInfo(string InstallerPath, string ArgumentsTemplate);
}
