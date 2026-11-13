namespace FrameGenManagerNext.Core.Features.Anr;

/// <summary>
/// Full carries OptiScaler and a proxy DLL; addon only adds ANR to an OptiScaler
/// the user already installed.
/// </summary>
public enum AnrPackageKind
{
    Full,
    Addon,
}
