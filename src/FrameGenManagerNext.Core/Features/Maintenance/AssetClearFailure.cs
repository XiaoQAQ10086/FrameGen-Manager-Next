using FrameGenManagerNext.Domain.Errors;

namespace FrameGenManagerNext.Core.Features.Maintenance;

public sealed record AssetClearFailure(
    AssetFamily Family,
    string GameId,
    string GameName,
    string TargetDirectory,
    ErrorCode Code,
    string Detail,
    IReadOnlyList<string> Remaining);
