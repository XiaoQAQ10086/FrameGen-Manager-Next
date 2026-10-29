using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Library;

public sealed record GameScanResult(
    GameLibrary Library,
    IReadOnlyList<string> Warnings);
