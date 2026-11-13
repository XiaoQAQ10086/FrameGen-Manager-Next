using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace FrameGenManagerNext.Core.Features.Dlss5;

/// <summary>
/// Cheap PE/version checks used before a DLSS5 deployment. The GITC pack ships
/// both addon architectures; these checks catch a game directory that already has
/// multiple ReShade proxy modules or a proxy whose architecture cannot load.
/// </summary>
public static class Dlss5EnvironmentProbe
{
    public static bool Is64Bit(string exePath)
    {
        try
        {
            using var stream = File.OpenRead(exePath);
            using var reader = new PEReader(stream);
            return reader.PEHeaders.CoffHeader.Machine is Machine.Amd64 or Machine.Arm64;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return false;
        }
    }

    public static IReadOnlyList<string> FindReShadeModules(string targetDirectory)
    {
        var modules = new List<string>();
        foreach (var name in Dlss5ProxyCatalog.Modules)
        {
            var path = Path.Combine(targetDirectory, name);
            if (File.Exists(path) && IsReShadeModule(path))
            {
                modules.Add(name);
            }
        }

        return modules;
    }

    public static bool IsReShadeModule(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return info.ProductName?.Contains("ReShade", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
