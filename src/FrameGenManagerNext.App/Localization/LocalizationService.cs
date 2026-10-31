using System.ComponentModel;
using System.Globalization;
using FrameGenManagerNext.Domain.Settings;

namespace FrameGenManagerNext.App.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Current { get; } = new();

    private AppLanguage _language = AppLanguage.ChineseSimplified;

    private LocalizationService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public AppLanguage Language => _language;

    public string this[string key] => Get(key);

    public void SetLanguage(AppLanguage language)
    {
        var effective = language == AppLanguage.System ? DetectSystemLanguage() : language;
        _language = effective;
        var culture = effective == AppLanguage.English
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.GetCultureInfo("zh-CN");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static AppLanguage DetectSystemLanguage()
    {
        var culture = CultureInfo.InstalledUICulture;
        if (string.IsNullOrWhiteSpace(culture.Name))
        {
            culture = CultureInfo.CurrentUICulture;
        }

        return string.Equals(culture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.ChineseSimplified
            : AppLanguage.English;
    }

    public IReadOnlyDictionary<string, string> Snapshot()
    {
        return StringCatalog.Entries.ToDictionary(
            pair => pair.Key,
            pair => _language == AppLanguage.English ? pair.Value.English : pair.Value.Chinese,
            StringComparer.Ordinal);
    }

    public string Get(string key)
    {
        if (!StringCatalog.Entries.TryGetValue(key, out var entry))
        {
            return key;
        }

        return _language == AppLanguage.English ? entry.English : entry.Chinese;
    }

    public string Format(string key, params object[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }
}

public static class L
{
    public static string T(string key)
    {
        return LocalizationService.Current.Get(key);
    }

    public static string F(string key, params object[] arguments)
    {
        return LocalizationService.Current.Format(key, arguments);
    }
}
