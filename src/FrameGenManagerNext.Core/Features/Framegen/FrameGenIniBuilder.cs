using System.Globalization;
using FrameGenManagerNext.Domain.Ini;
using FrameGenManagerNext.Domain.Packs;

namespace FrameGenManagerNext.Core.Features.Framegen;

/// <summary>
/// Produces dlssg_sm86.ini content from a pack template and user options.
/// Stock values are left implicit; only differing keys are written.
/// </summary>
public static class FrameGenIniBuilder
{
    private static readonly HashSet<string> Presets = new(StringComparer.OrdinalIgnoreCase) { "A", "B", "C" };

    private static readonly HashSet<string> SuperResolutionModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Off",
        "M-INT8",
        "L-INT8",
        "M-FP16",
        "L-FP16",
    };

    private static readonly HashSet<string> XeSsFlow = new(StringComparer.OrdinalIgnoreCase)
    {
        "Coarse",
        "Middle",
        "Fine",
    };

    private static readonly HashSet<string> XeSsHudless = new(StringComparer.OrdinalIgnoreCase)
    {
        "Auto",
        "1",
        "0",
    };

    public static string Build(AssetPack pack, string templateText, FrameGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(templateText);
        ArgumentNullException.ThrowIfNull(options);

        if (pack.Ini is null)
        {
            throw new ArgumentException("The pack does not define an INI template.", nameof(pack));
        }

        Validate(options);

        var engine = Normalize(options.Engine, "DLSS");
        var preset = Normalize(options.Preset, "A");
        var srMode = Normalize(options.SuperResolutionMode, "Off");
        var flow = Normalize(options.XeSsFlowResolution, "Middle");
        var hudless = Normalize(options.XeSsHudless, "Auto");
        var frames = string.Equals(engine, "XeSS", StringComparison.OrdinalIgnoreCase) &&
                     options.MaxGeneratedFrames > 3
            ? 3
            : options.MaxGeneratedFrames;

        var document = IniDocument.Parse(templateText);
        var settings = new List<IniSetting>
        {
            new("DLSSG", "Optimized", options.Optimized.ToString(CultureInfo.InvariantCulture)),
            new("FrameGeneration", "MaxGeneratedFrames", frames.ToString(CultureInfo.InvariantCulture)),
            new("DLSSG", "Preset", preset),
            new("SuperResolution", "Mode", srMode),
            new("FrameGeneration", "Engine", engine),
        };

        if (string.Equals(engine, "XeSS", StringComparison.OrdinalIgnoreCase))
        {
            settings.Add(new IniSetting("XeSSFG", "FlowResolution", flow));
            settings.Add(new IniSetting("XeSSFG", "UseHudless", hudless));
        }

        IniMerger.ApplyOverrides(document, settings, pack.Ini.StockValues);
        return document.ToText("\r\n");
    }

    public static void Validate(FrameGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Optimized is < 0 or > 3)
        {
            throw new ArgumentException("Optimized must be between 0 and 3.", nameof(options));
        }

        if (options.MaxGeneratedFrames is not 1 and not 2 and not 3 and not 5)
        {
            throw new ArgumentException(
                "MaxGeneratedFrames must be 1 (2X), 2 (3X), 3 (4X) or 5 (6X).",
                nameof(options));
        }

        if (!Presets.Contains(options.Preset))
        {
            throw new ArgumentException("Preset must be A, B or C.", nameof(options));
        }

        if (!string.Equals(options.Engine, "DLSS", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.Engine, "XeSS", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Engine must be DLSS or XeSS.", nameof(options));
        }

        if (!SuperResolutionModes.Contains(options.SuperResolutionMode))
        {
            throw new ArgumentException("Unknown SuperResolution mode.", nameof(options));
        }

        if (!XeSsFlow.Contains(options.XeSsFlowResolution))
        {
            throw new ArgumentException("Unknown XeSS flow resolution.", nameof(options));
        }

        if (!XeSsHudless.Contains(options.XeSsHudless))
        {
            throw new ArgumentException("UseHudless must be Auto, 1 or 0.", nameof(options));
        }
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
