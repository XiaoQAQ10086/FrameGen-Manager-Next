namespace FrameGenManagerNext.Infrastructure.Library;

/// <summary>
/// Minimal Valve KeyValues (VDF) parser for Steam libraryfolders.vdf and appmanifest files.
/// </summary>
public sealed class ValveKeyValues
{
    private readonly Dictionary<string, object> _values;

    private ValveKeyValues(Dictionary<string, object> values)
    {
        _values = values;
    }

    public IReadOnlyDictionary<string, object> Values => _values;

    public static ValveKeyValues Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parser = new Parser(text);
        return new ValveKeyValues(parser.ParseObject(isRoot: true));
    }

    public IEnumerable<string> FindStringValues(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return FindStringValues(_values, key);
    }

    private static IEnumerable<string> FindStringValues(
        Dictionary<string, object> values,
        string key)
    {
        foreach (var pair in values)
        {
            if (pair.Value is string text)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    yield return text;
                }

                continue;
            }

            if (pair.Value is ValveKeyValues nested)
            {
                foreach (var value in FindStringValues(nested._values, key))
                {
                    yield return value;
                }
            }
        }
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _index;

        public Parser(string text)
        {
            _text = text;
        }

        public Dictionary<string, object> ParseObject(bool isRoot)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                SkipTrivia();
                if (_index >= _text.Length)
                {
                    if (isRoot)
                    {
                        return values;
                    }

                    throw new FormatException("Unexpected end of VDF object.");
                }

                if (_text[_index] == '}')
                {
                    if (isRoot)
                    {
                        throw new FormatException("Unexpected closing brace.");
                    }

                    _index++;
                    return values;
                }

                var key = ReadToken();
                SkipTrivia();

                if (_index < _text.Length && _text[_index] == '{')
                {
                    _index++;
                    values[key] = new ValveKeyValues(ParseObject(isRoot: false));
                }
                else
                {
                    values[key] = ReadToken();
                }
            }
        }

        private string ReadToken()
        {
            SkipTrivia();
            if (_index >= _text.Length)
            {
                throw new FormatException("Unexpected end of VDF token.");
            }

            if (_text[_index] == '"')
            {
                return ReadQuoted();
            }

            var start = _index;
            while (_index < _text.Length &&
                   !char.IsWhiteSpace(_text[_index]) &&
                   _text[_index] is not '{' and not '}')
            {
                _index++;
            }

            if (start == _index)
            {
                throw new FormatException("Empty VDF token.");
            }

            return _text[start.._index];
        }

        private string ReadQuoted()
        {
            _index++;
            var builder = new System.Text.StringBuilder();

            while (_index < _text.Length)
            {
                var character = _text[_index++];
                if (character == '"')
                {
                    return builder.ToString();
                }

                if (character != '\\' || _index >= _text.Length)
                {
                    builder.Append(character);
                    continue;
                }

                var escaped = _text[_index++];
                builder.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    _ => escaped,
                });
            }

            throw new FormatException("Unterminated VDF string.");
        }

        private void SkipTrivia()
        {
            while (_index < _text.Length)
            {
                if (char.IsWhiteSpace(_text[_index]))
                {
                    _index++;
                    continue;
                }

                if (_text[_index] == '/' && _index + 1 < _text.Length && _text[_index + 1] == '/')
                {
                    _index += 2;
                    while (_index < _text.Length && _text[_index] is not '\r' and not '\n')
                    {
                        _index++;
                    }

                    continue;
                }

                break;
            }
        }
    }
}
