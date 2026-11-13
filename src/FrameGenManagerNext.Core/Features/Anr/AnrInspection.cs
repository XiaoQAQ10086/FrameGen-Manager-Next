using FrameGenManagerNext.Domain.Deployment;

namespace FrameGenManagerNext.Core.Features.Anr;

public sealed record AnrInspection(
    DeploymentState State,
    string? Version,
    string? Proxy);
