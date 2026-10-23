namespace ClipboardManagerWinForm.Data;

public sealed class ClipEntry
{
    public long Id { get; init; }

    public required string Text { get; init; }

    public DateTime TimestampUtc { get; init; }

    public bool IsSticky { get; init; }

    public DateTime TimestampLocal => TimestampUtc.ToLocalTime();

    public string DisplayText => BuildSingleLine(Text);

    public static ClipEntry History(long id, string text, DateTime timestampUtc) => new()
    {
        Id = id,
        Text = text,
        TimestampUtc = timestampUtc
    };

    public static ClipEntry Sticky(long id, string text, DateTime timestampUtc) => new()
    {
        Id = id,
        Text = text,
        TimestampUtc = timestampUtc,
        IsSticky = true
    };

    private static string BuildSingleLine(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            builder.Append(character);
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }
}
