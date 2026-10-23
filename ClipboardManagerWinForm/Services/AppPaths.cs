namespace ClipboardManagerWinForm.Services;

public static class AppPaths
{
    private const string ApplicationFolderName = "ClipboardManager";

    public static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationFolderName);

    public static string DatabaseFile => Path.Combine(DataFolder, "clipboard.db");
}
