using System.Runtime.InteropServices;
using FrameGenManagerNext.Core.Features.App;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Platform;

/// <summary>
/// Windows 11 exposes efficiency mode through PROCESS_POWER_THROTTLING_EXECUTION_SPEED
/// (EcoQoS). Older Windows versions reject the call, which is harmless.
/// </summary>
public sealed partial class EfficiencyModeService : IEfficiencyModeService
{
    private const int ProcessPowerThrottling = 4;
    private const uint Version = 1;
    private const uint ExecutionSpeed = 1;

    private readonly ILogger<EfficiencyModeService> _logger;

    public EfficiencyModeService(ILogger<EfficiencyModeService> logger)
    {
        _logger = logger;
    }

    public bool TryEnable()
    {
        var state = new ProcessPowerThrottlingState
        {
            Version = Version,
            ControlMask = ExecutionSpeed,
            StateMask = ExecutionSpeed,
        };

        if (SetProcessInformation(
                GetCurrentProcess(),
                ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<ProcessPowerThrottlingState>()))
        {
            _logger.LogInformation("Efficiency mode (EcoQoS) enabled for this process");
            return true;
        }

        var error = Marshal.GetLastWin32Error();
        _logger.LogInformation(
            "Efficiency mode is not available on this system (Win32 {Error})",
            error);
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
    private static partial bool SetProcessInformation(
        IntPtr process,
        int processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);
}
