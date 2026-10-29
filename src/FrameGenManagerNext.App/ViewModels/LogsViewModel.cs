using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.Core.Features.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class LogsViewModel : ObservableObject
{
    private readonly IDiagnosticsService _diagnostics;
    private readonly ILogger<LogsViewModel> _logger;
    private bool _initialized;

    public LogsViewModel(IDiagnosticsService diagnostics, ILogger<LogsViewModel> logger)
    {
        _diagnostics = diagnostics;
        _logger = logger;
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = L.T("Logs.Status.NotLoaded");

    [ObservableProperty]
    private string _logText = string.Empty;

    public async Task LoadAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = L.T("Logs.Status.Reading");

        try
        {
            LogText = await _diagnostics.GetLogTailAsync(800, CancellationToken.None);
            StatusText = string.IsNullOrWhiteSpace(LogText) ? L.T("Logs.Status.Empty") : L.T("Logs.Status.Refreshed");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not read logs");
            StatusText = L.T("Logs.Status.ReadFailed");
            LogText = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_diagnostics.LogsDirectory}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not open logs folder");
            StatusText = L.T("Logs.Status.OpenFailed");
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = L.T("Logs.Status.Exporting");

        try
        {
            var path = await _diagnostics.ExportAsync(CancellationToken.None);
            StatusText = L.F("Logs.Status.Exported", path);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not export diagnostics");
            StatusText = L.T("Logs.Status.ExportFailed");
            LogText = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
