using FrameGenManagerNext.Infrastructure.Dlss5;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class ReShadeConfigServiceTests
{
    [Fact]
    public async Task EnsureOverlayKeyCreatesInputSection()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fgm-reshade-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var service = new ReShadeConfigService();

            await service.EnsureOverlayKeyAsync(
                directory,
                36,
                TestContext.Current.CancellationToken);

            var text = await File.ReadAllTextAsync(
                Path.Combine(directory, "ReShade.ini"),
                TestContext.Current.CancellationToken);
            text.ShouldContain("[INPUT]");
            text.ShouldContain("KeyOverlay=36,0,0,0");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureOverlayKeyPreservesOtherSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fgm-reshade-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "ReShade.ini");
            await File.WriteAllTextAsync(
                path,
                "[GENERAL]\r\nEffectSearchPaths=test\r\n\r\n[INPUT]\r\nKeyOverlay=45,0,0,0\r\n",
                TestContext.Current.CancellationToken);
            var service = new ReShadeConfigService();

            await service.EnsureOverlayKeyAsync(
                directory,
                36,
                TestContext.Current.CancellationToken);

            var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            text.ShouldContain("EffectSearchPaths=test");
            text.ShouldContain("KeyOverlay=36,0,0,0");
            text.ShouldNotContain("KeyOverlay=45,0,0,0");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
