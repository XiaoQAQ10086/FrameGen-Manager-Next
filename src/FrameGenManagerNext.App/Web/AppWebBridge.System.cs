using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Settings;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private object BuildAppInfo()
    {
        var settings = _settings.Load();
        return new
        {
            product = AppInfo.ProductName,
            version = AppInfo.Version,
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            dataRoot = _paths.RootDirectory,
            logsDirectory = _paths.LogsDirectory,
            packsDirectory = _paths.PacksDirectory,
            bundledPacksDirectory = Path.Combine(AppContext.BaseDirectory, "packs"),
            backupsDirectory = _paths.BackupsDirectory,
            configDirectory = _paths.ConfigDirectory,
            portable = _environment.IsPortable,
            repository = "https://github.com/XiaoQAQ10086/FrameGen-Manager-Next",
            netdiskUrl = "https://pan.quark.cn/s/adea7ed4f2b2",
            language = LocalizationService.Current.Language == AppLanguage.English ? "en" : "zh",
            languagePreference = settings.Language switch
            {
                AppLanguage.ChineseSimplified => "zh",
                AppLanguage.English => "en",
                _ => "system",
            },
            settings = new
            {
                autoScanOnStartup = settings.AutoScanOnStartup,
                showCovers = settings.ShowCovers,
            },
            strings = LocalizationService.Current.Snapshot(),
        };
    }

    private async Task<object?> SaveSettingsAsync(JsonElement payload)
    {
        var current = _settings.Load();
        var language = ReadString(payload, "language", "system") switch
        {
            "en" => AppLanguage.English,
            "zh" => AppLanguage.ChineseSimplified,
            _ => AppLanguage.System,
        };
        var updated = current with
        {
            Language = language,
            Theme = AppTheme.Light,
            AutoScanOnStartup = ReadBool(payload, "autoScanOnStartup", current.AutoScanOnStartup),
            ShowCovers = ReadBool(payload, "showCovers", current.ShowCovers),
        };

        var result = await _settings.SaveAsync(updated, CancellationToken.None);
        if (result.IsFailure)
        {
            throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "save settings failed");
        }

        LocalizationService.Current.SetLanguage(language);
        return BuildAppInfo();
    }

    private object? OpenDataFolder()
    {
        OpenFolder(_paths.RootDirectory);
        return new { opened = true };
    }

    private static object? RestartComputer()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = "/r /t 0",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        return new { restarting = true };
    }

    private static object? OpenRepository()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/XiaoQAQ10086/FrameGen-Manager-Next",
            UseShellExecute = true,
        });
        return new { opened = true };
    }

    private object? OpenLogsFolder()
    {
        OpenFolder(_paths.LogsDirectory);
        return new { opened = true };
    }

    private object? OpenSettingsFolder(JsonElement payload)
    {
        var which = ReadString(payload, "which", "config");
        var path = which switch
        {
            "packs" => _paths.PacksDirectory,
            "bundledPacks" => Path.Combine(AppContext.BaseDirectory, "packs"),
            "backups" => _paths.BackupsDirectory,
            "logs" => _paths.LogsDirectory,
            _ => _paths.ConfigDirectory,
        };
        OpenFolder(path);
        return new { opened = true };
    }

    private static object? OpenHagsSettings()
    {
        var uri = Environment.OSVersion.Version.Build >= 22000
            ? "ms-settings:display-advancedgraphics-default"
            : "ms-settings:display-advancedgraphics";
        Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
        return new { opened = true };
    }

    private async Task<object?> ReadLogsAsync(JsonElement payload)
    {
        var lines = ReadInt(payload, "lines", 800);
        var text = await _diagnostics.GetLogTailAsync(lines, CancellationToken.None);
        return new
        {
            text,
            directory = _diagnostics.LogsDirectory,
        };
    }

    private async Task<object?> ExportLogsAsync()
    {
        var path = await _diagnostics.ExportAsync(CancellationToken.None);
        return new { path };
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true,
        });
    }

    private static bool ReadBool(JsonElement element, string name, bool fallback)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
    }
}
