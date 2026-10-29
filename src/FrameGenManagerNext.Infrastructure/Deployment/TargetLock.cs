using System.Text;

namespace FrameGenManagerNext.Infrastructure.Deployment;

/// <summary>
/// Cross-process lock backed by an exclusively opened file. Unlike a named Mutex,
/// it can be released from any thread after an await.
/// </summary>
internal sealed class TargetLock : IDisposable
{
    private readonly FileStream _stream;

    private TargetLock(FileStream stream)
    {
        _stream = stream;
    }

    public static TargetLock? TryAcquire(
        string lockDirectory,
        string familyId,
        string targetDirectory)
    {
        try
        {
            Directory.CreateDirectory(lockDirectory);
            var path = Path.Combine(
                lockDirectory,
                $"{familyId}-{DeploymentKey.ForPath(targetDirectory)}.lock");

            var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 4_096,
                FileOptions.None);

            try
            {
                stream.SetLength(0);
                var info = Encoding.UTF8.GetBytes(
                    $"pid={Environment.ProcessId}\nutc={DateTimeOffset.UtcNow:O}\n");
                stream.Write(info);
                stream.Flush();
            }
            catch (IOException)
            {
                // The lock is already held; diagnostic metadata is best-effort.
            }

            return new TargetLock(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
