namespace FrameGenManagerNext.Core.Features.App;

public interface IAppEnvironment
{
    string DataRoot { get; }

    string LogsDirectory { get; }

    bool IsPortable { get; }
}
