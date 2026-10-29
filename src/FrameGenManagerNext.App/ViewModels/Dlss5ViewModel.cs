using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.App.Localization;
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
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = L.T("Dlss5.Status.NotChecked");

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
        StatusText = L.T("Dlss5.Status.Checking");

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
                ? L.F("Dlss5.Status.Checked", Games.Count)
                : L.T("Dlss5.Status.GpuUnsupported");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load DLSS5 page");
            StatusText = L.T("Dlss5.Status.CheckFailed");
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync(Dlss5EntryViewModel? game)
    {
        if (game is null || !game.HasExecutable || !game.CanInstall || !_gpuSupported)
        {
            return;
        }

        if (!Confirm(L.F("Dlss5.Confirm.Install", game.GameName, game.TargetDirectory)))
        {
            return;
        }

        await RunOperationAsync(
            game,
            L.T("Dlss5.Status.Installing"),
            ct => _dlss5.InstallAsync(
                game.TargetDirectory,
                game.ExePath!,
                CreateProgress(),
                ct),
            L.T("Dlss5.Status.Installed"));
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task RestoreAsync(Dlss5EntryViewModel? game)
    {
        if (game is null)
        {
            return;
        }

        if (!Confirm(L.F("Dlss5.Confirm.Restore", game.GameName)))
        {
            return;
        }

        await RunOperationAsync(
            game,
            L.T("Dlss5.Status.Restoring"),
            ct => _dlss5.RestoreAsync(
                game.TargetDirectory,
                CreateProgress(),
                ct),
            L.T("Dlss5.Status.Restored"));
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task UninstallAsync(Dlss5EntryViewModel? game)
    {
        if (game is null || IsBusy)
        {
            return;
        }

        if (!Confirm(L.F("Dlss5.Confirm.Uninstall", game.GameName)))
        {
            return;
        }

        IsBusy = true;
        StatusText = L.T("Dlss5.Status.Uninstalling");
        Warnings = string.Empty;
        HasWarnings = false;

        try
        {
            var result = await _dlss5.UninstallAsync(
                game.TargetDirectory,
                game.ExePath,
                CreateProgress(),
                CancellationToken.None);

            if (result.IsFailure)
            {
                StatusText = L.F("Dlss5.Status.UninstallFailed", result.Error!.Code);
                Warnings = result.Error.Diagnostic ?? result.Error.Code.ToString();
                HasWarnings = true;
                return;
            }

            StatusText = result.Value.IsClean
                ? L.T("Dlss5.Status.UninstallDone")
                : L.T("Dlss5.Status.UninstallPartial");
            if (!result.Value.IsClean)
            {
                Warnings = string.Join("、", result.Value.Remaining);
                HasWarnings = true;
            }

            await RefreshEntryAsync(game);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "DLSS5 uninstall failed for {Game}", game.GameName);
            StatusText = L.T("Dlss5.Status.UninstallFailedShort");
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
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

            await RefreshEntryAsync(game);
        }
    }

    private async Task RefreshEntryAsync(Dlss5EntryViewModel game)
    {
        if (!game.HasExecutable)
        {
            game.SetUnavailable(L.T("Library.UnidentifiedExe"));
            return;
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
                StatusText = L.F("Dlss5.Status.OperationFailed", result.Error!.Code);
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
            StatusText = L.T("Dlss5.Status.Failed");
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
            L.T("Dlss5.UninstallConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private bool CanInstall()
    {
        return !IsBusy && _gpuSupported;
    }

    private bool CanModify()
    {
        return !IsBusy;
    }
}
