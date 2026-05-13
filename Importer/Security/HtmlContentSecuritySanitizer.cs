namespace Importer.Security;

/// <summary>
/// Makes content pass <see cref="HtmlContentValidator"/> by escaping the opening '&lt;' of the first invalid tag, repeatedly until valid.
/// </summary>
internal static class HtmlContentSecuritySanitizer
{
    public static string SanitizeToPassValidator(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var current = input;
        var guard = 0;
        var max = current.Length + 32;
        while (!HtmlContentValidator.IsValid(current) && guard++ < max)
        {
            var fixAt = HtmlContentValidator.FindFirstInvalidOpenBracketIndex(current);
            if (fixAt < 0)
                return current;

            current = string.Concat(current.AsSpan(0, fixAt), "&lt;", current.AsSpan(fixAt + 1));
        }

        return current;
    }
}
