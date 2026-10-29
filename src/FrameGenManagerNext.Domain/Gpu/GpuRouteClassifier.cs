namespace FrameGenManagerNext.Domain.Gpu;

/// <summary>
/// Name-based GPU routing, aligned with the old manager's ordered substring rules.
/// Unknown is fail-open and intentionally preferred over guessing.
/// </summary>
public static class GpuRouteClassifier
{
    private static readonly string[] NoTensorCoreMarkers =
    [
        "GTX 10",
        "GTX 9",
        "GTX 7",
        "GT 10",
        "GT 9",
        "GT 7",
        "GT 6",
        "MX1",
        "MX2",
        "MX3",
        "MX4",
        "MX5",
        "QUADRO",
        "TESLA",
    ];

    public static GpuRoute Classify(string? gpuName)
    {
        if (string.IsNullOrWhiteSpace(gpuName))
        {
            return GpuRoute.Unknown;
        }

        var name = gpuName.Trim().ToUpperInvariant();

        if (IsConsumerRtx40Or50(name))
        {
            return GpuRoute.NotNeeded;
        }

        if (name.Contains("AMD", StringComparison.Ordinal) ||
            name.Contains("RADEON", StringComparison.Ordinal) ||
            name.Contains("INTEL", StringComparison.Ordinal) ||
            name.Contains("ARC A", StringComparison.Ordinal))
        {
            return GpuRoute.Unsupported;
        }

        if (ContainsAdaToken(name))
        {
            return GpuRoute.NotNeeded;
        }

        if (name.Contains("QUADRO RTX", StringComparison.Ordinal))
        {
            return GpuRoute.Sm75;
        }

        if (name.Contains("RTX A", StringComparison.Ordinal))
        {
            return GpuRoute.Sm86;
        }

        if (name.Contains("RTX 30", StringComparison.Ordinal))
        {
            return GpuRoute.Sm86;
        }

        if (name.Contains("GTX 16", StringComparison.Ordinal))
        {
            return GpuRoute.Gtx16;
        }

        if (name.Contains("RTX 2050", StringComparison.Ordinal))
        {
            return GpuRoute.Sm86;
        }

        if (name.Contains("RTX 20", StringComparison.Ordinal))
        {
            return GpuRoute.Sm75;
        }

        if (NoTensorCoreMarkers.Any(marker => name.Contains(marker, StringComparison.Ordinal)))
        {
            return GpuRoute.NoTensorCore;
        }

        // Titan RTX is Turing and can run the SM75 path even though its name omits a model number.
        if (name.Contains("TITAN RTX", StringComparison.Ordinal))
        {
            return GpuRoute.Sm75;
        }

        return GpuRoute.Unknown;
    }

    private static bool ContainsAdaToken(string name)
    {
        return name.Contains(" ADA ", StringComparison.Ordinal) ||
               name.EndsWith(" ADA", StringComparison.Ordinal) ||
               name.Contains(" ADA GENERATION", StringComparison.Ordinal);
    }

    private static bool IsConsumerRtx40Or50(string name)
    {
        foreach (var prefix in new[] { "RTX 40", "RTX 50" })
        {
            var index = 0;
            while ((index = name.IndexOf(prefix, index, StringComparison.Ordinal)) >= 0)
            {
                var after = index + prefix.Length;
                if (after + 1 < name.Length &&
                    name[after] is >= '5' and <= '9' &&
                    name[after + 1] == '0')
                {
                    return true;
                }

                index = after;
            }
        }

        return false;
    }
}
