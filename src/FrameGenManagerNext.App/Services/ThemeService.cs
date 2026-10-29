using System.Windows;
using System.Windows.Media;
using FrameGenManagerNext.Domain.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FrameGenManagerNext.App.Services;

public static class ThemeService
{
    private static readonly IReadOnlyDictionary<string, Color> LightPalette =
        new Dictionary<string, Color>
        {
            ["AppBackgroundBrush"] = Color.FromRgb(0xF6, 0xF8, 0xFB),
            ["SidebarBackgroundBrush"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["CardBackgroundBrush"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["CardBorderBrush"] = Color.FromRgb(0xDF, 0xE4, 0xEC),
            ["TextPrimaryBrush"] = Color.FromRgb(0x18, 0x1C, 0x24),
            ["TextSecondaryBrush"] = Color.FromRgb(0x56, 0x60, 0x70),
            ["AccentBrush"] = Color.FromRgb(0x00, 0x7A, 0xFF),
            ["AccentMutedBrush"] = Color.FromRgb(0xD9, 0xEB, 0xFF),
            ["WarningBrush"] = Color.FromRgb(0xB2, 0x5A, 0x00),
            ["GridHeaderBrush"] = Color.FromRgb(0xEE, 0xF2, 0xF8),
            ["RowAlternateBrush"] = Color.FromRgb(0xFA, 0xFB, 0xFD),
            ["NavHoverBrush"] = Color.FromRgb(0xEE, 0xF3, 0xF9),
            ["InputBackgroundBrush"] = Color.FromRgb(0xF7, 0xF9, 0xFC),
        };

    public static void Apply(AppTheme theme)
    {
        _ = theme;
        var application = Application.Current;
        if (application is not null)
        {
            foreach (var (key, color) in LightPalette)
            {
                application.Resources[key] = new SolidColorBrush(color);
            }
        }

        ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.None, true);
    }
}
