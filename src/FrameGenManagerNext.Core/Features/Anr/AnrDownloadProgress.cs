namespace FrameGenManagerNext.Core.Features.Anr;

public sealed record AnrDownloadProgress(
    string Phase,
    long BytesReceived,
    long TotalBytes,
    string? Source,
    double BytesPerSecond = 0);
