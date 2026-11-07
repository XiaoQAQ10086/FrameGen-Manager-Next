using System.IO;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    /// <summary>
    /// Installs REFramework for an RE Engine game when the About-page setting is on.
    /// Returns a stable skip reason when an existing installation or a foreign
    /// dinput8.dll is present, so the UI can explain what was skipped.
    /// </summary>
    private async Task<string?> EnsureReframeworkAsync(
        GameEntry game,
        string targetDirectory,
        IProgress<DeploymentProgress>? progress)
    {
        if (!_settings.Load().AutoInstallReframework ||
            ResolveEngine(game) != GameEngine.ReEngine)
        {
            return null;
        }

        var existing = await _reframework.InspectAsync(targetDirectory, CancellationToken.None);
        var ourManifest =
            existing.IsSuccess && existing.Value.State is
                DeploymentState.Installed or
                DeploymentState.Outdated or
                DeploymentState.Partial or
                DeploymentState.Conflict or
                DeploymentState.InstalledWithoutManifest;
        var revision = File.Exists(Path.Combine(targetDirectory, "reframework_revision.txt"));
        if (ourManifest || revision)
        {
            _logger.LogInformation(
                "REFramework already present; skipping install target={Target}",
                targetDirectory);
            return "reframework";
        }

        // dinput8.dll is a common proxy for other mods too. Without REFramework's
        // revision file or our manifest we cannot claim it is REFramework, but it
        // is still unsafe to overwrite.
        if (File.Exists(Path.Combine(targetDirectory, "dinput8.dll")))
        {
            _logger.LogInformation(
                "Foreign dinput8.dll present; skipping REFramework install target={Target}",
                targetDirectory);
            return "dinput8";
        }

        var result = await _reframework.InstallAsync(
            targetDirectory,
            progress,
            CancellationToken.None);
        if (result.IsFailure)
        {
            throw new BridgeException(
                result.Error!.Code,
                result.Error.Diagnostic ?? "REFramework install failed");
        }

        return null;
    }
}
