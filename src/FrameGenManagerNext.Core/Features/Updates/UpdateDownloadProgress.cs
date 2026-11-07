namespace FrameGenManagerNext.Core.Features.Updates;

public sealed record UpdateDownloadProgress(
    long Received,
    long Total,
    double BytesPerSecond,
    string Source);
