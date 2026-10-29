namespace FrameGenManagerNext.Core.Features.Diagnostics;

public interface IDiagnosticsService
{
    string LogsDirectory { get; }

    Task<string> GetLogTailAsync(int maxLines, CancellationToken cancellationToken);

    Task<string> ExportAsync(CancellationToken cancellationToken);
}
