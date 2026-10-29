namespace FrameGenManagerNext.Core.Features.App;

public interface IAppEnvironment
{
    string DataRoot { get; }

    string LogsDirectory { get; }

    string PacksDirectory { get; }

    string BackupsDirectory { get; }

    string ConfigDirectory { get; }

    bool IsPortable { get; }
}
