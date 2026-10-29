using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class Dlss5ViewModel : ObservableObject
{
    private readonly IGameLibraryService _libraryService;
    private readonly IDlss5DeploymentService _dlss5;
    private readonly IGpuService _gpuService;
    private readonly ILogger<Dlss5ViewModel> _logger;
    private bool _initialized;
    private bool _gpuSupported;

    public Dlss5ViewModel(
        IGameLibraryService libraryService,
        IDlss5DeploymentService dlss5,
        IGpuService gpuService,
        ILogger<Dlss5ViewModel> logger)
    {
        _libraryService = libraryService;
        _dlss5 = dlss5;
        _gpuService = gpuService;
        _logger = logger;
    }

    public ObservableCollection<Dlss5EntryViewModel> Games { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "尚未检查 DLSS5 状态";

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
        StatusText = "正在检查 DLSS5 状态...";

        try
        {
            var gpu = await _gpuService.GetAsync(CancellationToken.None);
            _gpuSupported = gpu.Route is GpuRoute.Sm86 or GpuRoute.Sm75;

            var library = await _libraryService.LoadAsync(CancellationToken.None);
            Games.Clear();
            foreach (var game in library.EnumerateVisibleGames())
            {
                Games.Add(new Dlss5EntryViewModel(game));
            }

            HasGames = Games.Count > 0;
            IsEmpty = !HasGames;
            IsBusy = false;
            await RefreshStatesCoreAsync();
            StatusText = _gpuSupported
                ? $"已检查 {Games.Count} 个游戏"
                : "当前 GPU 不支持 DLSS5（仅 RTX 20/30）";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load DLSS5 page");
            StatusText = "检查 DLSS5 状态失败";
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task InstallAsync(Dlss5EntryViewModel? game)
    {
        if (game is null || !game.HasExecutable || !_gpuSupported)
        {
            return;
        }

        if (!Confirm($"将为“{game.GameName}”安装 DLSS5 / ReShade，并写入：\n{game.TargetDirectory}\n\n请确认游戏已关闭。继续？"))
        {
            return;
        }

        await RunOperationAsync(
            game,
            "正在安装 DLSS5...",
            ct => _dlss5.InstallAsync(
                game.TargetDirectory,
                game.ExePath!,
                CreateProgress(),
                ct),
            "DLSS5 安装完成");
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task RestoreAsync(Dlss5EntryViewModel? game)
    {
        if (game is null)
        {
            return;
        }

        if (!Confirm($"将还原“{game.GameName}”的 DLSS5 文件。\nReShade 模块/INI/日志会保留。继续？"))
        {
            return;
        }

        await RunOperationAsync(
            game,
            "正在还原 DLSS5...",
            ct => _dlss5.RestoreAsync(
                game.TargetDirectory,
                CreateProgress(),
                ct),
            "DLSS5 还原完成");
    }

    [RelayCommand]
    private async Task RefreshStatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await RefreshStatesCoreAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshStatesCoreAsync()
    {
        foreach (var game in Games)
        {
            if (!_gpuSupported)
            {
                game.SetUnavailable("当前 GPU 不支持");
                continue;
            }

            if (!game.HasExecutable)
            {
                game.SetUnavailable("未识别渲染 EXE");
                continue;
            }

            var result = await _dlss5.InspectAsync(
                game.TargetDirectory,
                game.ExePath!,
                CancellationToken.None);

            if (result.IsSuccess)
            {
                game.ApplyInspection(result.Value);
            }
            else
            {
                game.SetUnavailable(result.Error!.Code.ToString());
            }
        }
    }

    private async Task RunOperationAsync<T>(
        Dlss5EntryViewModel game,
        string status,
        Func<CancellationToken, Task<Result<T>>> operation,
        string successStatus)
    {
        IsBusy = true;
        StatusText = status;
        Warnings = string.Empty;
        HasWarnings = false;

        try
        {
            var result = await operation(CancellationToken.None);
            if (result.IsFailure)
            {
                StatusText = $"操作失败：{result.Error!.Code}";
                Warnings = result.Error.Diagnostic ?? result.Error.Code.ToString();
                HasWarnings = true;
                return;
            }

            StatusText = successStatus;
            if (game.HasExecutable)
            {
                var inspection = await _dlss5.InspectAsync(
                    game.TargetDirectory,
                    game.ExePath!,
                    CancellationToken.None);
                if (inspection.IsSuccess)
                {
                    game.ApplyInspection(inspection.Value);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "DLSS5 operation failed for {Game}", game.GameName);
            StatusText = "操作失败";
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Progress<DeploymentProgress> CreateProgress()
    {
        return new Progress<DeploymentProgress>(update =>
        {
            StatusText = update.Phase switch
            {
                "planning" => $"准备：{update.Subject}",
                "writing" => $"写入：{update.Subject}",
                "restoring" => $"还原：{update.Subject}",
                "done" => "完成",
                _ => update.Subject,
            };
        });
    }

    private static bool Confirm(string message)
    {
        return MessageBox.Show(
            message,
            "DLSS5",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private bool CanModify()
    {
        return !IsBusy && _gpuSupported;
    }
}
