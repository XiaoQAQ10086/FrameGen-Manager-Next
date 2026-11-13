using System.Security.Cryptography;
using FrameGenManagerNext.Core.Features.Anr;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Anr;

public sealed partial class AnrDeploymentService
{
    private const string AddonFolderName = "Apex Neural Rendering FG";

    private async Task<Result<DeploymentReport>> DeployAddonAsync(
        AnrPackageInfo package,
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var iniPath = Path.Combine(targetDirectory, "OptiScaler.ini");
        if (!File.Exists(iniPath))
        {
            return Result<DeploymentReport>.Fail(new AppError(
                ErrorCode.FileNotFound,
                "OptiScaler.ini was not found; install OptiScaler first or use the full package"));
        }

        var originalIni = await File.ReadAllBytesAsync(iniPath, cancellationToken);
        var main = OptiScalerIni.ResolveMainDirectory(targetDirectory, originalIni);
        if (main.IsFailure)
        {
            return Result<DeploymentReport>.Fail(main.Error!);
        }

        var destination = Path.Combine(main.Value, "custom", AddonFolderName);
        var existingManifest = await _engine.LoadManifestAsync(FamilyId, targetDirectory, cancellationToken);
        if (existingManifest is null &&
            Directory.Exists(destination) &&
            Directory.EnumerateFileSystemEntries(destination).Any())
        {
            return Result<DeploymentReport>.Fail(AppError.Create(
                ErrorCode.Conflict,
                ("target", destination),
                ("reason", "an existing Apex Neural Rendering FG folder was found")));
        }

        var addonSource = Path.Combine(package.PayloadDirectory, AddonFolderName);
        if (!Directory.Exists(addonSource))
        {
            return Result<DeploymentReport>.Fail(new AppError(
                ErrorCode.PackInvalid,
                "ANR addon package has no Apex Neural Rendering FG folder"));
        }

        var destinationRelative = Path.GetRelativePath(targetDirectory, destination)
            .Replace('\\', '/');
        var files = new List<DeploymentFilePlan>();
        foreach (var source in Directory.EnumerateFiles(addonSource, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(addonSource, source).Replace('\\', '/');
            var info = new FileInfo(source);
            files.Add(new DeploymentFilePlan(
                $"{destinationRelative}/{relative}",
                source,
                Content: null,
                await HashAsync(source, cancellationToken),
                info.Length,
                "runtime"));
        }

        var updatedIni = OptiScalerIni.ApplyFrameGeneration(originalIni);
        if (updatedIni.IsFailure)
        {
            return Result<DeploymentReport>.Fail(updatedIni.Error!);
        }

        files.Add(new DeploymentFilePlan(
            "OptiScaler.ini",
            SourcePath: null,
            updatedIni.Value,
            Convert.ToHexString(SHA256.HashData(updatedIni.Value)).ToLowerInvariant(),
            updatedIni.Value.Length,
            "config"));

        var plan = new DeploymentPlan(
            FamilyId,
            PackId,
            package.Version,
            targetDirectory,
            files,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kind"] = "addon",
                ["source"] = "anr-fg-addon",
            });

        var result = await _engine.DeployAsync(plan, progress, cancellationToken);
        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "anr.deploy.addon target={Target} version={Version}",
                targetDirectory,
                package.Version);
        }

        return result;
    }
}
