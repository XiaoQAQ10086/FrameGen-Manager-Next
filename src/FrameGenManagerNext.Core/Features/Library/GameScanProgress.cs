namespace FrameGenManagerNext.Core.Features.Library;

/// <summary>
/// Structured scan progress. The UI and CLI own the user-facing text.
/// </summary>
public sealed record GameScanProgress(
    string Phase,
    string Subject,
    int Current,
    int Total);
