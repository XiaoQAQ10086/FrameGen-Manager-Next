using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Dlss5;

/// <summary>
/// Install-time options for the ReShade + GITC pack. The proxy is a concrete
/// ReShade loader DLL; <see cref="ProxyModule"/> null keeps the API-derived choice.
/// </summary>
public sealed record Dlss5InstallOptions(
    GraphicsApi? KnownApi = null,
    string? ProxyModule = null,
    int? OverlayKey = null);
