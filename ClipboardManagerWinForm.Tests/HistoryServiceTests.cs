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
    public void GetMoreHistoryEntries_ReturnsOnlyOlderArchiveEntriesNewestFirst()
    {
        _settingsStore.Settings.HistoryDepth = 2;

        _service.Capture("first entry");
        _service.Capture("second entry");
        _service.Capture("third entry");

        var entries = _service.GetMoreHistoryEntries();

        // "third entry" and "second entry" are already part of the history, so they are skipped.
        Assert.Single(entries);
        Assert.Equal("first entry", entries[0].Text);
        Assert.All(entries, entry => Assert.False(entry.IsSticky));
    }

    [Fact]
    public void GetMoreHistoryEntries_SkipsAllClipsWhileHistoryIsEmpty()
    {
        _service.Capture("only entry");

        // The clip is still part of the history, so no older clips are left.
        Assert.Empty(_service.GetMoreHistoryEntries());

        _repository.ClearHistory();

        var entries = _service.GetMoreHistoryEntries();

        Assert.Single(entries);
        Assert.Equal("only entry", entries[0].Text);
    }

    [Fact]
    public void GetMoreHistoryEntries_ReturnsNothingWhenArchiveIsDisabled()
    {
        _settingsStore.Settings.AddToArchive = false;

        _service.Capture("only entry");

        Assert.Empty(_service.GetMoreHistoryEntries());
    }

    [Fact]
    public void Reuse_MovesClipToTheFirstHistoryPosition()
    {
        _service.Capture("first entry");
        _service.Capture("second entry");
        _service.Capture("third entry");

        _service.Reuse("first entry");

        var history = _repository.GetHistory(10);

        Assert.Equal(3, history.Count);
        Assert.Equal("first entry", history[0].Text);
        Assert.Single(history, entry => entry.Text == "first entry");
    }

    [Fact]
    public void Reuse_AddsClipToHistoryWhenItIsNotPartOfItAnymore()
    {
        _service.Capture("first entry");
        _service.Capture("second entry");
        _repository.ClearHistory();

        _service.Reuse("first entry");

        var history = _repository.GetHistory(10);

        Assert.Single(history);
        Assert.Equal("first entry", history[0].Text);
    }

    [Fact]
    public void Reuse_TrimsHistoryToTheConfiguredDepth()
    {
        _settingsStore.Settings.HistoryDepth = 2;

        _service.Capture("first entry");
        _service.Capture("second entry");

        _service.Reuse("third entry");

        var history = _repository.GetHistory(10);

        Assert.Equal(2, history.Count);
        Assert.Equal("third entry", history[0].Text);
    }

    [Fact]
    public void Reuse_IgnoresTheCaptureFilters()
    {
        _settingsStore.Settings.MonitorClipboard = false;
        _settingsStore.Settings.MinTextLengthEnabled = true;
        _settingsStore.Settings.MinTextLength = 10;

        _service.Reuse("abc");

        var history = _repository.GetHistory(10);

        Assert.Single(history);
        Assert.Equal("abc", history[0].Text);
    }

    [Fact]
    public void Reuse_DoesNotRaiseTextCapturedEvent()
    {
        var raised = 0;
        _service.TextCaptured += (_, _) => raised++;

        _service.Reuse("reused text");

        Assert.Equal(0, raised);
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
