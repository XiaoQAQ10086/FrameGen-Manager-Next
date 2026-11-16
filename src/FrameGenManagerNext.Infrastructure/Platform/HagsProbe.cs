using System.Runtime.InteropServices;

namespace FrameGenManagerNext.Infrastructure.Platform;

/// <summary>
/// Reads the live hardware-accelerated GPU scheduling state from every display
/// adapter. Querying only the adapter behind the primary display DC can land on
/// an iGPU or a virtual display adapter and report HAGS as disabled even when
/// the NVIDIA adapter has it enabled, so adapters are enumerated first.
/// </summary>
internal static class HagsProbe
{
    private const int Wddm27Caps = 70;
    private const uint HwSchSupportedBit = 1u << 0;
    private const uint HwSchEnabledBit = 1u << 1;
    private const int MaxAdapters = 32;

    public static HagsSnapshot Read()
    {
        return QueryAdapters() ?? QueryFromDisplayDc() ?? new HagsSnapshot(
            null,
            Supported: false,
            "no D3DKMT adapter could be queried");
    }

    private static HagsSnapshot? QueryAdapters()
    {
        var size = Marshal.SizeOf<D3DKMT_ADAPTERINFO>();
        var buffer = Marshal.AllocHGlobal(size * MaxAdapters);
        try
        {
            var enumeration = new D3DKMT_ENUMADAPTERS2
            {
                NumAdapters = MaxAdapters,
                Adapters = buffer,
            };

            int status;
            try
            {
                status = D3DKMTEnumAdapters2(ref enumeration);
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }

            if (status != 0 || enumeration.NumAdapters == 0)
            {
                return null;
            }

            var count = (int)Math.Min(enumeration.NumAdapters, MaxAdapters);
            var details = new List<string>(count);
            var anyEnabled = false;
            var anySupported = false;
            var queried = 0;

            for (var index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<D3DKMT_ADAPTERINFO>(
                    IntPtr.Add(buffer, index * size));
                try
                {
                    var caps = QueryCaps(info.Adapter);
                    if (caps is null)
                    {
                        details.Add($"adapter{index}=query-failed");
                        continue;
                    }

                    queried++;
                    anyEnabled |= (caps.Value & HwSchEnabledBit) != 0;
                    anySupported |= (caps.Value & HwSchSupportedBit) != 0;
                    details.Add($"adapter{index}=0x{caps.Value:X8}");
                }
                finally
                {
                    var close = new D3DKMT_CLOSEADAPTER { Adapter = info.Adapter };
                    _ = D3DKMTCloseAdapter(ref close);
                }
            }

            if (queried == 0)
            {
                return null;
            }

            return new HagsSnapshot(
                anyEnabled,
                anySupported,
                string.Join(", ", details));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static HagsSnapshot? QueryFromDisplayDc()
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
                var caps = QueryCaps(open.Adapter);
                return caps is null
                    ? null
                    : new HagsSnapshot(
                        (caps.Value & HwSchEnabledBit) != 0,
                        (caps.Value & HwSchSupportedBit) != 0,
                        $"display-dc=0x{caps.Value:X8}");
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

    private static uint? QueryCaps(uint adapter)
    {
        var caps = new uint[1];
        var pinned = GCHandle.Alloc(caps, GCHandleType.Pinned);
        try
        {
            var query = new D3DKMT_QUERYADAPTERINFO
            {
                Adapter = adapter,
                Type = Wddm27Caps,
                PrivateDriverData = pinned.AddrOfPinnedObject(),
                PrivateDriverDataSize = sizeof(uint),
            };
            return D3DKMTQueryAdapterInfo(ref query) == 0 ? caps[0] : null;
        }
        finally
        {
            pinned.Free();
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

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_ENUMADAPTERS2
    {
        public uint NumAdapters;
        public IntPtr Adapters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_ADAPTERINFO
    {
        public uint Adapter;
        public long AdapterLuid;
        public uint NumOfSources;
        public int Present;
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

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTEnumAdapters2(ref D3DKMT_ENUMADAPTERS2 data);
}

internal readonly record struct HagsSnapshot(
    bool? Enabled,
    bool Supported,
    string Detail);
