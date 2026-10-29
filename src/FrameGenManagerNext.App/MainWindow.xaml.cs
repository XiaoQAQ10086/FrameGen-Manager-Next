using FrameGenManagerNext.App.ViewModels;
using Wpf.Ui.Controls;

namespace FrameGenManagerNext.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
