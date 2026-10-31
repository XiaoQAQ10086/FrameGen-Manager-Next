using System.Runtime.InteropServices;

namespace FrameGenManagerNext.Infrastructure.NvApi;

internal static class NvApiNative
{
    public const uint IdInitialize = 0x0150_E828;
    public const uint IdUninitialize = 0x0150_E829;
    public const uint IdCreateSession = 0x0694_D52E;
    public const uint IdLoadSettings = 0x375D_BD6B;
    public const uint IdGetBaseProfile = 0xDA84_66A0;
    public const uint IdFindProfile = 0x7E4A_9A0B;
    public const uint IdCreateProfile = 0xCC17_6068;
    public const uint IdCreateApplication = 0x4347_A9DE;
    public const uint IdSetSetting = 0x8A2C_F5F5;
    public const uint IdSaveSettings = 0xFCBC_7E14;
    public const uint IdDeleteProfile = 0x1709_3206;
    public const uint IdFindApplicationByName = 0xEEE5_66B2;
    public const uint IdDeleteProfileSetting = 0xD20D_29DF;
    public const uint IdGetSetting = 0xEA99_498D;
    public const uint IdProfileInfo = 0x61CD_6FD6;
    public const uint IdDestroySession = 0xDAEF_F8B0;
    public const uint IdEnumProfiles = 0xBC37_1EE0;
    public const uint IdEnumApplications = 0x7FA2_173A;

    public const uint SmoothMotionSettingId = 0xB0D3_84C0;

    public const int SettingBufferSize = 12_320;
    public const int ProfileBufferSize = 4_116;
    public const int ApplicationBufferSize = 20_492;
    public const int SettingCurrentOffset = 8_220;
    public const int ApplicationSlots = 32;
    public const int ApplicationVersion = 4;

    public const string PlaceholderName = "PLACEHOLDER";
    public const string ProfilePrefix = "FrameGen Manager - ";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int UninitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CreateSessionDelegate(out IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int LoadSettingsDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int GetBaseProfileDelegate(IntPtr session, out IntPtr profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int FindProfileDelegate(
        IntPtr session,
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        out IntPtr profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CreateProfileDelegate(
        IntPtr session,
        IntPtr profileBuffer,
        out IntPtr profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CreateApplicationDelegate(
        IntPtr session,
        IntPtr profile,
        IntPtr applicationBuffer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int FindApplicationDelegate(
        IntPtr session,
        [MarshalAs(UnmanagedType.LPWStr)] string executablePath,
        out IntPtr profile,
        IntPtr applicationBuffer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int GetSettingDelegate(
        IntPtr session,
        IntPtr profile,
        uint settingId,
        IntPtr settingBuffer,
        out uint extra);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int SetSettingDelegate(
        IntPtr session,
        IntPtr profile,
        IntPtr settingBuffer,
        uint a,
        uint b);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int DeleteSettingDelegate(
        IntPtr session,
        IntPtr profile,
        uint settingId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int SaveSettingsDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int DestroySessionDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int DeleteProfileDelegate(IntPtr session, IntPtr profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ProfileInfoDelegate(
        IntPtr session,
        IntPtr profile,
        IntPtr infoBuffer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int EnumProfilesDelegate(
        IntPtr session,
        uint index,
        out IntPtr profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int EnumApplicationsDelegate(
        IntPtr session,
        IntPtr profile,
        uint startIndex,
        ref uint count,
        IntPtr applicationBuffer);

    public static IntPtr AllocateZeroed(int size)
    {
        var buffer = Marshal.AllocHGlobal(size);
        Marshal.Copy(new byte[size], 0, buffer, size);
        return buffer;
    }

    public static void WriteVersion(IntPtr buffer, int size, int version)
    {
        // NVAPI packs the structure size in the low 16 bits and the version in the high 16 bits.
        Marshal.WriteInt32(buffer, 0, size | (version << 16));
    }

    public static void WriteString(IntPtr buffer, int offset, int maxChars, string value)
    {
        var length = Math.Min(value.Length, maxChars - 1);
        for (var index = 0; index < length; index++)
        {
            Marshal.WriteInt16(buffer, offset + (index * 2), value[index]);
        }

        Marshal.WriteInt16(buffer, offset + (length * 2), 0);
    }

    public static string ReadString(IntPtr buffer, int offset, int maxChars)
    {
        var chars = new char[maxChars];
        var length = 0;
        for (var index = 0; index < maxChars; index++)
        {
            var value = (char)Marshal.ReadInt16(buffer, offset + (index * 2));
            if (value == '\0')
            {
                break;
            }

            chars[length++] = value;
        }

        return new string(chars, 0, length);
    }

    public static IntPtr CreateSettingBuffer(uint settingId)
    {
        var buffer = AllocateZeroed(SettingBufferSize);
        WriteVersion(buffer, SettingBufferSize, 1);
        WriteString(buffer, 4, 2048, PlaceholderName);
        Marshal.WriteInt32(buffer, 4_100, (int)settingId);
        return buffer;
    }

    public static void SetSettingValue(IntPtr settingBuffer, uint value)
    {
        Marshal.WriteInt32(settingBuffer, SettingCurrentOffset, (int)value);
    }

    public static uint GetSettingValue(IntPtr settingBuffer)
    {
        return (uint)Marshal.ReadInt32(settingBuffer, SettingCurrentOffset);
    }

    public static IntPtr CreateProfileBuffer(string name)
    {
        var buffer = AllocateZeroed(ProfileBufferSize);
        WriteVersion(buffer, ProfileBufferSize, 1);
        WriteString(buffer, 4, 2048, name);
        return buffer;
    }

    public static IntPtr CreateApplicationBuffer(string executablePath)
    {
        var buffer = AllocateZeroed(ApplicationBufferSize);
        WriteVersion(buffer, ApplicationBufferSize, 4);
        WriteString(buffer, 8, 2048, executablePath);
        return buffer;
    }

    public static IntPtr CreateProfileInfoBuffer()
    {
        var buffer = AllocateZeroed(ProfileBufferSize);
        WriteVersion(buffer, ProfileBufferSize, 1);
        return buffer;
    }
}
