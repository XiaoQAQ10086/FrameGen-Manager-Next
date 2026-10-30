using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Safety;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Framegen;

public sealed class FrameGenDeploymentService : IFrameGenDeploymentService
{
    public const string FamilyId = "framegen";
    public const string PackId = "framegen.sm86";

    private readonly IPackRepository _packs;
    private readonly IDeploymentEngine _engine;
    private readonly ILogger<FrameGenDeploymentService> _logger;

    public FrameGenDeploymentService(
        IPackRepository packs,
        IDeploymentEngine engine,
        ILogger<FrameGenDeploymentService> logger)
    {
        _packs = packs;
        _engine = engine;
        _logger = logger;
    }

    public async Task<Result<DeploymentInspection>> InspectAsync(
        string targetDirectory,
        FrameGenOptions options,
        CancellationToken cancellationToken)
    {
        // Inspection must judge the deployment that is actually on disk. Using the
        // dialog defaults would report every non-default deployment as Outdated and
        // would hide a real asset update among false positives.
        var effective = await ReadDeployedOptionsAsync(targetDirectory, options, cancellationToken);
        var plan = await BuildPlanAsync(targetDirectory, effective, verifySources: false, cancellationToken);
        return plan.IsFailure
            ? Result<DeploymentInspection>.Fail(plan.Error!)
            : await _engine.InspectAsync(plan.Value, cancellationToken);
    }

    private async Task<FrameGenOptions> ReadDeployedOptionsAsync(
        string targetDirectory,
        FrameGenOptions fallback,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return fallback;
        }

        string target;
        try
        {
            target = Path.GetFullPath(targetDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return fallback;
        }

        var stored = await _engine.LoadOptionsAsync(FamilyId, target, cancellationToken);
        if (stored is null)
        {
            return fallback;
        }

        var parsed = new FrameGenOptions(
            ReadString(stored, "proxySlot", fallback.ProxySlot),
            ReadInt32(stored, "optimized", fallback.Optimized),
            ReadInt32(stored, "maxGeneratedFrames", fallback.MaxGeneratedFrames),
            ReadString(stored, "preset", fallback.Preset),
            ReadString(stored, "engine", fallback.Engine),
            ReadString(stored, "superResolutionMode", fallback.SuperResolutionMode),
            ReadString(stored, "xessFlowResolution", fallback.XeSsFlowResolution),
            ReadString(stored, "xessHudless", fallback.XeSsHudless));

        try
        {
            FrameGenIniBuilder.Validate(parsed);
            return parsed;
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Stored FrameGen options for {Target} are invalid", target);
            return fallback;
        }
    }

    private static string ReadString(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static int ReadInt32(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback)
    {
        return values.TryGetValue(key, out var value) &&
               int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public async Task<Result<DeploymentReport>> DeployAsync(
        string targetDirectory,
        FrameGenOptions options,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var plan = await BuildPlanAsync(targetDirectory, options, verifySources: true, cancellationToken);
        return plan.IsFailure
            ? Result<DeploymentReport>.Fail(plan.Error!)
            : await _engine.DeployAsync(plan.Value, progress, cancellationToken);
    }

    public Task<Result<RestoreReport>> RestoreAsync(
        string targetDirectory,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        return _engine.RestoreAsync(FamilyId, targetDirectory, progress, cancellationToken);
    }

    private async Task<Result<DeploymentPlan>> BuildPlanAsync(
        string targetDirectory,
        FrameGenOptions options,
        bool verifySources,
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

        try
        {
            FrameGenIniBuilder.Validate(options);
        }
        catch (ArgumentException exception)
        {
            return Result<DeploymentPlan>.Fail(new AppError(ErrorCode.InvalidArgument, exception.Message));
        }

        var location = await _packs.FindAsync(PackId, cancellationToken);
        if (location is null)
        {
            return Result<DeploymentPlan>.Fail(AppError.Create(ErrorCode.PackNotFound, ("pack", PackId)));
        }

        var schema = AssetPackValidator.ValidateSchema(location.Pack, AppInfo.Version);
        if (!schema.IsValid)
        {
            var diagnostic = string.Join("; ", schema.Errors.Select(error => $"{error.Code} {error.Path}: {error.Message}"));
            return Result<DeploymentPlan>.Fail(AppError.Create(
                ErrorCode.PackInvalid,
                ("pack", PackId),
                ("reason", diagnostic)));
        }

        var pack = location.Pack;
        if (pack.Ini is null)
        {
            return Result<DeploymentPlan>.Fail(new AppError(ErrorCode.PackInvalid, "pack has no INI template"));
        }

        var proxy = pack.Files.FirstOrDefault(file =>
            file.ProxySlot &&
            string.Equals(file.Target, options.ProxySlot, StringComparison.OrdinalIgnoreCase));
        if (proxy is null)
        {
            return Result<DeploymentPlan>.Fail(AppError.Create(
                ErrorCode.InvalidArgument,
                ("proxy", options.ProxySlot),
                ("reason", "proxy slot is not provided by the pack")));
        }

        var files = new List<DeploymentFilePlan>();
        foreach (var file in pack.Files)
        {
            if (file.ProxySlot && !ReferenceEquals(file, proxy))
            {
                continue;
            }

            var source = SafePath.CombineUnderRoot(location.PayloadDirectory, file.Source);
            if (source.IsFailure)
            {
                return Result<DeploymentPlan>.Fail(source.Error!);
            }

            if (!File.Exists(source.Value))
            {
                return Result<DeploymentPlan>.Fail(AppError.Create(
                    ErrorCode.PackInvalid,
                    ("pack", PackId),
                    ("file", file.Source),
                    ("reason", "source file is missing")));
            }

            var info = new FileInfo(source.Value);
            if (file.Size is null || info.Length != file.Size.Value)
            {
                return Result<DeploymentPlan>.Fail(AppError.Create(
                    ErrorCode.PackInvalid,
                    ("pack", PackId),
                    ("file", file.Source),
                    ("reason", "source file size does not match the manifest")));
            }

            if (string.IsNullOrWhiteSpace(file.Sha256))
            {
                return Result<DeploymentPlan>.Fail(new AppError(ErrorCode.PackInvalid, "source hash is missing"));
            }

            if (verifySources)
            {
                var actual = await HashFileAsync(source.Value, cancellationToken);
                if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<DeploymentPlan>.Fail(AppError.Create(
                        ErrorCode.PackHashMismatch,
                        ("pack", PackId),
                        ("file", file.Source)));
                }
            }

            files.Add(new DeploymentFilePlan(
                file.Target,
                source.Value,
                Content: null,
                file.Sha256,
                file.Size.Value,
                file.Role));
        }

        var template = SafePath.CombineUnderRoot(location.PayloadDirectory, pack.Ini.Template);
        if (template.IsFailure || !File.Exists(template.Value))
        {
            return Result<DeploymentPlan>.Fail(AppError.Create(
                ErrorCode.PackInvalid,
                ("pack", PackId),
                ("reason", "INI template is missing")));
        }

        if (pack.Ini.TemplateSize is null ||
            new FileInfo(template.Value).Length != pack.Ini.TemplateSize.Value ||
            string.IsNullOrWhiteSpace(pack.Ini.TemplateSha256))
        {
            return Result<DeploymentPlan>.Fail(new AppError(
                ErrorCode.PackInvalid,
                "INI template metadata is incomplete"));
        }

        var templateText = await File.ReadAllTextAsync(template.Value, cancellationToken);
        if (verifySources)
        {
            var actualTemplateHash = await HashFileAsync(template.Value, cancellationToken);
            if (!string.Equals(actualTemplateHash, pack.Ini.TemplateSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Result<DeploymentPlan>.Fail(AppError.Create(
                    ErrorCode.PackHashMismatch,
                    ("pack", PackId),
                    ("file", pack.Ini.Template)));
            }
        }

        string iniText;
        try
        {
            iniText = FrameGenIniBuilder.Build(pack, templateText, options);
        }
        catch (ArgumentException exception)
        {
            return Result<DeploymentPlan>.Fail(new AppError(ErrorCode.InvalidArgument, exception.Message));
        }

        var iniBytes = Encoding.UTF8.GetBytes(iniText);
        var iniHash = Convert.ToHexString(SHA256.HashData(iniBytes)).ToLowerInvariant();
        files.Add(new DeploymentFilePlan(
            pack.Ini.Target,
            SourcePath: null,
            iniBytes,
            iniHash,
            iniBytes.LongLength,
            "ini"));

        var optionValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["proxySlot"] = options.ProxySlot,
            ["optimized"] = options.Optimized.ToString(CultureInfo.InvariantCulture),
            ["maxGeneratedFrames"] = options.MaxGeneratedFrames.ToString(CultureInfo.InvariantCulture),
            ["preset"] = options.Preset,
            ["engine"] = options.Engine,
            ["superResolutionMode"] = options.SuperResolutionMode,
            ["xessFlowResolution"] = options.XeSsFlowResolution,
            ["xessHudless"] = options.XeSsHudless,
        };

        _logger.LogDebug("Built FrameGen deployment plan for {Target} with {Count} files", target, files.Count);
        return Result<DeploymentPlan>.Ok(new DeploymentPlan(
            FamilyId,
            pack.Id,
            pack.Version,
            target,
            files,
            optionValues));
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
}
