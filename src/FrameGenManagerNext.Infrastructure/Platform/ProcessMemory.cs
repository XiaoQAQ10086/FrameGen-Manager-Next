using System.Runtime.InteropServices;

namespace FrameGenManagerNext.Infrastructure.Platform;

/// <summary>
/// Trims the process working set when the window is hidden. This moves idle pages
/// to standby instead of keeping them resident while a game is running.
/// </summary>
public static partial class ProcessMemory
{
    public static bool TrimWorkingSet()
    {
        try
        {
            return SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);
        }
        catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(
        IntPtr process,
        IntPtr minimumWorkingSetSize,
        IntPtr maximumWorkingSetSize);
}
