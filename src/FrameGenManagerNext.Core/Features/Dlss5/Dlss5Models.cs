using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed record Dlss5Inspection(
    DeploymentState FileState,
    GraphicsApi Api,
    bool ReShadePresent,
    string? ReShadeModule = null,
    bool LegacyRenoDxPresent = false,
    bool HasLeftovers = false);

public sealed record Dlss5Report(
    DeploymentReport FileReport,
    GraphicsApi Api,
    string InstallerOutput);

public sealed record Dlss5RestoreReport(
    RestoreReport FileRestore,
    string Message);

public sealed record Dlss5UninstallReport(
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Remaining,
    string Message)
{
    public bool IsClean => Remaining.Count == 0;
}
