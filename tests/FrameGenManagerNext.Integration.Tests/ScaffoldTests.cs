using FrameGenManagerNext.Domain;
using Shouldly;

namespace FrameGenManagerNext.Integration.Tests;

[Trait("Category", "Integration")]
public sealed class ScaffoldTests
{
    [Fact]
    public void ProductMetadataIsAvailable()
    {
        AppInfo.ProductName.ShouldBe("FrameGen Manager-Next");
        AppInfo.Version.ShouldNotBeNullOrWhiteSpace();
    }
}
