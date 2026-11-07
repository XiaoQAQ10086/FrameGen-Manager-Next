using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;

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

    private async Task<IReadOnlyList<ProxyOccupancy>> GetOccupiedProxiesAsync(
        string targetDirectory,
        DeploymentInspection? frameGenInspection = null)
    {
        var occupied = new List<ProxyOccupancy>();
        var frameGen = frameGenInspection;
        if (frameGen is null)
        {
            var inspection = await _frameGen.InspectAsync(
                targetDirectory,
                FrameGenOptions.Default,
                CancellationToken.None);
            if (inspection.IsSuccess)
            {
                frameGen = inspection.Value;
            }
        }

        var frameGenDeployed = frameGen is not null && frameGen.State is
            DeploymentState.Installed or
            DeploymentState.Outdated or
            DeploymentState.Partial or
            DeploymentState.Conflict or
            DeploymentState.InstalledWithoutManifest;
        if (frameGenDeployed &&
            frameGen!.Options is not null &&
            frameGen.Options.TryGetValue("proxySlot", out var frameGenProxy))
        {
            occupied.Add(new ProxyOccupancy(frameGenProxy, "FrameGen"));
        }

        var reShadeProxy = FindReShadeProxy(targetDirectory);
        if (reShadeProxy is not null)
        {
            occupied.Add(new ProxyOccupancy(reShadeProxy, "Dlss5"));
        }

        var reframework = await _reframework.InspectAsync(targetDirectory, CancellationToken.None);
        var ourReframework = reframework.IsSuccess && reframework.Value.State is
            DeploymentState.Installed or
            DeploymentState.Outdated or
            DeploymentState.Partial or
            DeploymentState.Conflict or
            DeploymentState.InstalledWithoutManifest;
        var revision = File.Exists(Path.Combine(targetDirectory, "reframework_revision.txt"));
        if (ourReframework || revision)
        {
            occupied.Add(new ProxyOccupancy("dinput8.dll", "Reframework"));
        }
        else if (File.Exists(Path.Combine(targetDirectory, "dinput8.dll")))
        {
            occupied.Add(new ProxyOccupancy("dinput8.dll", "Other"));
        }

        return occupied;
    }

    private async Task<object?> GetFrameGenProxyStatusAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        var target = ResolveDeploymentTarget(game, payload);
        var occupied = await GetOccupiedProxiesAsync(target.Directory);
        if (_settings.Load().AutoInstallReframework &&
            ResolveEngine(game) == GameEngine.ReEngine &&
            occupied.All(item => !item.Module.Equals("dinput8.dll", StringComparison.OrdinalIgnoreCase)))
        {
            // REFramework will be installed on the next deploy.
            occupied = [.. occupied, new ProxyOccupancy("dinput8.dll", "Reframework")];
        }

        return new
        {
            reShadeProxy = FindReShadeProxy(target.Directory),
            reframeworkProxy = occupied
                .FirstOrDefault(item => item.Owner == "Reframework")
                ?.Module,
            occupied = occupied.Select(item => new { module = item.Module, owner = item.Owner }),
        };
    }

    private sealed record ProxyOccupancy(string Module, string Owner);
}
