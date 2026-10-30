using FrameGenManagerNext.Domain.Deployment;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed partial class DeploymentEngine
{
    private static List<DeployedFileRecord> StampTimestamps(
        List<DeployedFileRecord> records,
        string targetDirectory)
    {
        var stamped = new List<DeployedFileRecord>(records.Count);
        foreach (var record in records)
        {
            var path = GetTargetPath(targetDirectory, record.RelativePath);
            long? ticks = null;
            if (path.IsSuccess)
            {
                try
                {
                    ticks = new FileInfo(path.Value).LastWriteTimeUtc.Ticks;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    ticks = null;
                }
            }

            stamped.Add(record with { LastWriteTimeUtcTicks = ticks });
        }

        return stamped;
    }

}
