using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private static readonly string[] ReShadeProxyNames =
    [
        "d3d9.dll",
        "d3d10.dll",
        "d3d11.dll",
        "d3d12.dll",
        "dxgi.dll",
        "opengl32.dll",
    ];

    private static bool IsReShadeProxy(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return info.ProductName?.Contains("ReShade", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the proxy module used by a DLSS5/ReShade installation in this folder.
    /// Frame generation must not write its own proxy over that slot.
    /// </summary>
    private static string? FindReShadeProxy(string targetDirectory)
    {
        foreach (var name in ReShadeProxyNames)
        {
            var path = Path.Combine(targetDirectory, name);
            if (File.Exists(path) && IsReShadeProxy(path))
            {
                return name;
            }
        }

        return null;
    }

    private async Task<object?> GetFrameGenProxyStatusAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        var target = ResolveDeploymentTarget(game, payload);
        return new
        {
            reShadeProxy = FindReShadeProxy(target.Directory),
        };
    }
}
