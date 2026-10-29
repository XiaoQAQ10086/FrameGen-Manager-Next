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
            ["AppBackgroundBrush"] = Color.FromRgb(0x0F, 0x11, 0x15),
            ["SidebarBackgroundBrush"] = Color.FromRgb(0x15, 0x19, 0x22),
            ["CardBackgroundBrush"] = Color.FromRgb(0x1A, 0x1F, 0x2B),
            ["CardBorderBrush"] = Color.FromRgb(0x2A, 0x31, 0x42),
            ["TextPrimaryBrush"] = Color.FromRgb(0xE6, 0xE9, 0xEF),
            ["TextSecondaryBrush"] = Color.FromRgb(0xA0, 0xA8, 0xB8),
            ["AccentBrush"] = Color.FromRgb(0x4E, 0xA1, 0xFF),
            ["AccentMutedBrush"] = Color.FromRgb(0x2A, 0x4A, 0x6B),
            ["WarningBrush"] = Color.FromRgb(0xF0, 0xB4, 0x29),
            ["GridHeaderBrush"] = Color.FromRgb(0x20, 0x26, 0x36),
            ["RowAlternateBrush"] = Color.FromRgb(0x17, 0x1C, 0x26),
            ["NavHoverBrush"] = Color.FromRgb(0x20, 0x26, 0x34),
            ["InputBackgroundBrush"] = Color.FromRgb(0x20, 0x26, 0x33),
        };

    private static readonly IReadOnlyDictionary<string, Color> LightPalette =
        new Dictionary<string, Color>
        {
            ["AppBackgroundBrush"] = Color.FromRgb(0xF4, 0xF6, 0xF9),
            ["SidebarBackgroundBrush"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["CardBackgroundBrush"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["CardBorderBrush"] = Color.FromRgb(0xE0, 0xE5, 0xEE),
            ["TextPrimaryBrush"] = Color.FromRgb(0x1C, 0x23, 0x30),
            ["TextSecondaryBrush"] = Color.FromRgb(0x5B, 0x64, 0x75),
            ["AccentBrush"] = Color.FromRgb(0x0A, 0x66, 0xC2),
            ["AccentMutedBrush"] = Color.FromRgb(0xD6, 0xE6, 0xF8),
            ["WarningBrush"] = Color.FromRgb(0xB2, 0x6A, 0x00),
            ["GridHeaderBrush"] = Color.FromRgb(0xEC, 0xF0, 0xF6),
            ["RowAlternateBrush"] = Color.FromRgb(0xF8, 0xFA, 0xFC),
            ["NavHoverBrush"] = Color.FromRgb(0xEE, 0xF2, 0xF8),
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
