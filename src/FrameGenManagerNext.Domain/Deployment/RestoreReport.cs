namespace FrameGenManagerNext.Domain.Deployment;

public sealed record RestoreReport(
    int RestoredFiles,
    int RemovedFiles,
    int Conflicts,
    IReadOnlyList<string> Leftovers,
    IReadOnlyList<string> Warnings);
