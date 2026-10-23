using ClipboardManagerWinForm.Services;

namespace ClipboardManagerWinForm.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder;

    public SettingsStoreTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ClipboardManagerTests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void SaveAndLoad_KeepsPasteMenuWidth()
    {
        var store = new SettingsStore(_folder);
        store.Settings.PasteMenuWidth = 520;
        store.Save();

        var reloaded = new SettingsStore(_folder);

        Assert.Equal(520, reloaded.Settings.PasteMenuWidth);
    }

    [Fact]
    public void SaveAndLoad_KeepsDefaultWidthWhenNotSet()
    {
        var store = new SettingsStore(_folder);
        store.Save();

        var reloaded = new SettingsStore(_folder);

        Assert.Equal(0, reloaded.Settings.PasteMenuWidth);
    }

    [Fact]
    public void CopyFrom_CopiesPasteMenuWidth()
    {
        var source = new AppSettings { PasteMenuWidth = 640 };
        var target = new AppSettings();

        target.CopyFrom(source);

        Assert.Equal(640, target.PasteMenuWidth);
    }
}
