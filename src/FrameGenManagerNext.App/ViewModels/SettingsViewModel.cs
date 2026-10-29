using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.App.Localization;
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

    public IReadOnlyList<string> LanguageChoices { get; } = ["简体中文", "English"];

    public ObservableCollection<string> ThemeChoices { get; } = [];

    public string DataRoot => _environment.DataRoot;

    public string PacksDirectory => _environment.PacksDirectory;

    public string BackupsDirectory => _environment.BackupsDirectory;

    public string ConfigDirectory => _environment.ConfigDirectory;

    public string LogsDirectory => _environment.LogsDirectory;

    public string ModeDisplay => L.T(_environment.IsPortable ? "Settings.Mode.Portable" : "Settings.Mode.Installed");

    [ObservableProperty]
    private string _selectedLanguage = "简体中文";

    [ObservableProperty]
    private string _selectedTheme = string.Empty;

    [ObservableProperty]
    private bool _autoScanOnStartup;

    [ObservableProperty]
    private bool _showCovers;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = L.T("Settings.Status.Initial");

    public Task LoadAsync()
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        _initialized = true;
        var settings = _settings.Load();
        SelectedLanguage = settings.Language == AppLanguage.English ? "English" : "简体中文";
        RebuildThemeChoices();
        SelectedTheme = settings.Theme switch
        {
            AppTheme.Dark => L.T("Settings.Theme.Dark"),
            AppTheme.Light => L.T("Settings.Theme.Light"),
            _ => L.T("Settings.Theme.System"),
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
        StatusText = L.T("Settings.Status.Saving");

        try
        {
            var current = _settings.Load();
            var language = SelectedLanguage == "English"
                ? AppLanguage.English
                : AppLanguage.ChineseSimplified;
            var updated = current with
            {
                Language = language,
                Theme = SelectedTheme == L.T("Settings.Theme.Dark")
                    ? AppTheme.Dark
                    : SelectedTheme == L.T("Settings.Theme.Light")
                        ? AppTheme.Light
                        : AppTheme.System,
                AutoScanOnStartup = AutoScanOnStartup,
                ShowCovers = ShowCovers,
            };

            var result = await _settings.SaveAsync(updated, CancellationToken.None);
            if (result.IsFailure)
            {
                StatusText = L.F("Settings.Status.SaveFailedCode", result.Error!.Code);
                return;
            }

            LocalizationService.Current.SetLanguage(result.Value.Language);
            RebuildThemeChoices();
            SelectedTheme = result.Value.Theme switch
            {
                AppTheme.Dark => L.T("Settings.Theme.Dark"),
                AppTheme.Light => L.T("Settings.Theme.Light"),
                _ => L.T("Settings.Theme.System"),
            };
            ThemeService.Apply(result.Value.Theme);
            OnPropertyChanged(nameof(ModeDisplay));
            StatusText = L.T("Settings.Status.Saved");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not save settings");
            StatusText = L.T("Settings.Status.SaveFailed");
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

    private void RebuildThemeChoices()
    {
        ThemeChoices.Clear();
        ThemeChoices.Add(L.T("Settings.Theme.System"));
        ThemeChoices.Add(L.T("Settings.Theme.Dark"));
        ThemeChoices.Add(L.T("Settings.Theme.Light"));
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
