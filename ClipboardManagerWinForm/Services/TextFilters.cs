using System.Text;

namespace ClipboardManagerWinForm.Services;

public static class TextFilters
{
    private static readonly string[] UrlPrefixes = ["http://", "https://", "ftp://", "www.", "file://"];

    public static bool TryJoinBrowserLink(string text, out string joined)
    {
        joined = text;

        var lines = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();

        if (lines.Length < 2)
        {
            return false;
        }

        if (lines.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        if (!UrlPrefixes.Any(prefix => lines[0].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var candidate = string.Concat(lines);
        if (candidate.Length == 0 || candidate.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || !uri.IsAbsoluteUri)
        {
            return false;
        }

        joined = candidate;
        return true;
    }

    public static IReadOnlyList<string> SplitPatterns(string? patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns))
        {
            return [];
        }

        return patterns
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pattern => pattern.Length > 0)
            .ToArray();
    }

    public static bool ContainsIgnoredText(string text, string? patterns, bool enabled)
    {
        if (!enabled)
        {
            return false;
        }

        foreach (var pattern in SplitPatterns(patterns))
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static int GetByteCount(string text) => Encoding.UTF8.GetByteCount(text);

    /// <summary>
    ///  Turns copied text into pure text: no-break spaces become normal spaces, invisible "formatting"
    ///  characters are dropped and every line break becomes a CR/LF pair.
    /// </summary>
    public static string ToPlainText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var plain = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            switch (character)
            {
                case '\r':
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    plain.Append("\r\n");
                    break;

                case '\n':
                    plain.Append("\r\n");
                    break;

                // Copied no-break spaces glue words together, so they are pasted as regular spaces.
                case '\u00A0':
                case '\u2007':
                case '\u202F':
                    plain.Append(' ');
                    break;

                // Zero width spaces, joiners, the byte order mark and soft hyphens are invisible leftovers.
                case '\u200B':
                case '\u200C':
                case '\u200D':
                case '\u2060':
                case '\uFEFF':
                case '\u00AD':
                    break;

                default:
                    // Control characters carry no text, only a tab is kept.
                    if (!char.IsControl(character) || character == '\t')
                    {
                        plain.Append(character);
                    }

                    break;
            }
        }

        return plain.ToString();
    }

    public static string CreateTooltip(string? text, int maxLength = 63)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Clipboard Manager";
        }

        var singleLine = new StringBuilder();
        var lastWasSpace = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                if (!lastWasSpace && singleLine.Length > 0)
                {
                    singleLine.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            singleLine.Append(character);
            lastWasSpace = false;

            if (singleLine.Length >= maxLength)
            {
                break;
            }
        }

        var result = singleLine.ToString().TrimEnd();
        return result.Length == 0 ? "Clipboard Manager" : result;
    }

    public static string Summarize(string text, int maxLength)
    {
        var singleLine = text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace('\t', ' ');

        while (singleLine.Contains("  ", StringComparison.Ordinal))
        {
            singleLine = singleLine.Replace("  ", " ", StringComparison.Ordinal);
        }

        singleLine = singleLine.Trim();

        return singleLine.Length <= maxLength
            ? singleLine
            : singleLine[..Math.Max(0, maxLength - 1)] + "…";
    }
}
