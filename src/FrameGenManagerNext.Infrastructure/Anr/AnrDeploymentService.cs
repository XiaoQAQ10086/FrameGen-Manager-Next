using System.Security.Cryptography;
using FrameGenManagerNext.Core.Features.Anr;
using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Safety;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Anr;

public sealed partial class AnrDeploymentService : IAnrDeploymentService
{
    public const string FamilyId = "anr";
    public const string PackId = "anr-fg";

    private static readonly string[] ProxyModules = ["dxgi.dll", "winmm.dll", "version.dll"];

    private readonly IAnrPackageService _packages;
    private readonly IDeploymentEngine _engine;
    private readonly ILogger<AnrDeploymentService> _logger;

    public AnrDeploymentService(
        IAnrPackageService packages,
        IDeploymentEngine engine,
        ILogger<AnrDeploymentService> logger)
    {
        _packages = packages;
        _engine = engine;
        _logger = logger;
    }

    public async Task<AnrInspection> InspectAsync(
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var manifest = await _engine.LoadManifestAsync(FamilyId, targetDirectory, cancellationToken);
        if (manifest is null ||
            manifest.State is DeploymentManifestState.Restored or DeploymentManifestState.RolledBack)
        {
            return new AnrInspection(DeploymentState.NotDeployed, null, null);
        }

        manifest.Options.TryGetValue("proxy", out var proxy);
        return new AnrInspection(manifest.State switch
        {
            DeploymentManifestState.Installed => DeploymentState.Installed,
            DeploymentManifestState.Installing => DeploymentState.Partial,
            _ => DeploymentState.Partial,
        }, manifest.PackVersion, proxy);
    }

    public async Task<Result<DeploymentReport>> DeployAsync(
        string targetDirectory,
        string exePath,
        string? proxyModule,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(exePath))
        {
            return Result<DeploymentReport>.Fail(new AppError(ErrorCode.FileNotFound, exePath));
        }

        var package = await _packages.GetCachedAsync(cancellationToken);
        if (package is null)
        {
            return Result<DeploymentReport>.Fail(new AppError(
                ErrorCode.PackNotFound,
                "download or import ANR before deploying"));
        }

        if (package.Kind == AnrPackageKind.Addon)
        {
            return await DeployAddonAsync(
                package,
                targetDirectory,
                progress,
                cancellationToken);
        }

        if (proxyModule is null ||
            !ProxyModules.Contains(proxyModule, StringComparer.OrdinalIgnoreCase))
        {
            return Result<DeploymentReport>.Fail(new AppError(
                ErrorCode.InvalidArgument,
                "ANR proxy must be dxgi.dll, winmm.dll or version.dll"));
        }

        var existingManifest = await _engine.LoadManifestAsync(FamilyId, targetDirectory, cancellationToken);
        if (existingManifest is null &&
            Directory.Exists(Path.Combine(targetDirectory, "OptiScaler")))
        {
            return Result<DeploymentReport>.Fail(AppError.Create(
                ErrorCode.Conflict,
                ("target", targetDirectory),
                ("reason", "an existing OptiScaler installation was found; use the ANR addon package")));
        }

        var files = await BuildPlanFilesAsync(package.PayloadDirectory, proxyModule, cancellationToken);
        if (files.IsFailure)
        {
            return Result<DeploymentReport>.Fail(files.Error!);
        }

        var plan = new DeploymentPlan(
            FamilyId,
            PackId,
            package.Version,
            targetDirectory,
            files.Value,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["proxy"] = proxyModule.ToLowerInvariant(),
                ["source"] = "anr-fg-full",
            });

        var result = await _engine.DeployAsync(plan, progress, cancellationToken);
        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "anr.deploy target={Target} version={Version} proxy={Proxy}",
                targetDirectory,
                package.Version,
                proxyModule);
        }

        return result;
    }

    public async Task<Result<RestoreReport>> RestoreAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var first = await _engine.RestoreAsync(
            FamilyId,
            targetDirectory,
            progress,
            cancellationToken);
        if (first.IsFailure || first.Value.Leftovers.Count == 0)
        {
            return first;
        }

        // OptiScaler and ANR write logs and a Vulkan loader at runtime. They are
        // not part of the deployment plan, so the engine reports them as leftovers
        // and keeps the manifest in Partial. Remove only files inside ANR-owned
        // directories and run restore again so the deployment reaches Restored.
        var manifest = await _engine.LoadManifestAsync(
            FamilyId,
            targetDirectory,
            cancellationToken);
        var addon = manifest is not null &&
                    manifest.Options.TryGetValue("kind", out var kind) &&
                    string.Equals(kind, "addon", StringComparison.OrdinalIgnoreCase);
        var removed = 0;
        foreach (var leftover in first.Value.Leftovers)
        {
            if (!IsRuntimeArtifact(leftover, addon))
            {
                continue;
            }

            var path = SafePath.CombineUnderRoot(targetDirectory, leftover);
            if (path.IsFailure)
            {
                continue;
            }

            try
            {
                File.Delete(path.Value);
                removed++;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(
                    exception,
                    "Could not remove ANR runtime artifact {File}",
                    leftover);
            }
        }

        if (removed == 0)
        {
            return first;
        }

        _logger.LogInformation(
            "anr.restore.cleanup target={Target} removed={Removed}",
            targetDirectory,
            removed);
        var second = await _engine.RestoreAsync(
            FamilyId,
            targetDirectory,
            progress,
            cancellationToken);
        return second.IsSuccess ? second : first;
    }

    private static bool IsRuntimeArtifact(string relativePath, bool addon)
    {
        var path = relativePath.Replace('\\', '/');
        if (addon)
        {
            return path.StartsWith(
                       "OptiScaler/custom/Apex Neural Rendering FG/",
                       StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(
                       "custom/Apex Neural Rendering FG/",
                       StringComparison.OrdinalIgnoreCase);
        }

        return path.StartsWith("OptiScaler/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Result<List<DeploymentFilePlan>>> BuildPlanFilesAsync(
        string payloadDirectory,
        string proxyModule,
        CancellationToken cancellationToken)
    {
        var gameDirectory = Path.Combine(payloadDirectory, "game");
        if (!Directory.Exists(gameDirectory))
        {
            return Result<List<DeploymentFilePlan>>.Fail(new AppError(
                ErrorCode.PackInvalid,
                "ANR package has no game folder"));
        }

        var files = new List<DeploymentFilePlan>();
        foreach (var source in Directory.EnumerateFiles(gameDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(gameDirectory, source).Replace('\\', '/');
            var target = string.Equals(relative, "dxgi.dll", StringComparison.OrdinalIgnoreCase)
                ? proxyModule.ToLowerInvariant()
                : relative;
            var info = new FileInfo(source);
            var hash = await HashAsync(source, cancellationToken);
            files.Add(new DeploymentFilePlan(
                target,
                source,
                Content: null,
                hash,
                info.Length,
                string.Equals(target, proxyModule, StringComparison.OrdinalIgnoreCase) ? "proxy" : "runtime"));
        }

        if (files.Count == 0)
        {
            return Result<List<DeploymentFilePlan>>.Fail(new AppError(
                ErrorCode.PackInvalid,
                "ANR package has no files"));
        }

        return Result<List<DeploymentFilePlan>>.Ok(files);
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
