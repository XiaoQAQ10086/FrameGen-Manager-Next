using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FrameGenManagerNext.App.ViewModels;

namespace FrameGenManagerNext.App.Views;

public partial class LibraryPage : UserControl
{
    private readonly LibraryViewModel _viewModel;

    public LibraryPage(LibraryViewModel viewModel, GpuViewModel gpuViewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        GpuPanel.DataContext = gpuViewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += async (_, _) =>
        {
            await viewModel.LoadAsync();
            await gpuViewModel.LoadAsync();
        };
        Unloaded += (_, _) =>
        {
            if (_viewModel.IsDeployDialogOpen)
            {
                _viewModel.CloseDeployDialogCommand.Execute(null);
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.IsDeployDialogOpen) &&
            _viewModel.IsDeployDialogOpen)
        {
            Dispatcher.BeginInvoke(AnimateDeployDialog, DispatcherPriority.Loaded);
        }
    }

    private void AnimateDeployDialog()
    {
        DeployOverlay.Opacity = 0;
        DeployCardScale.ScaleX = 0.96;
        DeployCardScale.ScaleY = 0.96;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        DeployOverlay.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        DeployCardScale.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        DeployCardScale.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
    }
}
