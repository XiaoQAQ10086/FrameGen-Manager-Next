namespace FrameGenManagerNext.Domain.Ini;

/// <summary>
/// Applies typed overrides to an INI template while keeping stock values implicit.
/// </summary>
public static class IniMerger
{
    public static IReadOnlyList<IniChange> ApplyOverrides(
        IniDocument document,
        IEnumerable<IniSetting> settings,
        IEnumerable<IniSetting> stockValues)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(stockValues);

        var stock = new Dictionary<(string Section, string Key), string>(IniKeyComparer.Instance);
        foreach (var stockValue in stockValues)
        {
            ArgumentNullException.ThrowIfNull(stockValue);
            stock[(stockValue.Section, stockValue.Key)] = stockValue.Value;
        }

        var changes = new List<IniChange>();
        foreach (var setting in settings)
        {
            ArgumentNullException.ThrowIfNull(setting);

            var existing = document.GetValue(setting.Section, setting.Key);
            var hasStock = stock.TryGetValue((setting.Section, setting.Key), out var stockValue);

            if (existing is null && hasStock && string.Equals(setting.Value, stockValue, StringComparison.Ordinal))
            {
                changes.Add(new IniChange(setting.Section, setting.Key, null, setting.Value, Applied: false));
                continue;
            }

            var applied = existing is null || !string.Equals(existing, setting.Value, StringComparison.Ordinal);
            if (applied)
            {
                document.SetValue(setting.Section, setting.Key, setting.Value);
            }

            changes.Add(new IniChange(setting.Section, setting.Key, existing, setting.Value, applied));
        }

        return changes;
    }

    private sealed class IniKeyComparer : IEqualityComparer<(string Section, string Key)>
    {
        public static readonly IniKeyComparer Instance = new();

        public bool Equals((string Section, string Key) x, (string Section, string Key) y)
        {
            return string.Equals(x.Section, y.Section, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string Section, string Key) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Section),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key));
        }
    }
}
