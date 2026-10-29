using System.Windows.Controls;
using FrameGenManagerNext.App.ViewModels;

namespace FrameGenManagerNext.App.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
