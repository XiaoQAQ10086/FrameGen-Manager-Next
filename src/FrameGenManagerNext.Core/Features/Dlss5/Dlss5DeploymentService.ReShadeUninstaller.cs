using System.Security.Cryptography;
using FrameGenManagerNext.Domain.Deployment;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed partial class Dlss5DeploymentService
{
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
            notes.Add("No ReShade API matches the installed module; direct deletion is used.");
            return;
        }

        var location = await _packs.FindAsync(PackId, cancellationToken);
        if (location is null)
        {
            notes.Add("The DLSS5 pack is not available; direct deletion is used.");
            return;
        }

        var installer = ResolveInstaller(location);
        if (installer.IsFailure)
        {
            notes.Add("The ReShade installer could not be verified; direct deletion is used.");
            return;
        }

        var arguments = installer.Value.ArgumentsTemplate.Replace(
            "{api}",
            api,
            StringComparison.OrdinalIgnoreCase);
        arguments = $"{arguments} --state uninstall \"{exePath}\"";

        progress?.Report(new DeploymentProgress("uninstaller", "ReShade", 0, 0));
        try
        {
            var process = await _processRunner.RunAsync(
                installer.Value.InstallerPath,
                arguments,
                target,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (process.TimedOut || process.ExitCode != 0)
            {
                notes.Add("The official ReShade uninstaller did not finish; remaining files are deleted directly.");
                return;
            }

            notes.Add("The official ReShade uninstaller removed the ReShade module.");
        }
        catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(exception, "Could not start the ReShade uninstaller");
            notes.Add("The official ReShade uninstaller could not be started; direct deletion is used.");
        }
    }

    private async Task<string?> TryGetExpectedReShadeModuleHashAsync(
        string? exePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        var location = await _packs.FindAsync(PackId, cancellationToken);
        if (location is null)
        {
            return null;
        }

        var installer = ResolveInstaller(location);
        if (installer.IsFailure)
        {
            return null;
        }

        var module = await _reShadeInstaller.ExtractAsync(
            installer.Value.InstallerPath,
            exePath,
            cancellationToken);
        return module.IsSuccess
            ? Convert.ToHexString(SHA256.HashData(module.Value)).ToLowerInvariant()
            : null;
    }

    private static async Task<string?> FindOwnedModuleAsync(
        string target,
        string? expectedHash,
        CancellationToken cancellationToken)
    {
        if (expectedHash is null)
        {
            return null;
        }

        foreach (var name in ReShadeModules)
        {
            var path = Path.Combine(target, name);
            if (!File.Exists(path))
            {
                continue;
            }

            var description = ReadVersionText(path);
            if (description is null ||
                !description.Contains("ReShade", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hash = await TryHashAsync(path, cancellationToken);
            if (hash is not null &&
                string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    private static async Task<byte[]?> TryReadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task RestoreFileAsync(
        string path,
        byte[]? backup,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        if (backup is null || File.Exists(path))
        {
            return;
        }

        try
        {
            await File.WriteAllBytesAsync(path, backup, cancellationToken);
            notes.Add($"Restored {Path.GetFileName(path)} after the ReShade uninstaller removed it.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            notes.Add($"Could not restore {Path.GetFileName(path)}: {exception.Message}");
        }
    }
}
