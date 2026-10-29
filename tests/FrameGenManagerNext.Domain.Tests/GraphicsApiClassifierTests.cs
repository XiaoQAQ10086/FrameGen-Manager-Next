using FrameGenManagerNext.Domain.Library;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class GraphicsApiClassifierTests
{
    [Theory]
    [InlineData("dx12", GraphicsApi.Direct3D12)]
    [InlineData("dx11", GraphicsApi.Direct3D11)]
    [InlineData("dx10", GraphicsApi.Direct3D10)]
    [InlineData("dx9", GraphicsApi.Direct3D9)]
    [InlineData("vulkan", GraphicsApi.Vulkan)]
    [InlineData("opengl", GraphicsApi.OpenGl)]
    public void ClassifiesFromImports(string kind, GraphicsApi expected)
    {
        var imports = kind switch
        {
            "dx12" => new[] { "D3D12.dll" },
            "dx11" => new[] { "d3d11.dll", "DXGI.dll" },
            "dx10" => new[] { "d3d10_1.dll" },
            "dx9" => new[] { "d3d9.dll" },
            "vulkan" => new[] { "vulkan-1.dll" },
            "opengl" => new[] { "OPENGL32.dll" },
            _ => [],
        };

        GraphicsApiClassifier.Classify(new GraphicsApiFacts(imports, [])).ShouldBe(expected);
    }

    [Fact]
    public void Direct3D12WinsOverDirect3D11()
    {
        var facts = new GraphicsApiFacts(["d3d11.dll", "d3d12.dll"], []);

        GraphicsApiClassifier.Classify(facts).ShouldBe(GraphicsApi.Direct3D12);
    }

    [Fact]
    public void VulkanImportWinsOverDirect3D11()
    {
        var facts = new GraphicsApiFacts(["d3d11.dll", "vulkan-1.dll"], []);

        GraphicsApiClassifier.Classify(facts).ShouldBe(GraphicsApi.Vulkan);
    }

    [Fact]
    public void ExeNameEvidenceWinsOverImports()
    {
        var facts = new GraphicsApiFacts(["d3d11.dll"], [], "Game-dx12.exe");

        GraphicsApiClassifier.Classify(facts).ShouldBe(GraphicsApi.Direct3D12);
    }

    [Fact]
    public void DxvkWrapperIsVulkan()
    {
        var facts = new GraphicsApiFacts(["d3d11.dll"], [], "Game.exe", WrapperIsVulkan: true);

        GraphicsApiClassifier.Classify(facts).ShouldBe(GraphicsApi.Vulkan);
    }

    [Fact]
    public void SiblingProxyIsUsedWhenImportsAreInconclusive()
    {
        var facts = new GraphicsApiFacts([], ["d3d12.dll"]);

        GraphicsApiClassifier.Classify(facts).ShouldBe(GraphicsApi.Direct3D12);
    }

    [Fact]
    public void SiblingVulkanIsUsedWhenImportsAreInconclusive()
    {
        var facts = new GraphicsApiFacts([], ["vulkan-1.dll"]);

        GraphicsApiClassifier.Classify(facts).ShouldBe(GraphicsApi.Vulkan);
    }

    [Fact]
    public void SiblingDirect3D11IsNotEnough()
    {
        var facts = new GraphicsApiFacts([], ["d3d11.dll"]);

        GraphicsApiClassifier.Classify(facts).ShouldBe(GraphicsApi.Unknown);
    }

    [Fact]
    public void DxgiAloneIsAmbiguous()
    {
        var facts = new GraphicsApiFacts([], ["dxgi.dll"]);

        GraphicsApiClassifier.Classify(facts).ShouldBe(GraphicsApi.Unknown);
    }

    [Fact]
    public void EmptyFactsAreUnknown()
    {
        GraphicsApiClassifier.Classify(new GraphicsApiFacts([], [])).ShouldBe(GraphicsApi.Unknown);
    }
}
