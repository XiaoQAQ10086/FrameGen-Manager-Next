using FrameGenManagerNext.Infrastructure.Library;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class ValveKeyValuesTests
{
    [Fact]
    public void ParsesNestedObjectsAndComments()
    {
        var text = """
            "libraryfolders"
            {
                // first library
                "0"
                {
                    "path" "C:\\Program Files (x86)\\Steam"
                }
                "1"
                {
                    "path" "D:\\SteamLibrary"
                }
            }
            """;

        var parsed = ValveKeyValues.Parse(text);
        var paths = parsed.FindStringValues("path").ToList();

        paths.ShouldBe(["C:\\Program Files (x86)\\Steam", "D:\\SteamLibrary"]);
    }

    [Fact]
    public void ParsesEscapedQuotesAndTabs()
    {
        var parsed = ValveKeyValues.Parse("\"root\" { \"value\" \"a\\\"b\\tc\" }");

        parsed.Values["root"].ShouldBeOfType<ValveKeyValues>();
        ((ValveKeyValues)parsed.Values["root"]).Values["value"].ShouldBe("a\"b\tc");
    }

    [Fact]
    public void MalformedInputThrows()
    {
        Should.Throw<FormatException>(() => ValveKeyValues.Parse("\"root\" { \"value\" "));
    }
}
