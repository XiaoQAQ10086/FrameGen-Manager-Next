using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.App.Services;
using FrameGenManagerNext.Core.Features.App;
using FrameGenManagerNext.Core.Features.Settings;
using FrameGenManagerNext.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IAppEnvironment _environment;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _initialized;

    public SettingsViewModel(
        ISettingsService settings,
        IAppEnvironment environment,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _environment = environment;
        _logger = logger;
    }

    public IReadOnlyList<string> ThemeChoices { get; } = ["跟随系统", "深色", "浅色"];

    public string DataRoot => _environment.DataRoot;

    public string PacksDirectory => _environment.PacksDirectory;

    public string BackupsDirectory => _environment.BackupsDirectory;

    public string ConfigDirectory => _environment.ConfigDirectory;

    public string LogsDirectory => _environment.LogsDirectory;

    public string ModeDisplay => _environment.IsPortable ? "便携版" : "安装版";

    [ObservableProperty]
    private string _selectedTheme = "跟随系统";

    [ObservableProperty]
    private bool _autoScanOnStartup;

    [ObservableProperty]
    private bool _showCovers;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "设置保存在数据目录的 config\\app.json";

    public Task LoadAsync()
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        _initialized = true;
        var settings = _settings.Load();
        SelectedTheme = settings.Theme switch
        {
            AppTheme.Dark => "深色",
            AppTheme.Light => "浅色",
            _ => "跟随系统",
        };
        AutoScanOnStartup = settings.AutoScanOnStartup;
        ShowCovers = settings.ShowCovers;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在保存设置...";

        try
        {
            var current = _settings.Load();
            var updated = current with
            {
                Theme = SelectedTheme switch
                {
                    "深色" => AppTheme.Dark,
                    "浅色" => AppTheme.Light,
                    _ => AppTheme.System,
                },
                AutoScanOnStartup = AutoScanOnStartup,
                ShowCovers = ShowCovers,
            };

            var result = await _settings.SaveAsync(updated, CancellationToken.None);
            if (result.IsFailure)
            {
                StatusText = $"保存失败：{result.Error!.Code}";
                return;
            }

            ThemeService.Apply(result.Value.Theme);
            StatusText = "设置已保存";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not save settings");
            StatusText = "保存设置失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenPacks()
    {
        OpenFolder(PacksDirectory);
    }

    [RelayCommand]
    private void OpenBackups()
    {
        OpenFolder(BackupsDirectory);
    }

    [RelayCommand]
    private void OpenConfig()
    {
        OpenFolder(ConfigDirectory);
    }

    [RelayCommand]
    private void OpenLogs()
    {
        OpenFolder(LogsDirectory);
    }

    [RelayCommand]
    private void OpenHagsSettings()
    {
        var uri = Environment.OSVersion.Version.Build >= 22000
            ? "ms-settings:display-advancedgraphics-default"
            : "ms-settings:display-advancedgraphics";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Could not open HAGS settings");
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Could not open folder {Path}", path);
        }
    }
}
