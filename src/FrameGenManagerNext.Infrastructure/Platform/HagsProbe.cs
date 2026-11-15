using System.Runtime.InteropServices;

namespace FrameGenManagerNext.Infrastructure.Platform;

/// <summary>
/// Reads the live hardware-accelerated GPU scheduling state from the display
/// adapter. The HwSchMode registry value is only an explicit override and is
/// absent on machines where Windows uses its default policy, so the WDDM 2.7
/// caps query is the authoritative source.
/// </summary>
internal static class HagsProbe
{
    private const int Wddm27Caps = 70;
    private const uint HwSchEnabledBit = 1u << 1;

    public static bool? IsEnabled()
    {
        var hdc = CreateDC("DISPLAY", null, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var open = new D3DKMT_OPENADAPTERFROMHDC { Hdc = hdc };
            if (D3DKMTOpenAdapterFromHdc(ref open) != 0)
            {
                return null;
            }

            try
            {
                var caps = new uint[1];
                var pinned = GCHandle.Alloc(caps, GCHandleType.Pinned);
                try
                {
                    var query = new D3DKMT_QUERYADAPTERINFO
                    {
                        Adapter = open.Adapter,
                        Type = Wddm27Caps,
                        PrivateDriverData = pinned.AddrOfPinnedObject(),
                        PrivateDriverDataSize = sizeof(uint),
                    };
                    if (D3DKMTQueryAdapterInfo(ref query) != 0)
                    {
                        return null;
                    }

                    return (caps[0] & HwSchEnabledBit) != 0;
                }
                finally
                {
                    pinned.Free();
                }
            }
            finally
            {
                var close = new D3DKMT_CLOSEADAPTER { Adapter = open.Adapter };
                _ = D3DKMTCloseAdapter(ref close);
            }
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_OPENADAPTERFROMHDC
    {
        public IntPtr Hdc;
        public uint Adapter;
        public long AdapterLuid;
        public uint VidPnSourceId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_QUERYADAPTERINFO
    {
        public uint Adapter;
        public int Type;
        public IntPtr PrivateDriverData;
        public uint PrivateDriverDataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_CLOSEADAPTER
    {
        public uint Adapter;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(
        string? driver,
        string? device,
        string? output,
        IntPtr initData);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTOpenAdapterFromHdc(ref D3DKMT_OPENADAPTERFROMHDC data);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTQueryAdapterInfo(ref D3DKMT_QUERYADAPTERINFO data);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER data);
}
