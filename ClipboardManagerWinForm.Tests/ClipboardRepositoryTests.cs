using ClipboardManagerWinForm.Data;
using Microsoft.Data.Sqlite;

namespace ClipboardManagerWinForm.Tests;

public sealed class ClipboardRepositoryTests : IDisposable
{
    private readonly string _folder;
    private readonly ClipboardRepository _repository;

    public ClipboardRepositoryTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ClipboardManagerTests", Guid.NewGuid().ToString("N"));
        var database = new Database(Path.Combine(_folder, "clipboard.db"));
        _repository = new ClipboardRepository(database);
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
    public void AddHistory_DeduplicatesConsecutiveEntries()
    {
        var now = DateTime.UtcNow;

        _repository.AddHistory("same text", now, 10);
        _repository.AddHistory("same text", now.AddSeconds(1), 10);

        var history = _repository.GetHistory(10);

        Assert.Single(history);
        Assert.Equal("same text", history[0].Text);
    }

    [Fact]
    public void AddHistory_KeepsDistinctEntriesNewestFirst()
    {
        var now = DateTime.UtcNow;

        _repository.AddHistory("first", now, 10);
        _repository.AddHistory("second", now.AddSeconds(1), 10);

        var history = _repository.GetHistory(10);

        Assert.Equal(2, history.Count);
        Assert.Equal("second", history[0].Text);
        Assert.Equal("first", history[1].Text);
    }

    [Fact]
    public void AddHistory_TrimsHistoryToConfiguredDepth()
    {
        var now = DateTime.UtcNow;

        for (var index = 0; index < 5; index++)
        {
            _repository.AddHistory($"entry {index}", now.AddSeconds(index), 3);
        }

        var history = _repository.GetHistory(10);

        Assert.Equal(3, history.Count);
        Assert.Equal("entry 4", history[0].Text);
        Assert.Equal("entry 2", history[2].Text);
    }

    [Fact]
    public void SearchArchive_MatchesAllKeywordsAndExcludesTerms()
    {
        var now = DateTime.UtcNow;

        _repository.AddArchive(
            "Unicode is intended to address the need for a workable, reliable world text encoding.",
            now.AddMinutes(-3));
        _repository.AddArchive(
            "As of June 2015, the most recent version is Unicode 8.0, the industry standard.",
            now.AddMinutes(-2));
        _repository.AddArchive(
            "Clipboard managers are applications that enable users to collect and re-use text.",
            now.AddMinutes(-1));

        var results = _repository.SearchArchive(ArchiveSearchQuery.Parse("unicode -industry"), null, 100);

        Assert.Single(results);
        Assert.StartsWith("Unicode is intended", results[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchArchive_IsCaseInsensitive()
    {
        _repository.AddArchive("Mixed Case CONTENT", DateTime.UtcNow);

        var results = _repository.SearchArchive(ArchiveSearchQuery.Parse("mixed content"), null, 100);

        Assert.Single(results);
    }

    [Fact]
    public void SearchArchive_FiltersByDateRange()
    {
        var now = DateTime.UtcNow;

        _repository.AddArchive("old entry", now.AddDays(-10));
        _repository.AddArchive("recent entry", now.AddHours(-1));

        var results = _repository.SearchArchive(ArchiveSearchQuery.Empty, now.AddDays(-1), 100);

        Assert.Single(results);
        Assert.Equal("recent entry", results[0].Text);
    }

    [Fact]
    public void SearchArchive_RespectsResultLimit()
    {
        var now = DateTime.UtcNow;

        for (var index = 0; index < 10; index++)
        {
            _repository.AddArchive($"searchable entry {index}", now.AddSeconds(index));
        }

        var results = _repository.SearchArchive(ArchiveSearchQuery.Parse("searchable"), null, 4);

        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void AddArchive_IgnoresConsecutiveDuplicates()
    {
        var now = DateTime.UtcNow;

        _repository.AddArchive("duplicate", now);
        _repository.AddArchive("duplicate", now.AddSeconds(1));

        Assert.Equal(1, _repository.CountArchive());
    }

    [Fact]
    public void Stickies_CanBeAddedUpdatedAndDeleted()
    {
        var id = _repository.AddSticky("clipboardhistory@example.com");
        _repository.AddSticky("https://example.com");

        var stickies = _repository.GetStickies();

        Assert.Equal(2, stickies.Count);
        Assert.Equal("clipboardhistory@example.com", stickies[0].Text);

        _repository.UpdateSticky(id, "changed@example.com");

        var updated = _repository.GetStickies().First(sticky => sticky.Id == id);
        Assert.Equal("changed@example.com", updated.Text);

        _repository.DeleteSticky(id);

        Assert.Single(_repository.GetStickies());
    }

    [Fact]
    public void TrimArchive_KeepsOnlyNewestItems()
    {
        var now = DateTime.UtcNow;

        for (var index = 0; index < 5; index++)
        {
            _repository.AddArchive($"entry {index}", now.AddSeconds(index));
        }

        _repository.TrimArchive(2);

        var archive = _repository.GetArchive(10);

        Assert.Equal(2, archive.Count);
        Assert.Equal("entry 4", archive[0].Text);
        Assert.Equal("entry 3", archive[1].Text);
    }
}
