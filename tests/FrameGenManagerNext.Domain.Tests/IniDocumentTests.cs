using FrameGenManagerNext.Domain.Ini;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class IniDocumentTests
{
    private const string Sample = """
        ; header comment
        [General]
        Enabled=1
        Preset=A

        [FrameGeneration]
        Engine=DLSS
        MaxGeneratedFrames=3

        [SuperResolution]
        Mode=Off
        """;

    [Fact]
    public void ParsePreservesCommentsAndReadsValuesCaseInsensitively()
    {
        var document = IniDocument.Parse(Sample);

        document.GetValue("General", "Enabled").ShouldBe("1");
        document.GetValue("general", "preset").ShouldBe("A");
        document.GetValue("FrameGeneration", "Engine").ShouldBe("DLSS");

        var text = document.ToText("\n");
        text.ShouldContain("; header comment");
        text.ShouldContain("[General]");
        text.ShouldContain("Engine=DLSS");
    }

    [Fact]
    public void SetValueUpdatesExistingKeyInPlaceAndKeepsKeyCasing()
    {
        var document = IniDocument.Parse(Sample);

        document.SetValue("General", "enabled", "0");

        document.GetValue("GENERAL", "ENABLED").ShouldBe("0");
        document.ToText("\n").ShouldContain("Enabled=0");
    }

    [Fact]
    public void SetValueAddsMissingKeyBeforeNextSection()
    {
        var document = IniDocument.Parse(Sample);

        document.SetValue("FrameGeneration", "Engine", "XeSS");
        document.SetValue("FrameGeneration", "UseHudless", "Auto");

        var text = document.ToText("\n");
        var addedIndex = text.IndexOf("UseHudless=Auto", StringComparison.Ordinal);
        var nextSectionIndex = text.IndexOf("[SuperResolution]", StringComparison.Ordinal);

        addedIndex.ShouldBeGreaterThan(0);
        nextSectionIndex.ShouldBeGreaterThan(addedIndex);
    }

    [Fact]
    public void SetValueAddsMissingSection()
    {
        var document = IniDocument.Parse(Sample);

        document.SetValue("XeSSFG", "FlowResolution", "Fine");

        var text = document.ToText("\n");
        text.ShouldContain("[XeSSFG]");
        text.ShouldContain("FlowResolution=Fine");
        document.GetValue("XeSSFG", "FlowResolution").ShouldBe("Fine");
    }

    [Fact]
    public void SetValueSupportsSectionlessKeys()
    {
        var document = IniDocument.Parse("GlobalKey=1\n");

        document.SetValue(string.Empty, "GlobalKey", "2");
        document.SetValue(string.Empty, "Other", "3");

        document.GetValue(string.Empty, "GlobalKey").ShouldBe("2");
        document.GetValue(string.Empty, "Other").ShouldBe("3");
        document.ToText("\n").ShouldBe("GlobalKey=2\nOther=3\n");
    }

    [Fact]
    public void RemoveValueRemovesAllOccurrences()
    {
        var document = IniDocument.Parse("[A]\nk=1\nk=2\n[B]\nk=3\n");

        document.RemoveValue("A", "k").ShouldBe(2);

        document.GetValue("A", "k").ShouldBeNull();
        document.GetValue("B", "k").ShouldBe("3");
    }

    [Fact]
    public void DuplicateKeysReturnLastValue()
    {
        var document = IniDocument.Parse("[A]\nk=1\nk=2\n");

        document.GetValue("A", "k").ShouldBe("2");
    }

    [Fact]
    public void MalformedLinesAndInlineCommentsArePreserved()
    {
        var document = IniDocument.Parse("[A]\npath=C:\\Games;x\nnot a pair\n# comment\n");

        document.GetValue("A", "path").ShouldBe("C:\\Games;x");

        var text = document.ToText("\n");
        text.ShouldContain("not a pair");
        text.ShouldContain("# comment");
        text.ShouldContain("path=C:\\Games;x");
    }

    [Fact]
    public void ToTextUsesRequestedLineEndingsAndTrailingNewline()
    {
        var document = IniDocument.Parse("[A]\r\nk=1\r\n");

        document.ToText("\n").ShouldBe("[A]\nk=1\n");
        document.ToText("\r\n").ShouldBe("[A]\r\nk=1\r\n");
    }

    [Fact]
    public void ApplyOverridesSkipsStockValueWhenKeyIsMissing()
    {
        var document = IniDocument.Parse("[A]\nOther=1\n");
        var stock = new[] { new IniSetting("A", "k", "5") };
        var settings = new[] { new IniSetting("A", "k", "5") };

        var changes = IniMerger.ApplyOverrides(document, settings, stock);

        changes.Single().Applied.ShouldBeFalse();
        document.GetValue("A", "k").ShouldBeNull();
        document.ToText("\n").ShouldNotContain("k=5");
    }

    [Fact]
    public void ApplyOverridesAppliesDifferentValue()
    {
        var document = IniDocument.Parse("[A]\nOther=1\n");
        var stock = new[] { new IniSetting("A", "k", "5") };
        var settings = new[] { new IniSetting("A", "k", "6") };

        var changes = IniMerger.ApplyOverrides(document, settings, stock);

        changes.Single().Applied.ShouldBeTrue();
        document.GetValue("A", "k").ShouldBe("6");
    }

    [Fact]
    public void ApplyOverridesRestoresStockValueWhenTemplateDiffers()
    {
        var document = IniDocument.Parse("[A]\nk=9\n");
        var stock = new[] { new IniSetting("A", "k", "5") };
        var settings = new[] { new IniSetting("A", "k", "5") };

        var changes = IniMerger.ApplyOverrides(document, settings, stock);

        changes.Single().Applied.ShouldBeTrue();
        document.GetValue("A", "k").ShouldBe("5");
    }

    [Fact]
    public void ApplyOverridesMatchesStockKeysCaseInsensitively()
    {
        var document = IniDocument.Parse("[a]\nOTHER=1\n");
        var stock = new[] { new IniSetting("A", "K", "5") };
        var settings = new[] { new IniSetting("A", "k", "5") };

        var changes = IniMerger.ApplyOverrides(document, settings, stock);

        changes.Single().Applied.ShouldBeFalse();
    }
}
