using System.Diagnostics;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed partial class Dlss5DeploymentService
{
    private static TargetInventory ScanTarget(string target)
    {
        var module = FindReShadeModule(target);
        var legacyAddons = FindLegacyAddons(target);
        var leftovers = EnumerateLeftoverNames(target);
        var legacy = legacyAddons.Length > 0 || File.Exists(Path.Combine(target, "nvngx_dlssnr.dll"));

        var extras = KnownExtras.Any(name => File.Exists(Path.Combine(target, name))) ||
                     Directory.Exists(Path.Combine(target, "GITC-Uplift"));

        var ini = File.Exists(Path.Combine(target, "ReShade.ini"));
        var log = File.Exists(Path.Combine(target, "ReShade.log"));
        var preset = File.Exists(Path.Combine(target, "ReShadePreset.ini"));

        return new TargetInventory(
            module,
            ini,
            log,
            preset,
            legacy,
            legacyAddons,
            leftovers,
            extras);
    }

    private static string? FindReShadeModule(string target)
    {
        foreach (var name in ReShadeModules)
        {
            var path = Path.Combine(target, name);
            if (!File.Exists(path))
            {
                continue;
            }

            var text = ReadVersionText(path);
            if (text is not null && text.Contains("ReShade", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    private static string[] FindLegacyAddons(string target)
    {
        try
        {
            return Directory.EnumerateFiles(target, "renodx-dlss5*", SearchOption.TopDirectoryOnly)
                .Where(path =>
                {
                    var extension = Path.GetExtension(path);
                    return extension.Equals(".addon", StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(".addon32", StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(".addon64", StringComparison.OrdinalIgnoreCase);
                })
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .Cast<string>()
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static string[] EnumerateLeftoverNames(string target)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(target, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }

        return files
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Where(name =>
            {
                var extension = Path.GetExtension(name);
                return !extension.Equals(".addon", StringComparison.OrdinalIgnoreCase) &&
                       !extension.Equals(".addon32", StringComparison.OrdinalIgnoreCase) &&
                       !extension.Equals(".addon64", StringComparison.OrdinalIgnoreCase);
            })
            .Where(name => AddonLeftoverPrefixes.Any(prefix =>
                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static string? ReadVersionText(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return string.Join(
                " ",
                new[] { info.ProductName, info.FileDescription, info.CompanyName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static List<string> DescribeRemaining(TargetInventory inventory)
    {
        var remaining = new List<string>();
        if (inventory.ReShadeModule is not null)
        {
            remaining.Add(inventory.ReShadeModule);
        }

        if (inventory.ReShadeIni)
        {
            remaining.Add("ReShade.ini");
        }

        if (inventory.ReShadeLog)
        {
            remaining.Add("ReShade.log");
        }

        if (inventory.ReShadePreset)
        {
            remaining.Add("ReShadePreset.ini");
        }

        remaining.AddRange(inventory.LegacyAddons);
        if (inventory.LegacyRenoDx)
        {
            remaining.Add("nvngx_dlssnr.dll");
        }

        remaining.AddRange(inventory.Leftovers);
        if (inventory.Extras)
        {
            remaining.Add("GITC Uplift files");
        }

        return remaining;
    }

    private sealed record TargetInventory(
        string? ReShadeModule,
        bool ReShadeIni,
        bool ReShadeLog,
        bool ReShadePreset,
        bool LegacyRenoDx,
        IReadOnlyList<string> LegacyAddons,
        IReadOnlyList<string> Leftovers,
        bool Extras)
    {
        public bool ReShadePresent => ReShadeModule is not null || ReShadeIni || ReShadeLog;

        public bool HasLeftovers => LegacyRenoDx || Leftovers.Count > 0 || Extras;

        public bool HasNothing => ReShadeModule is null && !ReShadeIni && !ReShadeLog && !ReShadePreset &&
                                  !HasLeftovers;
    }
}
