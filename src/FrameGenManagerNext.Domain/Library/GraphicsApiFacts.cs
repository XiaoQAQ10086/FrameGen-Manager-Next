namespace FrameGenManagerNext.Domain.Library;

public sealed record GraphicsApiFacts(
    IReadOnlyCollection<string> ImportedModules,
    IReadOnlyCollection<string> SiblingFiles,
    string? ExeName = null,
    bool WrapperIsVulkan = false);
