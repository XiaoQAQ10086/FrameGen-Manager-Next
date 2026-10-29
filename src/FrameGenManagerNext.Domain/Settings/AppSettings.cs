namespace FrameGenManagerNext.Domain.Settings;

public enum AppLanguage
{
    ChineseSimplified,
    English,
}

public enum AppTheme
{
    System,
    Dark,
    Light,
}

public sealed record AppSettings(
    int SchemaVersion,
    AppLanguage Language,
    AppTheme Theme,
    bool AutoScanOnStartup,
    bool ShowCovers)
{
    public static AppSettings Default { get; } = new(
        SchemaVersion: 1,
        Language: AppLanguage.ChineseSimplified,
        Theme: AppTheme.Light,
        AutoScanOnStartup: false,
        ShowCovers: true);
}
