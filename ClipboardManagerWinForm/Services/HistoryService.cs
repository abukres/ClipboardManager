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

    public void ClearHistory() => repository.ClearHistory();
}
