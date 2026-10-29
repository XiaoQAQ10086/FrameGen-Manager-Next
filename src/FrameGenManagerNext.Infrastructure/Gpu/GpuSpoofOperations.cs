using System.Security;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Win32;

namespace FrameGenManagerNext.Infrastructure.Gpu;

public static class GpuSpoofOperations
{
    private const string DisplayClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private const string PciEnumKey = @"SYSTEM\CurrentControlSet\Enum\PCI";
    private const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string NvidiaService = "nvlddmkm";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static GpuSpoofState ReadState(AppPaths paths, bool? elevated = null)
    {
        var backup = LoadBackup(paths);
        var adapters = EnumerateAdapters(backup);
        return new GpuSpoofState(elevated ?? IsProcessElevated(), adapters);
    }

    public static bool IsProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public static IReadOnlyList<GpuSpoofAdapter> EnumerateAdapters(GpuSpoofBackup? backup)
    {
        var adapters = new List<GpuSpoofAdapter>();

        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var classKey = hklm.OpenSubKey(DisplayClassKey);
        if (classKey is null)
        {
            return adapters;
        }

        var classMap = new Dictionary<string, (string DriverName, string DriverVersion)>(StringComparer.OrdinalIgnoreCase);
        foreach (var subName in classKey.GetSubKeyNames())
        {
            if (subName.Length != 4 || !subName.All(char.IsAsciiDigit))
            {
                continue;
            }

            using var subKey = classKey.OpenSubKey(subName);
            if (subKey?.GetValue("DriverDesc") is not string description ||
                !description.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var matching = subKey.GetValue("MatchingDeviceId") as string;
            if (string.IsNullOrWhiteSpace(matching) ||
                !matching.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var version = subKey.GetValue("DriverVersion") as string ?? string.Empty;
            classMap[subName] = (description.Trim(), version.Trim());
        }

        if (classMap.Count == 0)
        {
            return adapters;
        }

        using var pciKey = hklm.OpenSubKey(PciEnumKey);
        if (pciKey is null)
        {
            return adapters;
        }

        foreach (var deviceName in pciKey.GetSubKeyNames())
        {
            if (!deviceName.StartsWith("VEN_10DE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var deviceKey = pciKey.OpenSubKey(deviceName);
            if (deviceKey is null)
            {
                continue;
            }

            foreach (var instanceName in deviceKey.GetSubKeyNames())
            {
                using var instanceKey = deviceKey.OpenSubKey(instanceName);
                if (instanceKey is null)
                {
                    continue;
                }

                if (instanceKey.GetValue("ClassGUID") is not string classGuid ||
                    !classGuid.Equals(DisplayClassGuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (instanceKey.GetValue("Service") is not string service ||
                    !service.Equals(NvidiaService, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (instanceKey.GetValue("Driver") is not string driver)
                {
                    continue;
                }

                var classSub = driver.Split('\\').LastOrDefault();
                if (string.IsNullOrWhiteSpace(classSub) ||
                    !classMap.TryGetValue(classSub, out var driverInfo))
                {
                    continue;
                }

                var hardwareIds = instanceKey.GetValue("HardwareID") as string[];
                var rawDescription = instanceKey.GetValue("DeviceDesc") as string;
                var currentName = string.IsNullOrWhiteSpace(rawDescription)
                    ? driverInfo.DriverName
                    : DisplayName(rawDescription);
                var enumKey = $@"{PciEnumKey}\{deviceName}\{instanceName}";
                var entry = backup?.Entries.FirstOrDefault(candidate =>
                    candidate.EnumKey.Equals(enumKey, StringComparison.OrdinalIgnoreCase));

                adapters.Add(new GpuSpoofAdapter(
                    enumKey,
                    driverInfo.DriverName,
                    currentName,
                    hardwareIds?.FirstOrDefault() ?? string.Empty,
                    !currentName.Equals(driverInfo.DriverName, StringComparison.OrdinalIgnoreCase),
                    entry?.ExistedBefore == true,
                    entry?.Original));
            }
        }

        return adapters;
    }

    public static Result<GpuSpoofOperationReport> Apply(AppPaths paths, string presetName)
    {
        if (!GpuSpoofPresets.IsAllowed(presetName))
        {
            return Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.InvalidArgument,
                $"GPU name is not in the allowed preset list: {presetName}"));
        }

        try
        {
            paths.EnsureCreated();
            var backup = LoadBackup(paths) ?? GpuSpoofBackup.Create();
            var adapters = EnumerateAdapters(backup);
            var adapter = adapters.Count > 0 ? adapters[0] : null;
            if (adapter is null)
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.GpuUnsupported,
                    "No NVIDIA PCI display adapter was found."));
            }

            var raw = ReadRawDeviceDescription(adapter.EnumKey);
            var entry = backup.Entries.FirstOrDefault(candidate =>
                candidate.EnumKey.Equals(adapter.EnumKey, StringComparison.OrdinalIgnoreCase));
            var now = DateTimeOffset.UtcNow.ToString("O");

            if (entry is null)
            {
                backup.Entries.Add(new GpuSpoofBackupEntry(
                    adapter.EnumKey,
                    raw is not null,
                    raw,
                    presetName,
                    adapter.DriverName,
                    now));
            }
            else
            {
                entry.Applied = presetName;
            }

            backup.SavedAt = now;
            SaveBackup(paths, backup);

            var verified = LoadBackup(paths)?.Entries.Any(candidate =>
                candidate.EnumKey.Equals(adapter.EnumKey, StringComparison.OrdinalIgnoreCase)) == true;
            if (!verified)
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.BackupCorrupt,
                    "The GPU name backup could not be read back; no registry value was changed."));
            }

            WriteDeviceDescription(adapter.EnumKey, presetName, raw);
            return Result<GpuSpoofOperationReport>.Ok(new GpuSpoofOperationReport(
                $"GPU name changed from \"{adapter.CurrentName}\" to \"{presetName}\". A restart is required.",
                ReadState(paths)));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            return Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.AccessDenied,
                "Administrator rights are required to write the GPU name."));
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            return Result<GpuSpoofOperationReport>.Fail(new AppError(ErrorCode.GpuSpoofFailed, exception.Message));
        }
    }

    public static Result<GpuSpoofOperationReport> Restore(
        AppPaths paths,
        GpuSpoofRestoreTarget target)
    {
        try
        {
            var backup = LoadBackup(paths) ?? GpuSpoofBackup.Create();
            var adapters = EnumerateAdapters(backup);
            var adapter = adapters.Count > 0 ? adapters[0] : null;
            if (adapter is null)
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.GpuUnsupported,
                    "No NVIDIA PCI display adapter was found."));
            }

            string targetName;
            if (target == GpuSpoofRestoreTarget.DriverName)
            {
                targetName = adapter.DriverName;
            }
            else
            {
                var entry = backup.Entries.FirstOrDefault(candidate =>
                    candidate.EnumKey.Equals(adapter.EnumKey, StringComparison.OrdinalIgnoreCase));
                if (entry is null || !entry.ExistedBefore || string.IsNullOrWhiteSpace(entry.Original))
                {
                    return Result<GpuSpoofOperationReport>.Fail(new AppError(
                        ErrorCode.BackupCorrupt,
                        "No original DeviceDesc value is recorded for this adapter."));
                }

                targetName = entry.Original;
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.GpuSpoofFailed,
                    "The restore target is empty."));
            }

            WriteDeviceDescription(adapter.EnumKey, targetName, adapter.CurrentName);
            return Result<GpuSpoofOperationReport>.Ok(new GpuSpoofOperationReport(
                $"GPU name restored to \"{DisplayName(targetName)}\". A restart is required.",
                ReadState(paths)));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            return Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.AccessDenied,
                "Administrator rights are required to restore the GPU name."));
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            return Result<GpuSpoofOperationReport>.Fail(new AppError(ErrorCode.GpuSpoofFailed, exception.Message));
        }
    }

    public static GpuSpoofBackup? LoadBackup(AppPaths paths)
    {
        var path = BackupPath(paths);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GpuSpoofBackup>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            try
            {
                File.Move(path, path + ".bad", overwrite: true);
            }
            catch (Exception moveException) when (moveException is IOException or UnauthorizedAccessException)
            {
                // Keep the corrupt file; the next apply will replace it.
            }

            return null;
        }
    }

    public static void SaveBackup(AppPaths paths, GpuSpoofBackup backup)
    {
        var path = BackupPath(paths);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(backup, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    public static string DisplayName(string raw)
    {
        var value = raw.LastIndexOf(';') >= 0
            ? raw[(raw.LastIndexOf(';') + 1)..]
            : raw;
        return value.Trim();
    }

    private static string BackupPath(AppPaths paths)
    {
        return Path.Combine(paths.StateDirectory, "gpu-name", "manifest.json");
    }

    private static string? ReadRawDeviceDescription(string enumKey)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(enumKey, writable: false);
        return key?.GetValue("DeviceDesc") as string;
    }

    private static void WriteDeviceDescription(string enumKey, string value, string? rollbackValue)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(enumKey, writable: true)
                        ?? throw new UnauthorizedAccessException($"Could not open HKLM\\{enumKey} for writing.");

        key.SetValue("DeviceDesc", value, RegistryValueKind.String);
        var actual = key.GetValue("DeviceDesc") as string;
        if (string.Equals(actual, value, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(rollbackValue))
        {
            try
            {
                key.SetValue("DeviceDesc", rollbackValue, RegistryValueKind.String);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
            {
                // The caller still needs the read-back failure.
            }
        }

        throw new InvalidOperationException($"DeviceDesc read-back mismatch: \"{actual}\".");
    }
}

public sealed class GpuSpoofBackup
{
    public int SchemaVersion { get; set; } = 1;

    public string SavedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    public List<GpuSpoofBackupEntry> Entries { get; set; } = [];

    public static GpuSpoofBackup Create()
    {
        return new GpuSpoofBackup();
    }
}

public sealed class GpuSpoofBackupEntry(
    string enumKey,
    bool existedBefore,
    string? original,
    string applied,
    string driverName,
    string savedAt)
{
    public string EnumKey { get; } = enumKey;

    public bool ExistedBefore { get; } = existedBefore;

    public string? Original { get; } = original;

    public string Applied { get; set; } = applied;

    public string DriverName { get; } = driverName;

    public string SavedAt { get; } = savedAt;
}
