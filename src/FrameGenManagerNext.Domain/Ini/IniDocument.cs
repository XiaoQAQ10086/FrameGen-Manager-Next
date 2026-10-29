namespace FrameGenManagerNext.Domain.Ini;

/// <summary>
/// Line-preserving INI editor. Sections and keys are case-insensitive; values are kept verbatim.
/// Inline comments are not parsed, so values containing ';' or '#' survive round-trips.
/// </summary>
public sealed class IniDocument
{
    private readonly List<IniLine> _lines;

    private IniDocument(List<IniLine> lines)
    {
        _lines = lines;
    }

    public static IniDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var rawLines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();

        if (rawLines.Count > 0 && rawLines[^1].Length == 0)
        {
            rawLines.RemoveAt(rawLines.Count - 1);
        }

        var lines = new List<IniLine>(rawLines.Count);
        foreach (var rawLine in rawLines)
        {
            lines.Add(ParseLine(rawLine));
        }

        return new IniDocument(lines);
    }

    public IReadOnlyList<string> SectionNames
    {
        get
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in _lines)
            {
                if (line is SectionLine section && seen.Add(section.Name))
                {
                    names.Add(section.Name);
                }
            }

            return names;
        }
    }

    public string? GetValue(string section, string key)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(key);

        string? value = null;
        var currentSection = string.Empty;

        foreach (var line in _lines)
        {
            switch (line)
            {
                case SectionLine sectionLine:
                    currentSection = sectionLine.Name;
                    break;
                case KeyValueLine keyValueLine when
                    string.Equals(currentSection, section, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(keyValueLine.Key, key, StringComparison.OrdinalIgnoreCase):
                    value = keyValueLine.Value;
                    break;
            }
        }

        return value;
    }

    public void SetValue(string section, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var currentSection = string.Empty;
        var lastMatchingKeyIndex = -1;
        var lastMatchingSectionIndex = section.Length == 0 ? 0 : -1;

        for (var index = 0; index < _lines.Count; index++)
        {
            switch (_lines[index])
            {
                case SectionLine sectionLine:
                    currentSection = sectionLine.Name;
                    if (string.Equals(sectionLine.Name, section, StringComparison.OrdinalIgnoreCase))
                    {
                        lastMatchingSectionIndex = index;
                    }

                    break;
                case KeyValueLine keyValueLine when
                    string.Equals(currentSection, section, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(keyValueLine.Key, key, StringComparison.OrdinalIgnoreCase):
                    lastMatchingKeyIndex = index;
                    break;
            }
        }

        if (lastMatchingKeyIndex >= 0)
        {
            ((KeyValueLine)_lines[lastMatchingKeyIndex]).Value = value;
            return;
        }

        if (lastMatchingSectionIndex >= 0)
        {
            var insertIndex = FindSectionEnd(lastMatchingSectionIndex);
            _lines.Insert(insertIndex, new KeyValueLine(key, value));
            return;
        }

        if (_lines.Count > 0 && _lines[^1] is not RawLine { Text.Length: 0 })
        {
            _lines.Add(new RawLine(string.Empty));
        }

        if (section.Length > 0)
        {
            _lines.Add(new SectionLine(section));
        }

        _lines.Add(new KeyValueLine(key, value));
    }

    public int RemoveValue(string section, string key)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(key);

        var currentSection = string.Empty;
        var matches = new List<int>();

        for (var index = 0; index < _lines.Count; index++)
        {
            switch (_lines[index])
            {
                case SectionLine sectionLine:
                    currentSection = sectionLine.Name;
                    break;
                case KeyValueLine keyValueLine when
                    string.Equals(currentSection, section, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(keyValueLine.Key, key, StringComparison.OrdinalIgnoreCase):
                    matches.Add(index);
                    break;
            }
        }

        for (var index = matches.Count - 1; index >= 0; index--)
        {
            _lines.RemoveAt(matches[index]);
        }

        return matches.Count;
    }

    public string ToText(string newLine = "\r\n")
    {
        ArgumentNullException.ThrowIfNull(newLine);
        if (newLine.Length == 0)
        {
            throw new ArgumentException("Line ending must not be empty.", nameof(newLine));
        }

        if (_lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var line in _lines)
        {
            builder.Append(line switch
            {
                RawLine raw => raw.Text,
                SectionLine section => $"[{section.Name}]",
                KeyValueLine keyValue => $"{keyValue.Key}={keyValue.Value}",
                _ => string.Empty,
            });
            builder.Append(newLine);
        }

        return builder.ToString();
    }

    private static IniLine ParseLine(string line)
    {
        var trimmed = line.Trim();

        if (trimmed.Length == 0 || trimmed[0] is ';' or '#')
        {
            return new RawLine(line);
        }

        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            return new SectionLine(trimmed[1..^1].Trim());
        }

        var equalsIndex = line.IndexOf('=');
        if (equalsIndex > 0)
        {
            var key = line[..equalsIndex].Trim();
            if (key.Length > 0)
            {
                return new KeyValueLine(key, line[(equalsIndex + 1)..].Trim());
            }
        }

        return new RawLine(line);
    }

    private int FindSectionEnd(int sectionIndex)
    {
        for (var index = sectionIndex + 1; index < _lines.Count; index++)
        {
            if (_lines[index] is SectionLine)
            {
                return index;
            }
        }

        return _lines.Count;
    }

    private abstract class IniLine;

    private sealed class RawLine(string text) : IniLine
    {
        public string Text { get; } = text;
    }

    private sealed class SectionLine(string name) : IniLine
    {
        public string Name { get; } = name;
    }

    private sealed class KeyValueLine(string key, string value) : IniLine
    {
        public string Key { get; } = key;

        public string Value { get; set; } = value;
    }
}
