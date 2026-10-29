using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed record Dlss5Inspection(
    DeploymentState FileState,
    GraphicsApi Api,
    bool ReShadePresent);

public sealed record Dlss5Report(
    DeploymentReport FileReport,
    GraphicsApi Api,
    string InstallerOutput);

public sealed record Dlss5RestoreReport(
    RestoreReport FileRestore,
    string Message);
