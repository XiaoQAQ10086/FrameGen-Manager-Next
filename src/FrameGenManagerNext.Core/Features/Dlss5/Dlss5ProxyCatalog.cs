using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Dlss5;

/// <summary>
/// ReShade's loader entry points. Auto mode uses dxgi for DX10/11/12 because that
/// is what ReShade's own analyzer recommends; DX9 must use d3d9.dll.
/// </summary>
public static class Dlss5ProxyCatalog
{
    public static readonly IReadOnlyList<string> Modules =
    [
        "dxgi.dll",
        "d3d12.dll",
        "d3d11.dll",
        "d3d10.dll",
        "d3d9.dll",
        "opengl32.dll",
    ];

    public static string? AutoModule(GraphicsApi api)
    {
        return api switch
        {
            GraphicsApi.Direct3D9 => "d3d9.dll",
            GraphicsApi.Direct3D10 or GraphicsApi.Direct3D11 or GraphicsApi.Direct3D12 => "dxgi.dll",
            GraphicsApi.OpenGl => "opengl32.dll",
            _ => null,
        };
    }

    public static string? ExplicitModule(GraphicsApi api)
    {
        return api switch
        {
            GraphicsApi.Direct3D9 => "d3d9.dll",
            GraphicsApi.Direct3D10 => "d3d10.dll",
            GraphicsApi.Direct3D11 => "d3d11.dll",
            GraphicsApi.Direct3D12 => "d3d12.dll",
            GraphicsApi.OpenGl => "opengl32.dll",
            _ => null,
        };
    }

    public static string? ArgumentFor(string? module)
    {
        return module?.ToLowerInvariant() switch
        {
            "dxgi.dll" => "dxgi",
            "d3d12.dll" => "d3d12",
            "d3d11.dll" => "d3d11",
            "d3d10.dll" => "d3d10",
            "d3d9.dll" => "d3d9",
            "opengl32.dll" => "opengl",
            _ => null,
        };
    }

    public static bool IsCompatible(GraphicsApi api, string module)
    {
        return api switch
        {
            GraphicsApi.Direct3D9 => module.Equals("d3d9.dll", StringComparison.OrdinalIgnoreCase),
            GraphicsApi.Direct3D10 => module.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase) ||
                                      module.Equals("d3d10.dll", StringComparison.OrdinalIgnoreCase),
            GraphicsApi.Direct3D11 => module.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase) ||
                                      module.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase),
            GraphicsApi.Direct3D12 => module.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase) ||
                                      module.Equals("d3d12.dll", StringComparison.OrdinalIgnoreCase),
            GraphicsApi.OpenGl => module.Equals("opengl32.dll", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
