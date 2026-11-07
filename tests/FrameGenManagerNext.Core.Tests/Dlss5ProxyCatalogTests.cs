using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Domain.Library;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class Dlss5ProxyCatalogTests
{
    [Theory]
    [InlineData(GraphicsApi.Direct3D9, "d3d9.dll")]
    [InlineData(GraphicsApi.Direct3D10, "dxgi.dll")]
    [InlineData(GraphicsApi.Direct3D11, "dxgi.dll")]
    [InlineData(GraphicsApi.Direct3D12, "dxgi.dll")]
    [InlineData(GraphicsApi.OpenGl, "opengl32.dll")]
    public void AutoModuleMatchesReShadeConvention(GraphicsApi api, string expected)
    {
        Dlss5ProxyCatalog.AutoModule(api).ShouldBe(expected);
    }

    [Fact]
    public void Direct3D9DoesNotAcceptDxgi()
    {
        Dlss5ProxyCatalog.IsCompatible(GraphicsApi.Direct3D9, "dxgi.dll").ShouldBeFalse();
        Dlss5ProxyCatalog.IsCompatible(GraphicsApi.Direct3D9, "d3d9.dll").ShouldBeTrue();
    }

    [Fact]
    public void ExplicitModuleMapsAllSupportedApis()
    {
        Dlss5ProxyCatalog.ExplicitModule(GraphicsApi.Direct3D12).ShouldBe("d3d12.dll");
        Dlss5ProxyCatalog.ExplicitModule(GraphicsApi.Direct3D11).ShouldBe("d3d11.dll");
        Dlss5ProxyCatalog.ArgumentFor("dxgi.dll").ShouldBe("dxgi");
        Dlss5ProxyCatalog.ArgumentFor("opengl32.dll").ShouldBe("opengl");
    }
}
