using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.Core.Features.Library;

public interface IGameLibraryStore
{
    Task<GameLibrary> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(GameLibrary library, CancellationToken cancellationToken);
}
