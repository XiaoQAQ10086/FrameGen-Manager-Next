using System.Diagnostics;
using FrameGenManagerNext.Core.Features.Deployment;

namespace FrameGenManagerNext.Infrastructure.Processes;

public sealed class GameProcessProbe : IGameProcessProbe
{
    public bool IsRunning(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        string name;
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(exePath);
            name = Path.GetFileNameWithoutExtension(fullPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(name);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    var processPath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(processPath) ||
                        string.Equals(processPath, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // The name matches and the path is unavailable or equal.
                        return true;
                    }
                }
                catch (Exception exception) when (
                    exception is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // The process name already matches; do not assume it is stopped.
                    return true;
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        return false;
    }
}
