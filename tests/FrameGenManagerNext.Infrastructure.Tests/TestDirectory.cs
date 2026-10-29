using System.Text;

namespace FrameGenManagerNext.Infrastructure.Tests;

internal sealed class TestDirectory : IDisposable
{
    public TestDirectory(string prefix = "fgm-test")
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            prefix,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string GetPath(string relative)
    {
        var parts = relative
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        return Path.Combine([Root, .. parts]);
    }

    public string CreateDirectory(string relative)
    {
        var path = GetPath(relative);
        Directory.CreateDirectory(path);
        return path;
    }

    public string WriteFile(string relative, string content)
    {
        var path = GetPath(relative);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup for temp test data.
        }
    }
}
