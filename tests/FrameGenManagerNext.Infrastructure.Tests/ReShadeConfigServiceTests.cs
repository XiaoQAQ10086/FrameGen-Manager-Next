using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Infrastructure.Dlss5;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class ReShadeConfigServiceTests
{
    [Fact]
    public async Task EnsureOverlayKeyCreatesInputSection()
    {
        var directory = CreateDirectory();
        try
        {
            var service = CreateService(directory, running: false);

            await service.EnsureOverlayKeyAsync(
                directory,
                null,
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
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "ReShade.ini");
            await File.WriteAllTextAsync(
                path,
                "[GENERAL]\r\nEffectSearchPaths=test\r\n\r\n[INPUT]\r\nKeyOverlay=45,0,0,0\r\n",
                TestContext.Current.CancellationToken);
            var service = CreateService(directory, running: false);

            await service.EnsureOverlayKeyAsync(
                directory,
                null,
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

    [Fact]
    public async Task RunningGameQueuesHotkeyUntilFlush()
    {
        var directory = CreateDirectory();
        try
        {
            var paths = new AppPaths(directory);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "ReShade.ini"),
                $"[INPUT]{Environment.NewLine}KeyOverlay=45,0,0,0{Environment.NewLine}",
                TestContext.Current.CancellationToken);
            var running = new ReShadeConfigService(
                new HotkeyQueueStore(paths),
                new StubProbe(isRunning: true),
                NullLogger<ReShadeConfigService>.Instance);

            var queued = await running.EnsureOverlayKeyAsync(
                directory,
                Path.Combine(directory, "game.exe"),
                36,
                TestContext.Current.CancellationToken);

            queued.Queued.ShouldBeTrue();
            queued.Applied.ShouldBeFalse();
            File.Exists(Path.Combine(paths.StateDirectory, "hotkey-queue.json")).ShouldBeTrue();

            var idle = new ReShadeConfigService(
                new HotkeyQueueStore(paths),
                new StubProbe(isRunning: false),
                NullLogger<ReShadeConfigService>.Instance);
            var report = await idle.FlushPendingAsync(TestContext.Current.CancellationToken);

            report.Applied.ShouldBe(1);
            report.Remaining.ShouldBe(0);
            var text = await File.ReadAllTextAsync(
                Path.Combine(directory, "ReShade.ini"),
                TestContext.Current.CancellationToken);
            text.ShouldContain("KeyOverlay=36,0,0,0");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ReShadeConfigService CreateService(string directory, bool running)
    {
        return new ReShadeConfigService(
            new HotkeyQueueStore(new AppPaths(directory)),
            new StubProbe(running),
            NullLogger<ReShadeConfigService>.Instance);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fgm-reshade-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class StubProbe(bool isRunning) : IGameProcessProbe
    {
        public bool IsRunning(string? exePath)
        {
            return isRunning;
        }
    }
}
