using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.App.Localization;
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
    private string _statusText = L.T("SmoothMotion.Status.NotLoaded");

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
        StatusText = L.T("SmoothMotion.Status.Checking");

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
            StatusText = L.F("SmoothMotion.Status.Checked", Games.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load Smooth Motion page");
            StatusText = L.T("SmoothMotion.Status.CheckFailed");
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

        if (!Confirm(L.F("SmoothMotion.Confirm.Enable", game.GameName, game.TargetDirectory)))
        {
            return;
        }

        await RunOperationAsync(
            game,
            L.T("SmoothMotion.Status.Enabling"),
            ct => _smoothMotion.EnableAsync(
                game.GameName,
                game.TargetDirectory,
                game.ExePath!,
                CreateProgress(),
                ct),
            L.T("SmoothMotion.Status.Enabled"));
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task DisableAsync(SmoothMotionEntryViewModel? game)
    {
        if (game is null || !game.HasExecutable)
        {
            return;
        }

        if (!Confirm(L.F("SmoothMotion.Confirm.Disable", game.GameName)))
        {
            return;
        }

        await RunOperationAsync(
            game,
            L.T("SmoothMotion.Status.Disabling"),
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
            L.T("SmoothMotion.Status.Disabled"));
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task RestoreAsync(SmoothMotionEntryViewModel? game)
    {
        if (game is null || !game.HasExecutable)
        {
            return;
        }

        if (!Confirm(L.F("SmoothMotion.Confirm.Restore", game.GameName)))
        {
            return;
        }

        await RunOperationAsync(
            game,
            L.T("SmoothMotion.Status.Restoring"),
            ct => _smoothMotion.RestoreAsync(
                game.GameName,
                game.TargetDirectory,
                game.ExePath!,
                CreateProgress(),
                ct),
            L.T("SmoothMotion.Status.Restored"));
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
                game.SetUnavailable(L.T("Library.UnidentifiedExe"));
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
                StatusText = L.F("SmoothMotion.Status.OperationFailed", result.Error!.Code);
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
            StatusText = L.T("SmoothMotion.Status.Failed");
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
                "planning" => L.F("Deployment.Phase.Planning", update.Subject),
                "writing" => L.F("Deployment.Phase.Writing", update.Subject),
                "restoring" => L.F("Deployment.Phase.Restoring", update.Subject),
                "done" => L.T("Deployment.Phase.Done"),
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
