using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Settings;

namespace FrameGenManagerNext.Core.Features.Settings;

public interface ISettingsService
{
    AppSettings Load();

    Task<Result<AppSettings>> SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken);
}
