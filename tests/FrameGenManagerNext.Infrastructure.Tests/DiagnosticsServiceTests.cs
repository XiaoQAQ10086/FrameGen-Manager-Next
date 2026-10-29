using FrameGenManagerNext.Infrastructure.Diagnostics;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class DiagnosticsServiceTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-diagnostics-tests");

    [Fact]
    public async Task ReadsLogTailWhileWriterHoldsTheFile()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        var logPath = Path.Combine(paths.LogsDirectory, "app-20260101.log");
        await File.WriteAllTextAsync(
            logPath,
            "line1\nline2\nline3\nline4\nline5",
            TestContext.Current.CancellationToken);

        var service = new DiagnosticsService(
            paths,
            gpuService: null!,
            gameLibraryService: null!,
            NullLogger<DiagnosticsService>.Instance);

        await using var writer = new FileStream(
            logPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);

        var tail = await service.GetLogTailAsync(2, TestContext.Current.CancellationToken);

        tail.ShouldContain("line4");
        tail.ShouldContain("line5");
        tail.ShouldNotContain("line1");
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
