using System.Windows.Controls;
using FrameGenManagerNext.App.ViewModels;

namespace FrameGenManagerNext.App.Views;

public partial class SmoothMotionPage : UserControl
{
    public SmoothMotionPage(SmoothMotionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
