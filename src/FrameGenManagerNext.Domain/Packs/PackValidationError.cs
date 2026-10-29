namespace FrameGenManagerNext.Domain.Packs;

/// <summary>
/// Technical validation error for a pack. Codes are stable; messages are English diagnostics.
/// </summary>
public sealed record PackValidationError(string Code, string Path, string Message);
