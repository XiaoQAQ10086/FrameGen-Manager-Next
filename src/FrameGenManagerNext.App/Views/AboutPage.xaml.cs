using System.Windows.Controls;
using FrameGenManagerNext.App.ViewModels;

namespace FrameGenManagerNext.App.Views;

public partial class AboutPage : UserControl
{
    public AboutPage(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
