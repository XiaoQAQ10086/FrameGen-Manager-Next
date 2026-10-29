using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject, IDisposable
{
    private readonly IGameLibraryService _library;
    private readonly ILogger<LibraryViewModel> _logger;
    private CancellationTokenSource? _scanCancellation;
    private bool _initialized;

    public LibraryViewModel(IGameLibraryService library, ILogger<LibraryViewModel> logger)
    {
        _library = library;
        _logger = logger;
    }

    public ObservableCollection<GameEntryViewModel> Games { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveGameCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "尚未加载游戏库";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _warnings = string.Empty;

    [ObservableProperty]
    private bool _hasWarnings;

    [ObservableProperty]
    private bool _hasGames;

    [ObservableProperty]
    private bool _isEmpty = true;

    public async Task LoadAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsBusy = true;
        StatusText = "正在读取游戏库缓存...";

        try
        {
            var library = await _library.LoadAsync(CancellationToken.None);
            Apply(library);
            StatusText = Games.Count == 0
                ? "游戏库为空，点击“扫描游戏”开始"
                : $"已加载 {Games.Count} 个游戏";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load game library");
            StatusText = "读取游戏库失败";
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _scanCancellation = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        Warnings = string.Empty;
        HasWarnings = false;

        var progress = new Progress<GameScanProgress>(update =>
        {
            StatusText = update.Phase switch
            {
                "launchers" => $"正在扫描 {update.Subject}",
                "games" => $"正在探测 {update.Subject}",
                "done" => $"扫描完成：{update.Current} 个游戏",
                _ => StatusText,
            };

            ProgressValue = update.Total > 0
                ? update.Current * 100.0 / update.Total
                : 0;
        });

        try
        {
            var result = await _library.ScanAsync(progress, _scanCancellation.Token);
            Apply(result.Library);
            ProgressValue = 100;
            StatusText = $"扫描完成：{Games.Count} 个游戏";

            if (result.Warnings.Count > 0)
            {
                Warnings = string.Join(Environment.NewLine, result.Warnings);
                HasWarnings = true;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
            ProgressValue = 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Game scan failed");
            StatusText = "扫描失败";
            Warnings = exception.Message;
            HasWarnings = true;
            ProgressValue = 0;
        }
        finally
        {
            IsBusy = false;
            _scanCancellation?.Dispose();
            _scanCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task AddGameAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择游戏渲染 EXE",
            Filter = "可执行文件 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在添加游戏...";

        try
        {
            var library = await _library.AddManualAsync(dialog.FileName, CancellationToken.None);
            Apply(library);
            StatusText = $"已添加：{Games.Count} 个游戏";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not add manual game");
            StatusText = "添加游戏失败";
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task RemoveGameAsync(GameEntryViewModel? game)
    {
        if (game is null)
        {
            return;
        }

        var choice = MessageBox.Show(
            $"确定要从列表中移除“{game.Name}”吗？\n\n扫描到的游戏会被加入忽略列表；手动添加的游戏会被删除。",
            "移除游戏",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (choice != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在移除...";

        try
        {
            var library = await _library.RemoveAsync(game.Id, CancellationToken.None);
            Apply(library);
            StatusText = $"已移除：{Games.Count} 个游戏";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not remove game");
            StatusText = "移除失败";
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _scanCancellation?.Cancel();
    }

    [RelayCommand]
    private void OpenFolder(GameEntryViewModel? game)
    {
        if (game is null)
        {
            return;
        }

        var targetDirectory = game.TargetDirectory;
        if (!Directory.Exists(targetDirectory))
        {
            StatusText = "目标目录不存在";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{targetDirectory}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not open game folder");
            StatusText = "无法打开目录";
        }
    }

    public void Dispose()
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = null;
    }

    private bool CanModify()
    {
        return !IsBusy;
    }

    private void Apply(GameLibrary library)
    {
        Games.Clear();
        foreach (var game in library.EnumerateVisibleGames())
        {
            Games.Add(new GameEntryViewModel(game));
        }

        HasGames = Games.Count > 0;
        IsEmpty = !HasGames;
    }
}
