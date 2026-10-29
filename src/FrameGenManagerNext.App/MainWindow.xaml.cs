using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FrameGenManagerNext.App.ViewModels;
using FrameGenManagerNext.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace FrameGenManagerNext.App;

public partial class MainWindow : FluentWindow
{
    private readonly IServiceProvider _services;
    private readonly TranslateTransform _pageTransform = new();

    public MainWindow(IServiceProvider services, MainWindowViewModel viewModel)
    {
        _services = services;
        InitializeComponent();
        DataContext = viewModel;
        PageHost.RenderTransform = _pageTransform;
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
            "library" => _services.GetRequiredService<WebFrameGenPage>(),
            "smooth-motion" => _services.GetRequiredService<SmoothMotionPage>(),
            "dlss5" => _services.GetRequiredService<Dlss5Page>(),
            "logs" => _services.GetRequiredService<LogsPage>(),
            "settings" => _services.GetRequiredService<SettingsPage>(),
            "about" => _services.GetRequiredService<AboutPage>(),
            _ => null,
        };

        PageHost.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        _pageTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }
}
