using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Core.Features.Settings;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject, IDisposable
{
    private readonly IGameLibraryService _library;
    private readonly IFrameGenDeploymentService _frameGen;
    private readonly IGameCoverService _covers;
    private readonly ISettingsService _settings;
    private readonly ILogger<LibraryViewModel> _logger;
    private CancellationTokenSource? _scanCancellation;
    private bool _initialized;

    public LibraryViewModel(
        IGameLibraryService library,
        IFrameGenDeploymentService frameGen,
        IGameCoverService covers,
        ISettingsService settings,
        ILogger<LibraryViewModel> logger)
    {
        _library = library;
        _frameGen = frameGen;
        _covers = covers;
        _settings = settings;
        _logger = logger;
    }

    public ObservableCollection<GameEntryViewModel> Games { get; } = [];

    public FrameGenOptionsViewModel Options { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDeployDialogCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeployCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreGameCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDeployDialogOpen;

    [ObservableProperty]
    private string _deployTargetName = string.Empty;

    [ObservableProperty]
    private string _deployTargetDirectory = string.Empty;

    [ObservableProperty]
    private GameEntryViewModel? _deployTarget;

    [ObservableProperty]
    private string _statusText = L.T("Library.Status.NotLoaded");

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
        StatusText = L.T("Library.Status.Loading");

        try
        {
            var library = await _library.LoadAsync(CancellationToken.None);
            Apply(library);
            StatusText = Games.Count == 0
                ? L.T("Library.Status.Empty")
                : L.F("Library.Status.Loaded", Games.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load game library");
            StatusText = L.T("Library.Status.LoadFailed");
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }

        if (_settings.Load().AutoScanOnStartup && Games.Count == 0)
        {
            await ScanAsync();
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
                "launchers" => L.F("Library.Status.ScanLaunchers", update.Subject),
                "games" => L.F("Library.Status.ScanGames", update.Subject),
                "done" => L.F("Library.Status.ScanDone", update.Current),
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
            StatusText = L.F("Library.Status.ScanDone", Games.Count);

            if (result.Warnings.Count > 0)
            {
                Warnings = string.Join(Environment.NewLine, result.Warnings);
                HasWarnings = true;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = L.T("Library.Status.ScanCancelled");
            ProgressValue = 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Game scan failed");
            StatusText = L.T("Library.Status.ScanFailed");
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
            Title = L.T("Library.Dialog.AddTitle"),
            Filter = L.T("Library.Dialog.ExeFilter"),
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        StatusText = L.T("Library.Status.Adding");

        try
        {
            var library = await _library.AddManualAsync(dialog.FileName, CancellationToken.None);
            Apply(library);
            StatusText = L.F("Library.Status.Added", Games.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not add manual game");
            StatusText = L.T("Library.Status.AddFailed");
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void OpenDeployDialog(GameEntryViewModel? game)
    {
        if (game is null)
        {
            return;
        }

        DeployTarget = game;
        DeployTargetName = game.Name;
        DeployTargetDirectory = game.TargetDirectory;
        IsDeployDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDeployDialog()
    {
        IsDeployDialogOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task ConfirmDeployAsync()
    {
        var game = DeployTarget;
        if (game is null)
        {
            return;
        }

        IsDeployDialogOpen = false;
        IsBusy = true;
        ProgressValue = 0;
        Warnings = string.Empty;
        HasWarnings = false;
        StatusText = L.T("Library.Status.Deploying");

        var progress = new Progress<DeploymentProgress>(update =>
        {
            StatusText = ProgressLabel(update);
            ProgressValue = update.Total > 0
                ? update.Current * 100.0 / update.Total
                : 0;
        });

        try
        {
            var result = await _frameGen.DeployAsync(
                game.TargetDirectory,
                Options.ToOptions(),
                progress,
                CancellationToken.None);

            if (result.IsFailure)
            {
                game.DeploymentStateDisplay = L.T("State.DeployFailed");
                StatusText = L.F("Library.Status.DeployFailedCode", result.Error!.Code);
                Warnings = result.Error.Diagnostic ?? result.Error.Code.ToString();
                HasWarnings = true;
                return;
            }

            game.SetState(DeploymentState.Installed);
            ProgressValue = 100;
            StatusText = L.F("Library.Status.DeployDone", game.Name);

            if (result.Value.Warnings.Count > 0)
            {
                Warnings = string.Join(Environment.NewLine, result.Value.Warnings);
                HasWarnings = true;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "FrameGen deployment failed");
            game.DeploymentStateDisplay = L.T("State.DeployFailed");
            StatusText = L.T("Library.Status.DeployFailed");
            Warnings = exception.Message;
            HasWarnings = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task RestoreGameAsync(GameEntryViewModel? game)
    {
        if (game is null)
        {
            return;
        }

        var choice = MessageBox.Show(
            L.F("Library.Confirm.Restore", game.TargetDirectory),
            L.T("Library.Dialog.RestoreTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        IsDeployDialogOpen = false;
        IsBusy = true;
        ProgressValue = 0;
        Warnings = string.Empty;
        HasWarnings = false;
        StatusText = L.T("Library.Status.Restoring");

        var progress = new Progress<DeploymentProgress>(update =>
        {
            StatusText = ProgressLabel(update);
            ProgressValue = update.Total > 0
                ? update.Current * 100.0 / update.Total
                : 0;
        });

        try
        {
            var result = await _frameGen.RestoreAsync(
                game.TargetDirectory,
                progress,
                CancellationToken.None);

            if (result.IsFailure)
            {
                StatusText = L.F("Library.Status.RestoreFailedCode", result.Error!.Code);
                Warnings = result.Error.Diagnostic ?? result.Error.Code.ToString();
                HasWarnings = true;
                return;
            }

            game.SetState(DeploymentState.NotDeployed);
            ProgressValue = 100;
            StatusText = L.F("Library.Status.RestoreDone", game.Name);

            var messages = new List<string>(result.Value.Warnings);
            messages.AddRange(result.Value.Leftovers.Select(leftover => L.F("Library.KeptFile", leftover)));
            if (messages.Count > 0)
            {
                Warnings = string.Join(Environment.NewLine, messages);
                HasWarnings = true;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "FrameGen restore failed");
            StatusText = L.T("Library.Status.RestoreFailed");
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
            L.F("Library.Confirm.Remove", game.Name),
            L.T("Library.Dialog.RemoveTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (choice != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        StatusText = L.T("Library.Status.Removing");

        try
        {
            var library = await _library.RemoveAsync(game.Id, CancellationToken.None);
            Apply(library);
            StatusText = L.F("Library.Status.Removed", Games.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not remove game");
            StatusText = L.T("Library.Status.RemoveFailed");
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
            StatusText = L.T("Library.Status.TargetMissing");
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
            StatusText = L.T("Library.Status.OpenFailed");
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
        if (_settings.Load().ShowCovers)
        {
            _ = LoadCoversAsync();
        }

        _ = RefreshDeploymentStatesAsync();
    }

    private static string ProgressLabel(DeploymentProgress update)
    {
        return update.Phase switch
        {
            "planning" => L.F("Deployment.Phase.Planning", update.Subject),
            "writing" => L.F("Deployment.Phase.Writing", update.Subject),
            "restoring" => L.F("Deployment.Phase.Restoring", update.Subject),
            "done" => L.T("Deployment.Phase.Done"),
            _ => update.Subject,
        };
    }
}
