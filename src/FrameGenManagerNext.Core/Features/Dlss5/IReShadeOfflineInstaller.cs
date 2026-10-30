using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Core.Features.Dlss5;

/// <summary>
/// Installs the ReShade module from the DLL embedded in the bundled setup
/// executable, without running the setup or downloading anything.
/// </summary>
public interface IReShadeOfflineInstaller
{
    Task<Result<string>> InstallAsync(
        string setupPath,
        string targetDirectory,
        string exePath,
        GraphicsApi api,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken);
}
