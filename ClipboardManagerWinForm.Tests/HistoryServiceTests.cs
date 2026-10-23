using ClipboardManagerWinForm.Data;
using ClipboardManagerWinForm.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManagerWinForm.Tests;

public sealed class HistoryServiceTests : IDisposable
{
    private readonly string _folder;
    private readonly ClipboardRepository _repository;
    private readonly SettingsStore _settingsStore;
    private readonly HistoryService _service;

    public HistoryServiceTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ClipboardManagerTests", Guid.NewGuid().ToString("N"));
        var database = new Database(Path.Combine(_folder, "clipboard.db"));
        _repository = new ClipboardRepository(database);
        _settingsStore = new SettingsStore(_folder);
        _service = new HistoryService(_repository, _settingsStore);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

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
    public void Capture_StoresTextInHistoryAndArchive()
    {
        Assert.True(_service.Capture("Hello clipboard"));

        var history = _repository.GetHistory(10);
        Assert.Single(history);
        Assert.Equal("Hello clipboard", history[0].Text);
        Assert.Equal(1, _repository.CountArchive());
    }

    [Fact]
    public void Capture_IgnoresTextShorterThanConfiguredMinimum()
    {
        _settingsStore.Settings.MinTextLengthEnabled = true;
        _settingsStore.Settings.MinTextLength = 5;

        Assert.False(_service.Capture("abc"));
        Assert.Empty(_repository.GetHistory(10));
    }

    [Fact]
    public void Capture_IgnoresTextContainingConfiguredPatterns()
    {
        _settingsStore.Settings.IgnoreContainingEnabled = true;
        _settingsStore.Settings.IgnoreContaining = ";secret;token";

        Assert.False(_service.Capture("my secret value"));
        Assert.Empty(_repository.GetHistory(10));

        Assert.True(_service.Capture("public value"));
        Assert.Single(_repository.GetHistory(10));
    }

    [Fact]
    public void Capture_JoinsMultilineBrowserLinks()
    {
        _settingsStore.Settings.JoinBrowserLinks = true;

        Assert.True(_service.Capture("https://example.com/very\r\n/long/path"));

        var history = _repository.GetHistory(10);
        Assert.Equal("https://example.com/very/long/path", history[0].Text);
    }

    [Fact]
    public void Capture_DoesNothingWhenMonitoringIsDisabled()
    {
        _settingsStore.Settings.MonitorClipboard = false;

        Assert.False(_service.Capture("Hello clipboard"));
        Assert.Empty(_repository.GetHistory(10));
    }

    [Fact]
    public void Capture_SkipsArchiveForItemsAboveArchiveSizeLimit()
    {
        _settingsStore.Settings.ArchiveLimitSizeKb = 1;

        Assert.True(_service.Capture(new string('x', 2_000)));

        Assert.Single(_repository.GetHistory(10));
        Assert.Equal(0, _repository.CountArchive());
    }

    [Fact]
    public void Capture_TrimsArchiveWhenItemLimitIsConfigured()
    {
        _settingsStore.Settings.ArchiveLimitItems = 2;

        _service.Capture("first entry");
        _service.Capture("second entry");
        _service.Capture("third entry");

        var archive = _repository.GetArchive(10);

        Assert.Equal(2, archive.Count);
        Assert.Equal("third entry", archive[0].Text);
    }

    [Fact]
    public void Capture_RaisesTextCapturedEvent()
    {
        string? captured = null;
        _service.TextCaptured += (_, text) => captured = text;

        _service.Capture("event text");

        Assert.Equal("event text", captured);
    }
}
