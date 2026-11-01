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
}
