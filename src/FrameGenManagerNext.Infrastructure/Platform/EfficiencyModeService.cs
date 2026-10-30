using System.Runtime.InteropServices;
using FrameGenManagerNext.Core.Features.App;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Platform;

/// <summary>
/// Windows 11 efficiency mode needs both halves: idle priority and EcoQoS
/// (PROCESS_POWER_THROTTLING_EXECUTION_SPEED). Applying only EcoQoS runs but does
/// not show as "Efficiency mode" in Task Manager. The app enables it only while
/// its window is not in the foreground, then restores normal priority.
/// </summary>
public sealed partial class EfficiencyModeService : IEfficiencyModeService
{
    private const int ProcessPowerThrottling = 4;
    private const uint Version = 1;
    private const uint ExecutionSpeed = 1;
    private const uint IdlePriorityClass = 0x0000_0040;
    private const uint NormalPriorityClass = 0x0000_0020;

    private readonly ILogger<EfficiencyModeService> _logger;
    private bool _background;

    public EfficiencyModeService(ILogger<EfficiencyModeService> logger)
    {
        _logger = logger;
    }

    public bool SetBackground(bool background)
    {
        if (_background == background)
        {
            return true;
        }

        var state = new ProcessPowerThrottlingState
        {
            Version = Version,
            ControlMask = ExecutionSpeed,
            StateMask = background ? ExecutionSpeed : 0,
        };

        var priority = SetPriorityClass(
            GetCurrentProcess(),
            background ? IdlePriorityClass : NormalPriorityClass);
        var throttled = SetProcessInformation(
            GetCurrentProcess(),
            ProcessPowerThrottling,
            ref state,
            (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());

        if (background && priority && throttled)
        {
            _background = true;
            _logger.LogDebug("Efficiency mode enabled (idle priority + EcoQoS)");
            return true;
        }

        if (!background)
        {
            _background = false;
            _logger.LogDebug("Efficiency mode disabled; normal priority restored");
            return priority || throttled;
        }

        var error = Marshal.GetLastWin32Error();
        _logger.LogDebug(
            "Efficiency mode is not available (Win32 {Error}); priority={Priority} throttling={Throttled}",
            error,
            priority,
            throttled);
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetPriorityClass(IntPtr process, uint priorityClass);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessInformation(
        IntPtr process,
        int processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);
}
