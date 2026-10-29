using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Ini;
using FrameGenManagerNext.Domain.Packs;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class FrameGenIniBuilderTests
{
    private const string Template = """
        [DLSSG]
        Optimized=1
        Preset=A

        [FrameGeneration]
        Engine=DLSS
        MaxGeneratedFrames=3

        [SuperResolution]
        Mode=Off

        [XeSSFG]
        FlowResolution=Middle
        UseHudless=Auto
        """;

    private static readonly AssetPack Pack = new()
    {
        SchemaVersion = 1,
        Id = "framegen.sm86",
        Version = "0.4.1",
        DisplayName = "FrameGen SM86",
        GpuRoutes = ["Sm86"],
        Files =
        [
            new AssetPackFile
            {
                Source = "version.dll",
                Target = "version.dll",
                Role = "proxy",
                ProxySlot = true,
                Sha256 = new string('a', 64),
                Size = 1,
            },
        ],
        Ini = new AssetPackIni
        {
            Target = "dlssg_sm86.ini",
            Template = "dlssg_sm86.ini",
            Mode = "merge",
            TemplateSha256 = new string('b', 64),
            TemplateSize = 1,
            StockValues =
            [
                new IniSetting("DLSSG", "Optimized", "1"),
                new IniSetting("DLSSG", "Preset", "A"),
                new IniSetting("FrameGeneration", "Engine", "DLSS"),
                new IniSetting("FrameGeneration", "MaxGeneratedFrames", "3"),
                new IniSetting("SuperResolution", "Mode", "Off"),
                new IniSetting("XeSSFG", "FlowResolution", "Middle"),
                new IniSetting("XeSSFG", "UseHudless", "Auto"),
            ],
        },
    };

    [Fact]
    public void DefaultOptionsKeepStockValues()
    {
        var text = FrameGenIniBuilder.Build(Pack, Template, FrameGenOptions.Default);
        var document = IniDocument.Parse(text);

        document.GetValue("DLSSG", "Optimized").ShouldBe("1");
        document.GetValue("DLSSG", "Preset").ShouldBe("A");
        document.GetValue("FrameGeneration", "MaxGeneratedFrames").ShouldBe("3");
        document.GetValue("SuperResolution", "Mode").ShouldBe("Off");
    }

    [Fact]
    public void NonDefaultOptionsAreWritten()
    {
        var options = new FrameGenOptions(
            Optimized: 3,
            MaxGeneratedFrames: 5,
            Preset: "C",
            SuperResolutionMode: "L-INT8");

        var text = FrameGenIniBuilder.Build(Pack, Template, options);
        var document = IniDocument.Parse(text);

        document.GetValue("DLSSG", "Optimized").ShouldBe("3");
        document.GetValue("FrameGeneration", "MaxGeneratedFrames").ShouldBe("5");
        document.GetValue("DLSSG", "Preset").ShouldBe("C");
        document.GetValue("SuperResolution", "Mode").ShouldBe("L-INT8");
    }

    [Fact]
    public void XeSsOptionsSetEngineAndXeSsKeys()
    {
        var options = new FrameGenOptions(
            Engine: "XeSS",
            XeSsFlowResolution: "Coarse",
            XeSsHudless: "0");

        var text = FrameGenIniBuilder.Build(Pack, Template, options);
        var document = IniDocument.Parse(text);

        document.GetValue("FrameGeneration", "Engine").ShouldBe("XeSS");
        document.GetValue("XeSSFG", "FlowResolution").ShouldBe("Coarse");
        document.GetValue("XeSSFG", "UseHudless").ShouldBe("0");
    }

    [Fact]
    public void XeSsClampsGeneratedFramesToStockMaximum()
    {
        var options = new FrameGenOptions(Engine: "XeSS", MaxGeneratedFrames: 5);

        var text = FrameGenIniBuilder.Build(Pack, Template, options);
        var document = IniDocument.Parse(text);

        document.GetValue("FrameGeneration", "MaxGeneratedFrames").ShouldBe("3");
    }

    [Fact]
    public void InvalidOptionsAreRejected()
    {
        Should.Throw<ArgumentException>(() => FrameGenIniBuilder.Build(
            Pack,
            Template,
            FrameGenOptions.Default with { Optimized = 9 }));

        Should.Throw<ArgumentException>(() => FrameGenIniBuilder.Build(
            Pack,
            Template,
            FrameGenOptions.Default with { MaxGeneratedFrames = 2 }));
    }
}
