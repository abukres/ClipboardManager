using ClipboardManagerWinForm.Data;

namespace ClipboardManagerWinForm.Services;

public sealed class HistoryService(ClipboardRepository repository, SettingsStore settingsStore)
{
    public event EventHandler<string>? TextCaptured;

    public bool Capture(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !settingsStore.Settings.MonitorClipboard)
        {
            return false;
        }

        var settings = settingsStore.Settings;
        var normalized = text.TrimEnd();

        if (TextFilters.ContainsIgnoredText(normalized, settings.IgnoreContaining, settings.IgnoreContainingEnabled))
        {
            return false;
        }

        if (settings.MinTextLengthEnabled && normalized.Trim().Length < Math.Max(1, settings.MinTextLength))
        {
            return false;
        }

        if (settings.MaxItemSizeEnabled &&
            TextFilters.GetByteCount(normalized) > Math.Max(1, settings.MaxItemSizeKb) * 1024)
        {
            return false;
        }

        if (settings.JoinBrowserLinks && TextFilters.TryJoinBrowserLink(normalized, out var joinedLink))
        {
            normalized = joinedLink;
        }

        var timestamp = DateTime.UtcNow;
        repository.AddHistory(normalized, timestamp, Math.Clamp(settings.HistoryDepth, 2, 100));

        if (settings.AddToArchive && IsArchiveSizeAllowed(normalized, settings))
        {
            repository.AddArchive(normalized, timestamp);

            if (settings.ArchiveLimitItems > 0)
            {
                repository.TrimArchive(settings.ArchiveLimitItems);
            }
        }

        TextCaptured?.Invoke(this, normalized);
        return true;
    }

    /// <summary>
    /// Puts a clip that the user took from the history or the archive to the first position of the
    /// history, so it is the most recent clip again. The capture filters do not apply, because the
    /// user picked the clip on purpose.
    /// </summary>
    public void Reuse(string text)
    {
        repository.PromoteHistory(text, DateTime.UtcNow, Math.Clamp(settingsStore.Settings.HistoryDepth, 2, 100));
    }

    private static bool IsArchiveSizeAllowed(string text, AppSettings settings)
    {
        return settings.ArchiveLimitSizeKb <= 0 ||
               TextFilters.GetByteCount(text) <= settings.ArchiveLimitSizeKb * 1024;
    }

    public IReadOnlyList<ClipEntry> GetHistoryEntries()
    {
        return repository.GetHistory(Math.Clamp(settingsStore.Settings.HistoryDepth, 2, 100));
    }

    public IReadOnlyList<ClipEntry> GetStickyEntries()
    {
        return settingsStore.Settings.ShowStickiesInMenu ? repository.GetStickies() : [];
    }

    /// <summary>
    /// The older clips of the archive that are not part of the history, sorted from newest to oldest.
    /// The history clips are skipped, because the paste menu already shows them.
    /// </summary>
    public IReadOnlyList<ClipEntry> GetMoreHistoryEntries()
    {
        var history = GetHistoryEntries();
        var limit = Math.Max(1, settingsStore.Settings.MaxArchiveSearchResults);

        List<ClipEntry> entries = [];
        var knownTexts = new HashSet<string>(history.Select(entry => entry.Text), StringComparer.Ordinal);

        // The archive also holds the history clips, so the remaining clips are the older ones.
        foreach (var entry in repository.GetArchive(history.Count + limit))
        {
            if (knownTexts.Add(entry.Text))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    public void ClearHistory() => repository.ClearHistory();
}
