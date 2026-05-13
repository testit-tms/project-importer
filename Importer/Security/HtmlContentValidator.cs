namespace Importer.Security;

/// <summary>
/// Mirrors Test IT HTML content rules: insecure tag names, event-handler attribute names, javascript: URLs in attribute values.
/// </summary>
internal static class HtmlContentValidator
{
    private const string AllowedSymbols = "-_:";

    private static readonly string[] InsecureTags =
    [
        "applet", "audio", "base", "canvas", "dialog", "embed", "frame", "frameset", "iframe", "link",
        "noscript", "object", "param", "script", "source", "style", "track", "video"
    ];

    private static readonly string[] InsecureAttributes =
    [
        "onabort", "onafterprint", "onbeforeprint", "onbeforeunload", "onblur", "oncanplay", "oncanplaythrough",
        "onchange", "onclick", "oncontextmenu", "oncopy", "oncuechange", "oncut", "ondblclick", "ondrag",
        "ondragend", "ondragenter", "ondragleave", "ondragover", "ondragstart", "ondrop", "ondurationchange",
        "onemptied", "onended", "onerror", "onfocus", "onhashchange", "oninput", "oninvalid", "onkeydown",
        "onkeypress", "onkeyup", "onload", "onloadeddata", "onloadedmetadata", "onloadstart", "onmousedown",
        "onmousemove", "onmouseout", "onmouseover", "onmouseup", "onmousewheel", "onoffline", "ononline",
        "onpagehide", "onpageshow", "onpaste", "onpause", "onplay", "onplaying", "onpopstate", "onprogress",
        "onratechange", "onreset", "onresize", "onscroll", "onsearch", "onseeked", "onseeking", "onselect",
        "onstalled", "onstorage", "onsubmit", "onsuspend", "ontimeupdate", "ontoggle", "onunload", "onvolumechange",
        "onwaiting", "onwheel",
    ];

    private enum EscapeQuotes
    {
        None,
        Single,
        Double
    }

    public static bool IsValid(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        var v = new Scanner(text);
        while (v.MoveNext())
        {
            if (v.Current != '<')
                continue;
            if (!v.ValidateTagFromOpenBracket())
                return false;
        }

        return true;
    }

    /// <summary>Returns index of '&lt;' that opens the first construct rejected by the validator, or -1 if valid.</summary>
    public static int FindFirstInvalidOpenBracketIndex(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '<')
                continue;
            if (!Scanner.TryValidateTagOpeningAt(text, i))
                return i;
        }

        return -1;
    }

    private sealed class Scanner
    {
        private readonly string _text;
        private int _index;
        private char _current;

        private Scanner(string text, int index, char current)
        {
            _text = text;
            _index = index;
            _current = current;
        }

        public Scanner(string text)
            : this(text, -1, '\0')
        {
        }

        public static bool TryValidateTagOpeningAt(string text, int openBracketIndex)
        {
            if ((uint)openBracketIndex >= (uint)text.Length || text[openBracketIndex] != '<')
                return true;

            var v = new Scanner(text, openBracketIndex, '<');
            return v.ValidateTagFromOpenBracket();
        }

        private bool CanMoveNext => _index < _text.Length;

        public bool MoveNext()
        {
            if (++_index < _text.Length)
            {
                _current = _text[_index];
                return true;
            }

            _current = '\0';
            return false;
        }

        public char Current => _current;

        public bool ValidateTagFromOpenBracket()
        {
            if (!MoveNext())
                return true;

            if (_current == '/')
                return true;

            if (!IsAllowedSymbol(_current))
                return true;

            if (!ValidateName(InsecureTags))
                return false;

            return ValidateAttributes();
        }

        private bool ValidateName(string[] disallowed)
        {
            var start = _index;

            do
            {
                if (!IsAllowedSymbol(_current))
                    break;
            } while (MoveNext());

            if (!CanMoveNext)
                return true;

            var name = _text.AsSpan(start, _index - start);
            return name.IsEmpty || IsAllowed(name, disallowed);
        }

        private bool ValidateAttributeValue()
        {
            if (!MoveNext())
                return true;

            var start = _index;
            var escaping = EscapeQuotes.None;

            switch (_current)
            {
                case '\'':
                    escaping = EscapeQuotes.Single;
                    start = _index + 1;
                    break;

                case '"':
                    escaping = EscapeQuotes.Double;
                    start = _index + 1;
                    break;

                case var ch when !IsAllowedSymbol(ch):
                    return true;
            }

            while (MoveNext())
            {
                if (escaping == EscapeQuotes.Single && _current == '\'')
                    break;

                if (escaping == EscapeQuotes.Double && _current == '"')
                    break;

                if (escaping == EscapeQuotes.None && !IsAllowedSymbol(_current))
                    break;
            }

            if (!CanMoveNext)
                return true;

            var endExclusive = escaping switch
            {
                EscapeQuotes.None => _index,
                EscapeQuotes.Single or EscapeQuotes.Double => int.Max(start, _index - 1),
                _ => throw new ArgumentOutOfRangeException(nameof(escaping))
            };

            return IsAllowedAttributeValue(_text.AsSpan(start, endExclusive - start));
        }

        private bool ValidateAttributes()
        {
            if (CanMoveNext && _current == '>')
                return true;

            while (MoveNext())
            {
                if (_current == '>')
                    return true;

                if (!IsAllowedSymbol(_current))
                    continue;

                if (!ValidateName(InsecureAttributes))
                    return false;

                if (_current != '=')
                    continue;

                if (!ValidateAttributeValue())
                    return false;
            }

            return true;
        }

        private static bool IsAllowedSymbol(char ch) =>
            char.IsLetterOrDigit(ch) || AllowedSymbols.Contains(ch);

        private static bool IsAllowed(ReadOnlySpan<char> segment, string[] disallowed)
        {
            foreach (var disallowedItem in disallowed)
            {
                if (segment.Equals(disallowedItem, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool IsAllowedAttributeValue(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
                return true;

            return !value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
