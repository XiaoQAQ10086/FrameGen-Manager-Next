namespace FrameGenManagerNext.Core.Features.Anr;

public sealed record AnrPackageInfo(
    string Version,
    string PayloadDirectory,
    string ZipPath,
    AnrPackageKind Kind = AnrPackageKind.Full);
