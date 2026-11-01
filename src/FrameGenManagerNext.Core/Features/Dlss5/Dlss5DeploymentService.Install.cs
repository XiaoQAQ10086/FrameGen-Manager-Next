using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed partial class Dlss5DeploymentService
{
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

        var explicitApi = knownApi is { } selectedApi && selectedApi != GraphicsApi.Unknown;
        var api = explicitApi
            ? Result<GraphicsApi>.Ok(knownApi!.Value)
            : await DetectApiAsync(exePath, true, cancellationToken);
        if (api.IsFailure)
        {
            return Result<Dlss5Report>.Fail(api.Error!);
        }

        // Automatic mode follows the upstream analyzer and uses dxgi for DX10/11/12.
        // An explicitly selected API uses its own proxy (DX12 -> d3d12.dll), which
        // lets users avoid an occupied dxgi slot.
        var reShadeArgument = explicitApi ? ApiArgument(api.Value) : AutoApiArgument(api.Value);
        var reShadeModule = ModuleForArgument(reShadeArgument);

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

        // The embedded module in the setup executable is byte-identical to the DLL the
        // official setup writes, so extract it locally and avoid the compatibility
        // download. Vulkan has no offline equivalent; other APIs retry with the
        // official setup if extraction fails.
        Result<string> reShade;
        if (api.Value == GraphicsApi.Vulkan)
        {
            _logger.LogInformation(
                "Running ReShade setup {Installer} for Vulkan",
                installer.Value.InstallerPath);
            reShade = await RunOnlineInstallerAsync(
                installer.Value,
                plan.Value.TargetDirectory,
                exePath,
                reShadeArgument,
                progress,
                cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Extracting ReShade module {Module} from {Installer} for {Api}",
                reShadeModule,
                installer.Value.InstallerPath,
                api.Value);
            reShade = await _reShadeInstaller.InstallAsync(
                installer.Value.InstallerPath,
                plan.Value.TargetDirectory,
                exePath,
                reShadeModule!,
                progress,
                cancellationToken);

            if (reShade.IsFailure)
            {
                _logger.LogWarning(
                    "Embedded ReShade extraction failed; falling back to the official setup: {Reason}",
                    reShade.Error!.Diagnostic);
                reShade = await RunOnlineInstallerAsync(
                    installer.Value,
                    plan.Value.TargetDirectory,
                    exePath,
                    reShadeArgument,
                    progress,
                    cancellationToken);
            }
        }

        if (reShade.IsFailure)
        {
            _logger.LogWarning(
                "ReShade install failed: {Code} {Reason}",
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
            $"ReShade module '{reShade.Value}' was installed."));
    }

    private async Task<Result<string>> RunOnlineInstallerAsync(
        InstallerInfo installer,
        string targetDirectory,
        string exePath,
        string apiArgument,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var arguments = installer.ArgumentsTemplate.Replace(
            "{api}",
            apiArgument,
            StringComparison.OrdinalIgnoreCase);
        arguments = $"{arguments} \"{exePath}\"";

        _logger.LogInformation(
            "Running ReShade setup for {Api} {Installer} with arguments {Arguments}",
            apiArgument,
            installer.InstallerPath,
            arguments);

        progress?.Report(new DeploymentProgress("installer", "ReShade", 0, 0));
        var process = await _processRunner.RunAsync(
            installer.InstallerPath,
            arguments,
            targetDirectory,
            TimeSpan.FromSeconds(240),
            cancellationToken);

        if (process.TimedOut || process.ExitCode != 0)
        {
            _logger.LogWarning(
                "ReShade setup failed. api={Api} exit={Exit} timedOut={TimedOut} stdout={StdOut} stderr={StdErr}",
                apiArgument,
                process.ExitCode,
                process.TimedOut,
                process.StandardOutput,
                process.StandardError);

            return Result<string>.Fail(AppError.Create(
                ErrorCode.InstallerFailed,
                ("api", apiArgument),
                ("exitCode", process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("timedOut", process.TimedOut.ToString()),
                ("stdout", Trim(process.StandardOutput)),
                ("stderr", Trim(process.StandardError))));
        }

        return Result<string>.Ok($"ReShade ({apiArgument})");
    }

    private static string AutoApiArgument(GraphicsApi api)
    {
        return api switch
        {
            GraphicsApi.Direct3D9 => "d3d9",
            GraphicsApi.Direct3D10 or GraphicsApi.Direct3D11 or GraphicsApi.Direct3D12 => "dxgi",
            GraphicsApi.OpenGl => "opengl",
            GraphicsApi.Vulkan => "vulkan",
            _ => "dxgi",
        };
    }

    private static string? ModuleForArgument(string apiArgument)
    {
        return apiArgument switch
        {
            "d3d9" => "d3d9.dll",
            "d3d10" => "d3d10.dll",
            "d3d11" => "d3d11.dll",
            "d3d12" => "d3d12.dll",
            "dxgi" => "dxgi.dll",
            "opengl" => "opengl32.dll",
            _ => null,
        };
    }

    private static string ApiArgument(GraphicsApi api)
    {
        return api switch
        {
            GraphicsApi.Direct3D9 => "d3d9",
            GraphicsApi.Direct3D10 => "d3d10",
            GraphicsApi.Direct3D11 => "d3d11",
            GraphicsApi.Direct3D12 => "d3d12",
            GraphicsApi.OpenGl => "opengl",
            GraphicsApi.Vulkan => "vulkan",
            _ => "d3d12",
        };
    }

    private static string Trim(string value)
    {
        const int maxLength = 2_000;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
