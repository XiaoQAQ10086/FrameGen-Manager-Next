namespace FrameGenManagerNext.Domain.Deployment;

/// <summary>
/// A file that a cleanup pass may delete once its current content still matches
/// the hash recorded here. Used when no deployment manifest exists.
/// </summary>
public sealed record CleanupCandidate(string RelativePath, string Sha256);
