using System.Windows.Controls;
using FrameGenManagerNext.App.ViewModels;
using FrameGenManagerNext.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace FrameGenManagerNext.App;

public partial class MainWindow : FluentWindow
{
    private readonly IServiceProvider _services;

    public MainWindow(IServiceProvider services, MainWindowViewModel viewModel)
    {
        _services = services;
        InitializeComponent();
        DataContext = viewModel;
        NavList.SelectedIndex = 0;
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        PageHost.Content = tag switch
        {
            "library" => _services.GetRequiredService<LibraryPage>(),
            "gpu" => _services.GetRequiredService<GpuPage>(),
            "smooth-motion" => _services.GetRequiredService<SmoothMotionPage>(),
            "logs" => _services.GetRequiredService<LogsPage>(),
            "about" => _services.GetRequiredService<AboutPage>(),
            _ => null,
        };
    }
}
