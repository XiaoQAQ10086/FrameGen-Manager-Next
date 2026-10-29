using Shouldly;

namespace FrameGenNext.Domain.Tests;

public sealed class AppInfoTests
{
    [Fact]
    public void VersionIsSemVerLike()
    {
        AppInfo.Version.ShouldNotBeNullOrWhiteSpace();

        var parts = AppInfo.Version.Split('.');
        parts.Length.ShouldBeGreaterThanOrEqualTo(2);
        int.TryParse(parts[0], out _).ShouldBeTrue();
    }

    [Fact]
    public void ProductIdentityIsStable()
    {
        AppInfo.ProductName.ShouldBe("FrameGen Next");
        AppInfo.DataDirectoryName.ShouldBe("FrameGenNext");
        AppInfo.UpdateFeedId.ShouldBe("FrameGenNext");
    }
}
