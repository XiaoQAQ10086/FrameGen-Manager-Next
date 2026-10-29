using FrameGenManagerNext.Core.Features.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Cli;

internal static class LogsCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        int lineCount,
        CancellationToken cancellationToken)
    {
        var diagnostics = services.GetRequiredService<IDiagnosticsService>();
        var text = await diagnostics.GetLogTailAsync(lineCount, cancellationToken);
        Console.WriteLine(text);
        return 0;
    }
}
