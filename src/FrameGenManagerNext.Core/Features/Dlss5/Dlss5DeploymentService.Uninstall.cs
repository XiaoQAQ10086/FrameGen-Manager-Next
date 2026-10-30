using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Safety;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed partial class Dlss5DeploymentService
{
    private static readonly string[] ReShadeModules =
    [
        "d3d9.dll",
        "d3d10.dll",
        "d3d11.dll",
        "d3d12.dll",
        "dxgi.dll",
        "opengl32.dll",
    ];

    private static readonly string[] AddonLeftoverPrefixes =
    [
        "reshade.ini.renodx-dlss5",
        "renodx-dlss5-report",
        "renodx-dlss5-crash",
        "renodx-dlss5-normtrace",
        "renodx-dlss5-edittrace",
        "renodx-dlss5-",
    ];

    private static readonly string[] KnownExtras =
    [
        "gitc-uplift.addon32",
        "gitc-uplift.addon64",
        "gitc-uplift-helper64.exe",
    ];

    public async Task<Result<Dlss5UninstallReport>> UninstallAsync(
        string targetDirectory,
        string? exePath,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Result<Dlss5UninstallReport>.Fail(new AppError(ErrorCode.InvalidPath, "target directory is empty"));
        }

        string target;
        try
        {
            target = Path.GetFullPath(targetDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<Dlss5UninstallReport>.Fail(new AppError(ErrorCode.InvalidPath, exception.Message));
        }

        if (!Directory.Exists(target))
        {
            return Result<Dlss5UninstallReport>.Fail(new AppError(ErrorCode.InvalidPath, target));
        }

        if (SafePath.IsProtectedSystemDirectory(target))
        {
            return Result<Dlss5UninstallReport>.Fail(new AppError(ErrorCode.ProtectedPath, target));
        }

        var inventory = ScanTarget(target);
        var removed = new List<string>();
        var notes = new List<string>();

        var restore = await _engine.RestoreAsync(FamilyId, target, progress, cancellationToken);
        if (restore.IsFailure && restore.Error!.Code != ErrorCode.ManifestMissing)
        {
            return Result<Dlss5UninstallReport>.Fail(restore.Error);
        }

        if (restore.IsSuccess)
        {
            notes.Add($"Restored {restore.Value.RestoredFiles} file(s) and removed {restore.Value.RemovedFiles} deployed file(s).");
            if (restore.Value.Conflicts > 0)
            {
                notes.Add($"{restore.Value.Conflicts} modified file(s) were left in place.");
            }
        }

        if (inventory.ReShadeModule is not null && !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
        {
            await RunOfficialUninstallerAsync(target, exePath, inventory.ReShadeModule, notes, progress, cancellationToken);
        }
        else if (inventory.ReShadeModule is not null)
        {
            notes.Add("The game executable was not available, so ReShade was removed by file deletion.");
        }

        progress?.Report(new DeploymentProgress("uninstalling", "ReShade files", 0, 1));
        DeleteCoreFiles(target, inventory, removed, notes);
        DeleteGatheringFiles(target, removed, notes);
        DeleteKnownExtras(target, removed, notes);
        DeleteLegacyFiles(target, inventory, removed, notes);
        DeleteKnownShaders(target, removed, notes);
        RemoveD3D12Agility(target, removed, notes);
        PruneEmptyDirectories(target, removed);

        var remaining = DescribeRemaining(ScanTarget(target));
        if (remaining.Count == 0)
        {
            var discard = await _engine.DiscardBackupAsync(FamilyId, target, cancellationToken);
            if (discard.IsSuccess)
            {
                notes.Add("Deployment manifest and backups were removed.");
            }
            else
            {
                notes.Add($"Could not remove deployment metadata: {discard.Error!.Code}");
            }
        }
        else
        {
            notes.Add("Some files remain; deployment metadata was kept so the cleanup can be retried.");
        }

        progress?.Report(new DeploymentProgress("done", string.Empty, 1, 1));

        if (removed.Count == 0 && remaining.Count == 0 && inventory.HasNothing)
        {
            return Result<Dlss5UninstallReport>.Ok(new Dlss5UninstallReport(
                Array.Empty<string>(),
                Array.Empty<string>(),
                "No ReShade or DLSS5 deployment was detected in this directory."));
        }

        var message = remaining.Count == 0
            ? "ReShade and DLSS5 files were removed from this directory. Deletions are permanent and do not use the Recycle Bin."
            : "Some files could not be removed. Close the game and try again.";
        if (notes.Count > 0)
        {
            message = $"{message}\n{string.Join("\n", notes)}";
        }

        return Result<Dlss5UninstallReport>.Ok(new Dlss5UninstallReport(
            removed,
            remaining,
            message));
    }

    private async Task RunOfficialUninstallerAsync(
        string target,
        string exePath,
        string module,
        List<string> notes,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var api = ApiFromModule(module);
        if (api is null)
        {
            return;
        }

        var location = await _packs.FindAsync(PackId, cancellationToken);
        if (location is null)
        {
            notes.Add("The DLSS5 pack is not available; falling back to direct deletion.");
            return;
        }

        var installer = ResolveInstaller(location);
        if (installer.IsFailure)
        {
            notes.Add("The ReShade installer could not be verified; falling back to direct deletion.");
            return;
        }

        var arguments = installer.Value.ArgumentsTemplate.Replace(
            "{api}",
            api,
            StringComparison.OrdinalIgnoreCase);
        arguments = $"{arguments} --state uninstall \"{exePath}\"";

        progress?.Report(new DeploymentProgress("uninstalling", "ReShade uninstaller", 0, 1));
        ProcessResult process;
        try
        {
            process = await _processRunner.RunAsync(
                installer.Value.InstallerPath,
                arguments,
                target,
                TimeSpan.FromSeconds(240),
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(exception, "Could not start the ReShade uninstaller");
            notes.Add("The ReShade uninstaller could not be started; falling back to direct deletion.");
            return;
        }

        if (process.TimedOut || process.ExitCode != 0)
        {
            _logger.LogWarning(
                "ReShade uninstaller failed. exit={Exit} timedOut={TimedOut} stdout={StdOut} stderr={StdErr}",
                process.ExitCode,
                process.TimedOut,
                process.StandardOutput,
                process.StandardError);
            notes.Add("The ReShade uninstaller reported an error; remaining files were deleted directly.");
            return;
        }

        notes.Add("The official ReShade uninstaller removed the ReShade module and its INI files.");
    }

    private static void DeleteCoreFiles(
        string target,
        TargetInventory inventory,
        List<string> removed,
        List<string> notes)
    {
        if (inventory.ReShadeModule is not null)
        {
            TryDeleteFile(Path.Combine(target, inventory.ReShadeModule), removed, notes);
        }

        foreach (var name in new[] { "ReShade.ini", "ReShade.log", "ReShadePreset.ini" })
        {
            TryDeleteFile(Path.Combine(target, name), removed, notes);
        }
    }

    private static void DeleteGatheringFiles(string target, List<string> removed, List<string> notes)
    {
        foreach (var name in EnumerateLeftoverNames(target))
        {
            TryDeleteFile(Path.Combine(target, name), removed, notes);
        }
    }

    private static void DeleteKnownExtras(string target, List<string> removed, List<string> notes)
    {
        foreach (var name in KnownExtras)
        {
            TryDeleteFile(Path.Combine(target, name), removed, notes);
        }

        var modelDirectory = Path.Combine(target, "GITC-Uplift");
        if (Directory.Exists(modelDirectory))
        {
            TryDeleteDirectory(modelDirectory, "GITC-Uplift", removed, notes);
        }
    }

    private static void DeleteLegacyFiles(
        string target,
        TargetInventory inventory,
        List<string> removed,
        List<string> notes)
    {
        if (!inventory.LegacyRenoDx)
        {
            return;
        }

        TryDeleteFile(Path.Combine(target, "nvngx_dlssnr.dll"), removed, notes);
        foreach (var name in inventory.LegacyAddons)
        {
            TryDeleteFile(Path.Combine(target, name), removed, notes);
        }
    }

    private static void DeleteKnownShaders(string target, List<string> removed, List<string> notes)
    {
        var shaders = Path.Combine(target, "reshade-shaders", "Shaders");
        foreach (var name in new[] { "Uplift.fx", "UpliftMask.fx" })
        {
            TryDeleteFile(Path.Combine(shaders, name), removed, notes);
            TryDeleteFile(Path.Combine(shaders, "GITC-Uplift", name), removed, notes);
        }

        var gitcDirectory = Path.Combine(shaders, "GITC-Uplift");
        if (Directory.Exists(gitcDirectory) &&
            !Directory.EnumerateFileSystemEntries(gitcDirectory).Any())
        {
            TryDeleteDirectory(gitcDirectory, "reshade-shaders\\Shaders\\GITC-Uplift", removed, notes);
        }
    }

    private static void RemoveD3D12Agility(string target, List<string> removed, List<string> notes)
    {
        var directory = Path.Combine(target, "D3D12");
        if (!Directory.Exists(directory))
        {
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            notes.Add($"Could not inspect D3D12\\: {exception.Message}");
            return;
        }

        if (files.Length != 1 ||
            !string.Equals(Path.GetFileName(files[0]), "D3D12Core.dll", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var version = ReadVersionText(files[0]);
        if (version is null || !version.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TryDeleteFile(files[0], removed, notes);
        TryDeleteDirectory(directory, "D3D12", removed, notes);
    }

    private static void PruneEmptyDirectories(string target, List<string> removed)
    {
        var shaders = Path.Combine(target, "reshade-shaders");
        if (!Directory.Exists(shaders))
        {
            return;
        }

        DeleteEmptyChildren(shaders);
        if (!Directory.EnumerateFileSystemEntries(shaders).Any())
        {
            try
            {
                Directory.Delete(shaders);
                removed.Add("reshade-shaders\\");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A locked empty directory is harmless.
            }
        }
    }

    private static void DeleteEmptyChildren(string directory)
    {
        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            DeleteEmptyChildren(child);
            try
            {
                if (!Directory.EnumerateFileSystemEntries(child).Any())
                {
                    Directory.Delete(child);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Leave locked or non-empty directories alone.
            }
        }
    }

    private static void TryDeleteFile(string path, List<string> removed, List<string> notes)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
            removed.Add(Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            notes.Add($"Could not delete {Path.GetFileName(path)}: {exception.Message}");
        }
    }

    private static void TryDeleteDirectory(string path, string label, List<string> removed, List<string> notes)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            removed.Add($"{label}\\");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            notes.Add($"Could not delete {label}: {exception.Message}");
        }
    }
}
