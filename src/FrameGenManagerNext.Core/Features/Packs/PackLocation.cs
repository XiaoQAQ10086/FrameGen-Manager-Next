using FrameGenManagerNext.Domain.Packs;

namespace FrameGenManagerNext.Core.Features.Packs;

public sealed record PackLocation(
    AssetPack Pack,
    string ManifestPath,
    string PayloadDirectory);
