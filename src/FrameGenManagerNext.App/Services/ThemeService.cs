using System.Windows;
using System.Windows.Media;
using FrameGenManagerNext.Domain.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FrameGenManagerNext.App.Services;

public static class ThemeService
{
    private static readonly IReadOnlyDictionary<string, Color> DarkPalette =
        new Dictionary<string, Color>
        {
            ["AppBackgroundBrush"] = Color.FromRgb(0x0A, 0x0A, 0x0C),
            ["SidebarBackgroundBrush"] = Color.FromRgb(0x12, 0x12, 0x16),
            ["CardBackgroundBrush"] = Color.FromRgb(0x1A, 0x1A, 0x1F),
            ["CardBorderBrush"] = Color.FromRgb(0x2C, 0x2C, 0x33),
            ["TextPrimaryBrush"] = Color.FromRgb(0xF5, 0xF5, 0xF7),
            ["TextSecondaryBrush"] = Color.FromRgb(0x98, 0x98, 0x9F),
            ["AccentBrush"] = Color.FromRgb(0x0A, 0x84, 0xFF),
            ["AccentMutedBrush"] = Color.FromRgb(0x16, 0x32, 0x4F),
            ["WarningBrush"] = Color.FromRgb(0xFF, 0x9F, 0x0A),
            ["GridHeaderBrush"] = Color.FromRgb(0x21, 0x21, 0x27),
            ["RowAlternateBrush"] = Color.FromRgb(0x17, 0x17, 0x1C),
            ["NavHoverBrush"] = Color.FromRgb(0x24, 0x24, 0x2B),
            ["InputBackgroundBrush"] = Color.FromRgb(0x1F, 0x1F, 0x25),
        };

    private static readonly IReadOnlyDictionary<string, Color> LightPalette =
        new Dictionary<string, Color>
        {
            ["AppBackgroundBrush"] = Color.FromRgb(0xF5, 0xF5, 0xF7),
            ["SidebarBackgroundBrush"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["CardBackgroundBrush"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["CardBorderBrush"] = Color.FromRgb(0xE5, 0xE5, 0xEA),
            ["TextPrimaryBrush"] = Color.FromRgb(0x1D, 0x1D, 0x1F),
            ["TextSecondaryBrush"] = Color.FromRgb(0x6E, 0x6E, 0x73),
            ["AccentBrush"] = Color.FromRgb(0x00, 0x7A, 0xFF),
            ["AccentMutedBrush"] = Color.FromRgb(0xD6, 0xE9, 0xFF),
            ["WarningBrush"] = Color.FromRgb(0xC9, 0x34, 0x00),
            ["GridHeaderBrush"] = Color.FromRgb(0xF2, 0xF2, 0xF7),
            ["RowAlternateBrush"] = Color.FromRgb(0xFA, 0xFA, 0xFC),
            ["NavHoverBrush"] = Color.FromRgb(0xF0, 0xF0, 0xF4),
            ["InputBackgroundBrush"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
        };

    public static void Apply(AppTheme theme)
    {
        var resolved = theme switch
        {
            AppTheme.Dark => ApplicationTheme.Dark,
            AppTheme.Light => ApplicationTheme.Light,
            _ => IsSystemDark() ? ApplicationTheme.Dark : ApplicationTheme.Light,
        };

        ApplyPalette(resolved == ApplicationTheme.Dark ? DarkPalette : LightPalette);
        ApplicationThemeManager.Apply(resolved, WindowBackdropType.None, true);
    }

    private static void ApplyPalette(IReadOnlyDictionary<string, Color> palette)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        foreach (var (key, color) in palette)
        {
            application.Resources[key] = new SolidColorBrush(color);
        }
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
