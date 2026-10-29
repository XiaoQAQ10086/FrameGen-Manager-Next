using FrameGenNext.Domain;
using Shouldly;

namespace FrameGenNext.Integration.Tests;

[Trait("Category", "Integration")]
public sealed class ScaffoldTests
{
    [Fact]
    public void ProductMetadataIsAvailable()
    {
        AppInfo.ProductName.ShouldBe("FrameGen Next");
        AppInfo.Version.ShouldNotBeNullOrWhiteSpace();
    }
}
