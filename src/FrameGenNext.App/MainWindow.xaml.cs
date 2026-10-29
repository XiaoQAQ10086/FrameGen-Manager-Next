using FrameGenNext.App.ViewModels;
using Wpf.Ui.Controls;

namespace FrameGenNext.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
