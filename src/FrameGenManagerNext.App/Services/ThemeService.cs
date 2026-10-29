using FrameGenManagerNext.Domain.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FrameGenManagerNext.App.Services;

public static class ThemeService
{
    public static void Apply(AppTheme theme)
    {
        var resolved = theme switch
        {
            AppTheme.Dark => ApplicationTheme.Dark,
            AppTheme.Light => ApplicationTheme.Light,
            _ => IsSystemDark() ? ApplicationTheme.Dark : ApplicationTheme.Light,
        };

        ApplicationThemeManager.Apply(resolved, WindowBackdropType.None, true);
    }

    private static bool IsSystemDark()
    {
        return ApplicationThemeManager.GetSystemTheme() switch
        {
            SystemTheme.Dark => true,
            SystemTheme.Light => false,
            _ => true,
        };
    }
}
