namespace ClipboardManagerWinForm.Services;

public sealed class AppSettings
{
    public bool MonitorClipboard { get; set; } = true;

    public bool HotKeysActive { get; set; } = true;

    public bool CapsLockEnabled { get; set; } = true;

    public bool HotKeyEnabled { get; set; }

    public int HotKeyModifiers { get; set; } = 0x0001;

    public int HotKeyKey { get; set; } = (int)Keys.C;

    public bool DisableInFullScreen { get; set; } = true;

    public int HistoryDepth { get; set; } = 10;

    public bool MaxItemSizeEnabled { get; set; }

    public int MaxItemSizeKb { get; set; } = 4;

    public bool MinTextLengthEnabled { get; set; } = true;

    public int MinTextLength { get; set; } = 3;

    public bool RunAtStartup { get; set; }

    public bool AddToArchive { get; set; } = true;

    public bool CloseAfterAdding { get; set; }

    public int ArchiveLimitSizeKb { get; set; }

    public int ArchiveLimitItems { get; set; }

    public bool JoinBrowserLinks { get; set; } = true;

    public bool IgnoreContainingEnabled { get; set; }

    public string IgnoreContaining { get; set; } = ";foo;bar";

    public int MaxArchiveSearchResults { get; set; } = 1000;

    public bool PlaySoundOnChange { get; set; }

    public string SoundFile { get; set; } = string.Empty;

    public bool TrayLeftDoubleClickOpensConfiguration { get; set; } = true;

    public int PasteMenuCursorPosition { get; set; } = 1;

    public bool DoubleClickPastesToApplication { get; set; }

    public bool AskBeforeDeleting { get; set; } = true;

    public bool ShowStickiesInMenu { get; set; } = true;

    public int PasteMenuWidth { get; set; }

    public AppSettings Clone()
    {
        return (AppSettings)MemberwiseClone();
    }

    public void CopyFrom(AppSettings other)
    {
        MonitorClipboard = other.MonitorClipboard;
        HotKeysActive = other.HotKeysActive;
        CapsLockEnabled = other.CapsLockEnabled;
        HotKeyEnabled = other.HotKeyEnabled;
        HotKeyModifiers = other.HotKeyModifiers;
        HotKeyKey = other.HotKeyKey;
        DisableInFullScreen = other.DisableInFullScreen;
        HistoryDepth = other.HistoryDepth;
        MaxItemSizeEnabled = other.MaxItemSizeEnabled;
        MaxItemSizeKb = other.MaxItemSizeKb;
        MinTextLengthEnabled = other.MinTextLengthEnabled;
        MinTextLength = other.MinTextLength;
        RunAtStartup = other.RunAtStartup;
        AddToArchive = other.AddToArchive;
        CloseAfterAdding = other.CloseAfterAdding;
        ArchiveLimitSizeKb = other.ArchiveLimitSizeKb;
        ArchiveLimitItems = other.ArchiveLimitItems;
        JoinBrowserLinks = other.JoinBrowserLinks;
        IgnoreContainingEnabled = other.IgnoreContainingEnabled;
        IgnoreContaining = other.IgnoreContaining;
        MaxArchiveSearchResults = other.MaxArchiveSearchResults;
        PlaySoundOnChange = other.PlaySoundOnChange;
        SoundFile = other.SoundFile;
        TrayLeftDoubleClickOpensConfiguration = other.TrayLeftDoubleClickOpensConfiguration;
        PasteMenuCursorPosition = other.PasteMenuCursorPosition;
        DoubleClickPastesToApplication = other.DoubleClickPastesToApplication;
        AskBeforeDeleting = other.AskBeforeDeleting;
        ShowStickiesInMenu = other.ShowStickiesInMenu;
        PasteMenuWidth = other.PasteMenuWidth;
    }
}
