using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class SmoothMotionViewModel : ObservableObject
{
    private readonly IGameLibraryService _libraryService;
    private readonly ISmoothMotionDeploymentService _smoothMotion;
    private readonly ILogger<SmoothMotionViewModel> _logger;
    private bool _initialized;

    public SmoothMotionViewModel(
        IGameLibraryService libraryService,
        ISmoothMotionDeploymentService smoothMotion,
        ILogger<SmoothMotionViewModel> logger)
    {
        _libraryService = libraryService;
        _smoothMotion = smoothMotion;
        _logger = logger;
    }

    public ObservableCollection<SmoothMotionEntryViewModel> Games { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnableCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "尚未检查 Smooth Motion 状态";

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
        StatusText = "正在检查 Smooth Motion 状态...";

        try
        {
            var library = await _libraryService.LoadAsync(CancellationToken.None);
            Games.Clear();
            foreach (var game in library.EnumerateVisibleGames())
            {
                Games.Add(new SmoothMotionEntryViewModel(game));
            }

            HasGames = Games.Count > 0;
            IsEmpty = !HasGames;
            IsBusy = false;
            await RefreshStatesCoreAsync();
            StatusText = $"已检查 {Games.Count} 个游戏";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load Smooth Motion page");
            StatusText = "检查 Smooth Motion 状态失败";
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task EnableAsync(SmoothMotionEntryViewModel? game)
    {
        if (game is null || !game.HasExecutable)
        {
            return;
        }

        if (!Confirm($"将为“{game.GameName}”开启 Smooth Motion，并把解锁组件部署到：\n{game.TargetDirectory}\n\n请确认游戏已关闭。继续？"))
        {
            return;
        }

        await RunOperationAsync(
            game,
            "正在开启 Smooth Motion...",
            ct => _smoothMotion.EnableAsync(
                game.GameName,
                game.TargetDirectory,
                game.ExePath!,
                CreateProgress(),
                ct),
            "开启 Smooth Motion 完成");
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task DisableAsync(SmoothMotionEntryViewModel? game)
    {
        if (game is null || !game.HasExecutable)
        {
            return;
        }

        if (!Confirm($"将为“{game.GameName}”关闭 Smooth Motion。\n组件文件会保留，可稍后“还原”删除。继续？"))
        {
            return;
        }

        await RunOperationAsync(
            game,
            "正在关闭 Smooth Motion...",
            async ct =>
            {
                var result = await _smoothMotion.DisableAsync(
                    game.GameName,
                    game.TargetDirectory,
                    game.ExePath!,
                    ct);
                return result.IsSuccess
                    ? Result<object>.Ok(result.Value)
                    : Result<object>.Fail(result.Error!);
            },
            "关闭 Smooth Motion 完成");
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task RestoreAsync(SmoothMotionEntryViewModel? game)
    {
        if (game is null || !game.HasExecutable)
        {
            return;
        }

        if (!Confirm($"将还原“{game.GameName}”的 Smooth Motion 设置和组件文件。\n用户修改过的文件会保留并报告冲突。继续？"))
        {
            return;
        }

        await RunOperationAsync(
            game,
            "正在还原 Smooth Motion...",
            ct => _smoothMotion.RestoreAsync(
                game.GameName,
                game.TargetDirectory,
                game.ExePath!,
                CreateProgress(),
                ct),
            "还原 Smooth Motion 完成");
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
            if (!game.HasExecutable)
            {
                game.SetUnavailable("未识别渲染 EXE");
                continue;
            }

            var result = await _smoothMotion.InspectAsync(
                game.GameName,
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
        SmoothMotionEntryViewModel game,
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
            var inspection = await _smoothMotion.InspectAsync(
                game.GameName,
                game.TargetDirectory,
                game.ExePath!,
                CancellationToken.None);

            if (inspection.IsSuccess)
            {
                game.ApplyInspection(inspection.Value);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Smooth Motion operation failed for {Game}", game.GameName);
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
            "Smooth Motion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private bool CanModify()
    {
        return !IsBusy;
    }
}
