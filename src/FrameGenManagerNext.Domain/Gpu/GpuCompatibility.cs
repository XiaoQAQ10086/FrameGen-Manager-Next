namespace FrameGenManagerNext.Domain.Gpu;

/// <summary>
/// Feature gates shared by the UI and the deployment engine.
/// Frame generation requires driver R580 or newer; Smooth Motion requires 616.64 or newer.
/// </summary>
public static class GpuCompatibility
{
    public const int MinimumFrameGenerationDriver = 580;
    public static readonly Version MinimumSmoothMotionDriver = new(616, 64);

    public static GpuFeatureGate EvaluateFrameGeneration(GpuRoute route, string? driverVersion)
    {
        return route switch
        {
            GpuRoute.Sm86 or GpuRoute.Sm75 => EvaluateDriver(driverVersion, MinimumFrameGenerationDriver, 0),
            GpuRoute.NotNeeded => GpuFeatureGate.GpuNotNeeded,
            GpuRoute.Gtx16 or GpuRoute.NoTensorCore or GpuRoute.Unsupported => GpuFeatureGate.GpuNotSupported,
            _ => GpuFeatureGate.Unknown,
        };
    }

    public static GpuFeatureGate EvaluateSmoothMotion(GpuRoute route, string? driverVersion)
    {
        return route switch
        {
            GpuRoute.Sm86 or GpuRoute.Sm75 => EvaluateDriverVersion(driverVersion, MinimumSmoothMotionDriver),
            GpuRoute.NotNeeded => GpuFeatureGate.GpuNotNeeded,
            GpuRoute.Gtx16 or GpuRoute.NoTensorCore or GpuRoute.Unsupported => GpuFeatureGate.GpuNotSupported,
            _ => GpuFeatureGate.Unknown,
        };
    }

    public static bool TryParseDriverVersion(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var head = value.Trim().Split(' ')[0];
        if (Version.TryParse(head, out var parsed))
        {
            version = parsed;
            return true;
        }

        return false;
    }

    private static GpuFeatureGate EvaluateDriver(string? driverVersion, int major, int minor)
    {
        if (!TryParseDriverVersion(driverVersion, out var version))
        {
            return GpuFeatureGate.Unknown;
        }

        return version >= new Version(major, minor)
            ? GpuFeatureGate.Supported
            : GpuFeatureGate.DriverTooOld;
    }

    private static GpuFeatureGate EvaluateDriverVersion(string? driverVersion, Version minimum)
    {
        if (!TryParseDriverVersion(driverVersion, out var version))
        {
            return GpuFeatureGate.Unknown;
        }

        return version >= minimum
            ? GpuFeatureGate.Supported
            : GpuFeatureGate.DriverTooOld;
    }
}
