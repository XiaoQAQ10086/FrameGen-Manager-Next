using System.Runtime.InteropServices;

namespace FrameGenManagerNext.Infrastructure.NvApi;

internal sealed partial class NvApiSession
{
    private static readonly int[] ApplicationNameOffsets = [8, 4_104, 8_200, 12_296];

    /// <summary>
    /// Every driver profile that binds this executable. NVIDIA's database can hold
    /// more than one such profile (for example "PUBG" and "PLAYERUNKNOWN'S
    /// BATTLEGROUNDS" both list TslGame.exe), and DRS_FindApplicationByName returns
    /// whichever it likes — so a single write can land in a profile the game never
    /// uses. Enumeration is only done on explicit enable/disable/restore.
    /// </summary>
    public IReadOnlyList<IntPtr> FindProfilesForExe(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var query = Marshal.GetDelegateForFunctionPointer<NvApiNative.QueryInterface>(
            NativeLibrary.GetExport(_library, "nvapi_QueryInterface"));
        var enumProfiles = GetDelegate<NvApiNative.EnumProfilesDelegate>(query, NvApiNative.IdEnumProfiles);
        var enumApplications = GetDelegate<NvApiNative.EnumApplicationsDelegate>(query, NvApiNative.IdEnumApplications);

        var matches = new List<IntPtr>();
        var seen = new HashSet<IntPtr>();

        var direct = FindProfileForExe(executablePath);
        if (direct is not null && !IsBaseProfile(direct.Value) && seen.Add(direct.Value))
        {
            matches.Add(direct.Value);
        }

        var slots = NvApiNative.ApplicationSlots;
        var slotSize = NvApiNative.ApplicationBufferSize;
        var buffer = NvApiNative.AllocateZeroed(slots * slotSize);
        try
        {
            for (uint index = 0; ; index++)
            {
                var result = enumProfiles(_session, index, out var profile);
                if (result != 0)
                {
                    break;
                }

                if (profile == IntPtr.Zero ||
                    IsBaseProfile(profile) ||
                    !seen.Add(profile))
                {
                    continue;
                }

                if (ProfileBindsExecutable(enumApplications, buffer, profile, executablePath))
                {
                    matches.Add(profile);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return matches;
    }

    private bool ProfileBindsExecutable(
        NvApiNative.EnumApplicationsDelegate enumApplications,
        IntPtr buffer,
        IntPtr profile,
        string executablePath)
    {
        var slots = NvApiNative.ApplicationSlots;
        var slotSize = NvApiNative.ApplicationBufferSize;
        InitializeApplicationSlots(buffer, slots, slotSize);

        var count = (uint)slots;
        if (enumApplications(_session, profile, 0, ref count, buffer) != 0)
        {
            return false;
        }

        for (var index = 0; index < Math.Min((int)count, slots); index++)
        {
            var slot = index * slotSize;
            foreach (var offset in ApplicationNameOffsets)
            {
                var stored = NvApiNative.ReadString(buffer + slot + offset, 0, 2_048);
                if (MatchesExecutable(stored, executablePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void InitializeApplicationSlots(IntPtr buffer, int slots, int slotSize)
    {
        var version = unchecked((int)((uint)slotSize | ((uint)NvApiNative.ApplicationVersion << 16)));
        for (var index = 0; index < slots; index++)
        {
            Marshal.WriteInt32(buffer, index * slotSize, version);
        }
    }

    private static bool MatchesExecutable(string stored, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var normalized = stored.Replace('/', '\\').Trim();
        var target = Path.GetFullPath(executablePath).Replace('/', '\\');
        if (string.Equals(normalized, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = Path.GetFileName(target);
        if (!normalized.Contains('\\', StringComparison.Ordinal))
        {
            // NVIDIA stores some entries as a bare executable name.
            return string.Equals(normalized, fileName, StringComparison.OrdinalIgnoreCase);
        }

        // Or as a relative path such as "binaries\win64\game.exe".
        return !Path.IsPathRooted(normalized) &&
               target.EndsWith("\\" + normalized, StringComparison.OrdinalIgnoreCase);
    }
}
