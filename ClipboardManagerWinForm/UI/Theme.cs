namespace ClipboardManagerWinForm.UI;

public static class Theme
{
    public static readonly Font BodyFont = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

    public static readonly Font BoldFont = new("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);

    public static Color MenuBackground => SystemColors.Window;

    public static Color MenuBorder => SystemColors.ControlDarkDark;

    public static Color MenuHighlight => SystemColors.Highlight;

    public static Color SeparatorColor => SystemColors.ControlDark;

    public static Icon ApplicationIcon { get; } = LoadApplicationIcon();

    private static Icon LoadApplicationIcon()
    {
        using var stream = typeof(Theme).Assembly.GetManifestResourceStream("app.ico");
        if (stream is null)
        {
            return SystemIcons.Application;
        }

        return new Icon(stream);
    }
}
