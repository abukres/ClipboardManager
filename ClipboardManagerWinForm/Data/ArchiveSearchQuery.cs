namespace ClipboardManagerWinForm.Data;

public sealed class ArchiveSearchQuery
{
    public static readonly ArchiveSearchQuery Empty = new([], []);

    private ArchiveSearchQuery(IReadOnlyList<string> terms, IReadOnlyList<string> excludedTerms)
    {
        Terms = terms;
        ExcludedTerms = excludedTerms;
    }

    public IReadOnlyList<string> Terms { get; }

    public IReadOnlyList<string> ExcludedTerms { get; }

    public bool IsEmpty => Terms.Count == 0 && ExcludedTerms.Count == 0;

    public static ArchiveSearchQuery Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Empty;
        }

        List<string> terms = [];
        List<string> excluded = [];

        foreach (var token in Tokenize(input))
        {
            if (token.Length == 0)
            {
                continue;
            }

            if (token.Length > 1 && token[0] == '-')
            {
                var excludedTerm = token[1..].Trim();
                if (excludedTerm.Length > 0)
                {
                    excluded.Add(excludedTerm);
                }

                continue;
            }

            var term = token.Trim();
            if (term.Length > 0)
            {
                terms.Add(term);
            }
        }

        return new ArchiveSearchQuery(terms, excluded);
    }

    private static IEnumerable<string> Tokenize(string input)
    {
        var buffer = new System.Text.StringBuilder();
        var tokenStarted = false;
        var inQuotes = false;

        foreach (var character in input)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                tokenStarted = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(character))
            {
                if (tokenStarted)
                {
                    yield return buffer.ToString();
                    buffer.Clear();
                    tokenStarted = false;
                }

                continue;
            }

            buffer.Append(character);
            tokenStarted = true;
        }

        if (tokenStarted)
        {
            yield return buffer.ToString();
        }
    }
}
