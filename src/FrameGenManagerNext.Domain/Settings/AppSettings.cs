namespace FrameGenManagerNext.Domain.Settings;

public enum AppLanguage
{
    ChineseSimplified,
    English,
    System,
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
        SchemaVersion: 2,
        Language: AppLanguage.System,
        Theme: AppTheme.Light,
        AutoScanOnStartup: false,
        ShowCovers: true);
}
