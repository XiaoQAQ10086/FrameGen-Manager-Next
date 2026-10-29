namespace FrameGenManagerNext.Core.Features.Packs;

public interface IPackRepository
{
    Task<PackLocation?> FindAsync(string packId, CancellationToken cancellationToken);
}
