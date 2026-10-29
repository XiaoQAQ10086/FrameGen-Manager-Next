namespace FrameGenManagerNext.Domain.Library;

public sealed record GameExeCandidate(
    string FullPath,
    string RelativeDirectory,
    long Size)
{
    public string FileName => Path.GetFileName(FullPath);
}
