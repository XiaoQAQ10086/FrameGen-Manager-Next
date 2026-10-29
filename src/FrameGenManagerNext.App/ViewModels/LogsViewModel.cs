using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private string _statusText = "尚未读取日志";

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
        StatusText = "正在读取日志...";

        try
        {
            LogText = await _diagnostics.GetLogTailAsync(800, CancellationToken.None);
            StatusText = string.IsNullOrWhiteSpace(LogText) ? "暂无日志" : "已刷新";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not read logs");
            StatusText = "读取日志失败";
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
            StatusText = "无法打开日志目录";
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
        StatusText = "正在生成诊断包...";

        try
        {
            var path = await _diagnostics.ExportAsync(CancellationToken.None);
            StatusText = $"诊断包已生成：{path}";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not export diagnostics");
            StatusText = "诊断包生成失败";
            LogText = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
