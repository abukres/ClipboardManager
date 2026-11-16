using ClipboardManagerWinForm.Clipboard;
using ClipboardManagerWinForm.Data;
using ClipboardManagerWinForm.Forms;
using ClipboardManagerWinForm.Input;
using ClipboardManagerWinForm.Interop;
using ClipboardManagerWinForm.Services;
using ClipboardManagerWinForm.UI;

namespace ClipboardManagerWinForm;

public sealed class ClipboardApplicationContext : ApplicationContext
{
    private const uint SoundAsync = 0x0001;
    private const uint SoundFileName = 0x00020000;

    private readonly MessageDispatcher _dispatcher = new();
    private readonly SettingsStore _settingsStore = new(AppPaths.DataFolder);
    private readonly Database _database;
    private readonly HistoryService _historyService;
    private readonly ClipboardMonitor _monitor;
    private readonly CapsLockHook _capsLockHook;
    private readonly HotKeyWindow _hotKeyWindow = new();
    private readonly NotifyIcon _notifyIcon;

    private ConfigurationForm? _configurationForm;
    private ArchiveForm? _archiveForm;
    private ContextMenuStrip? _trayMenu;
    private bool _suppressCapture;
    private bool _pasteMenuOpen;
    private bool _disposed;

    public ClipboardApplicationContext()
    {
        _database = new Database(AppPaths.DatabaseFile);
        Repository = new ClipboardRepository(_database);
        _historyService = new HistoryService(Repository, _settingsStore);
        _historyService.TextCaptured += OnTextCaptured;

        _notifyIcon = new NotifyIcon
        {
            Icon = Theme.ApplicationIcon,
            Text = "Clipboard Manager",
            Visible = true
        };

        _notifyIcon.MouseUp += OnTrayMouseUp;
        _notifyIcon.MouseDoubleClick += OnTrayMouseDoubleClick;

        _monitor = new ClipboardMonitor();
        _monitor.ClipboardUpdated += OnClipboardUpdated;

        _capsLockHook = new CapsLockHook(_dispatcher.Post);
        _capsLockHook.CapsLockPressed += (_, _) => ShowPasteMenu();

        _hotKeyWindow.Pressed += (_, _) => ShowPasteMenu();

        if (Settings.RunAtStartup)
        {
            StartupManager.SetEnabled(true);
        }

        Repository.TrimHistory(Math.Clamp(Settings.HistoryDepth, 2, 100));
        UpdateTriggers();
        UpdateTrayTooltipFromHistory();
    }

    public ClipboardRepository Repository { get; }

    public AppSettings Settings => _settingsStore.Settings;

    public string SettingsPath => _settingsStore.SettingsPath;

    public void ApplySettings()
    {
        if (_disposed)
        {
            return;
        }

        _settingsStore.Save();
        UpdateTriggers();
        Repository.TrimHistory(Math.Clamp(Settings.HistoryDepth, 2, 100));
    }

    public void ReplaceSettings(AppSettings settings)
    {
        Settings.CopyFrom(settings);
        ApplySettings();
    }

    public bool CopyToClipboard(string text, bool addToHistory = true)
    {
        _suppressCapture = true;

        if (!ClipboardService.SetText(text))
        {
            _suppressCapture = false;
            return false;
        }

        if (addToHistory)
        {
            _historyService.Reuse(text);
        }

        _notifyIcon.Text = TextFilters.CreateTooltip(text);
        return true;
    }

    public void ShowPasteMenu()
    {
        if (_pasteMenuOpen || _disposed)
        {
            return;
        }

        if (Settings.DisableInFullScreen && FullScreenDetector.IsFullScreenApplicationActive())
        {
            return;
        }

        _pasteMenuOpen = true;

        try
        {
            var target = NativeMethods.GetForegroundWindow();

            var previousWidth = Settings.PasteMenuWidth;

            var result = PasteMenuForm.ShowMenu(
                _historyService.GetHistoryEntries(),
                _historyService.GetStickyEntries(),
                Math.Clamp(Settings.PasteMenuCursorPosition, 0, 1),
                previousWidth,
                _historyService.GetMoreHistoryEntries);

            if (result.Width > 0 && result.Width != previousWidth)
            {
                Settings.PasteMenuWidth = result.Width;
                _settingsStore.Save();
            }

            switch (result.Command)
            {
                case PasteMenuCommand.Paste when result.Entry is not null:
                    PasteEntry(target, result.Entry);
                    break;

                case PasteMenuCommand.ClearClipboard:
                    _suppressCapture = true;
                    ClipboardService.Clear();
                    RestoreFocus(target);
                    break;

                case PasteMenuCommand.AddClipboardToStickies:
                    AddClipboardContentToStickies();
                    break;

                case PasteMenuCommand.Archive:
                    ShowArchive();
                    break;

                case PasteMenuCommand.Configure:
                    ShowConfiguration();
                    break;

                default:
                    RestoreFocus(target);
                    break;
            }
        }
        finally
        {
            _pasteMenuOpen = false;
        }
    }

    public void RequestShowConfiguration()
    {
        _dispatcher.Post(ShowConfiguration);
    }

    public void ShowConfiguration()
    {
        if (_configurationForm is { IsDisposed: false })
        {
            if (_configurationForm.WindowState == FormWindowState.Minimized)
            {
                _configurationForm.WindowState = FormWindowState.Normal;
            }

            _configurationForm.Show();
            WindowActivation.BringToForeground(_configurationForm.Handle);
            _configurationForm.Activate();
            return;
        }

        _configurationForm = new ConfigurationForm(this);
        _configurationForm.Show();
        WindowActivation.BringToForeground(_configurationForm.Handle);
    }

    public void ShowArchive()
    {
        if (_archiveForm is { IsDisposed: false })
        {
            if (_archiveForm.WindowState == FormWindowState.Minimized)
            {
                _archiveForm.WindowState = FormWindowState.Normal;
            }

            _archiveForm.Show();
            WindowActivation.BringToForeground(_archiveForm.Handle);
            _archiveForm.Activate();
            return;
        }

        _archiveForm = new ArchiveForm(this);
        _archiveForm.Show();
        WindowActivation.BringToForeground(_archiveForm.Handle);
    }

    public void NotifyConfigurationClosed(ConfigurationForm form)
    {
        if (ReferenceEquals(_configurationForm, form))
        {
            _configurationForm = null;
        }
    }

    public void NotifyArchiveClosed(ArchiveForm form)
    {
        if (ReferenceEquals(_archiveForm, form))
        {
            _archiveForm = null;
        }
    }

    public void AddClipboardContentToStickies()
    {
        ClipboardService.TryGetText(out var text);

        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show(
                "The Clipboard does not contain any text.",
                "Clipboard Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var editor = new StickyEditorForm(Repository, null, text);
        editor.ShowDialog();
    }

    public void ExitApplication()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        _capsLockHook.Dispose();
        _hotKeyWindow.Dispose();
        _monitor.Dispose();
        _dispatcher.Dispose();

        if (_configurationForm is { IsDisposed: false })
        {
            _configurationForm.Close();
        }

        if (_archiveForm is { IsDisposed: false })
        {
            _archiveForm.Close();
        }

        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _notifyIcon.Dispose();
                _capsLockHook.Dispose();
                _hotKeyWindow.Dispose();
                _monitor.Dispose();
                _dispatcher.Dispose();
            }

            _trayMenu?.Dispose();
            _trayMenu = null;
        }

        base.Dispose(disposing);
    }

    private void UpdateTriggers()
    {
        var settings = Settings;
        _capsLockHook.Enabled = settings.HotKeysActive && settings.CapsLockEnabled;

        var canRegisterHotKey = settings.HotKeysActive &&
                                settings.HotKeyEnabled &&
                                settings.HotKeyKey != 0;

        if (canRegisterHotKey)
        {
            var modifiers = settings.HotKeyModifiers &
                            (NativeMethods.MOD_ALT |
                             NativeMethods.MOD_CONTROL |
                             NativeMethods.MOD_SHIFT |
                             NativeMethods.MOD_WIN);

            _hotKeyWindow.Register(modifiers, (Keys)settings.HotKeyKey);
        }
        else
        {
            _hotKeyWindow.Unregister();
        }
    }

    private void OnClipboardUpdated(object? sender, EventArgs e)
    {
        if (_suppressCapture)
        {
            _suppressCapture = false;
            return;
        }

        if (!Settings.MonitorClipboard || !ClipboardService.TryGetText(out var text))
        {
            return;
        }

        _historyService.Capture(text);
    }

    private void OnTextCaptured(object? sender, string text)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Text = TextFilters.CreateTooltip(text);

        if (!Settings.PlaySoundOnChange)
        {
            return;
        }

        var soundFile = Settings.SoundFile;

        if (!string.IsNullOrWhiteSpace(soundFile) && File.Exists(soundFile))
        {
            NativeMethods.PlaySound(soundFile, IntPtr.Zero, SoundAsync | SoundFileName);
        }
        else
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
    }

    private async void PasteEntry(IntPtr target, ClipEntry entry)
    {
        if (!CopyToClipboard(entry.Text, addToHistory: !entry.IsSticky))
        {
            return;
        }

        RestoreFocus(target);

        await Task.Delay(80);
        ClipboardService.SendPasteKeys();
    }

    private static void RestoreFocus(IntPtr target)
    {
        if (target != IntPtr.Zero && NativeMethods.IsWindow(target))
        {
            NativeMethods.SetForegroundWindow(target);
        }
    }

    private void UpdateTrayTooltipFromHistory()
    {
        var history = Repository.GetHistory(1);
        _notifyIcon.Text = history.Count > 0
            ? TextFilters.CreateTooltip(history[0].Text)
            : "Clipboard Manager";
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && !Settings.TrayLeftDoubleClickOpensConfiguration)
        {
            ShowTrayMenu();
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (Settings.TrayLeftDoubleClickOpensConfiguration)
            {
                ShowTrayMenu();
            }
            else
            {
                ShowConfiguration();
            }
        }
    }

    private void OnTrayMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Settings.TrayLeftDoubleClickOpensConfiguration)
        {
            ShowConfiguration();
        }
    }

    private void ShowTrayMenu()
    {
        if (_disposed)
        {
            return;
        }

        var menu = EnsureTrayMenu();

        if (menu.Visible)
        {
            menu.Close();
        }

        ResetTrayMenu(menu);
        PopulateTrayMenu(menu);
        menu.Show(Cursor.Position);
    }

    private ContextMenuStrip EnsureTrayMenu()
    {
        if (_trayMenu is { IsDisposed: false } menu)
        {
            return menu;
        }

        menu = new ContextMenuStrip
        {
            Font = Theme.BodyFont,
            ShowImageMargin = false,
            RenderMode = ToolStripRenderMode.System
        };

        _trayMenu = menu;

        return menu;
    }

    private static void ResetTrayMenu(ContextMenuStrip menu)
    {
        var items = menu.Items.Cast<ToolStripItem>().ToArray();
        menu.Items.Clear();

        foreach (var item in items)
        {
            item.Dispose();
        }
    }

    private void PopulateTrayMenu(ContextMenuStrip menu)
    {
        var history = _historyService.GetHistoryEntries();

        for (var index = 0; index < history.Count; index++)
        {
            var entry = history[index];
            var prefix = index < 9 ? $"{index + 1}  " : string.Empty;
            var item = new ToolStripMenuItem(prefix + TextFilters.Summarize(entry.DisplayText, 62));
            item.Click += (_, _) => CopyToClipboard(entry.Text);
            menu.Items.Add(item);
        }

        if (history.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
        }

        var clearItem = new ToolStripMenuItem("Clear Clipboard content");
        clearItem.Click += (_, _) =>
        {
            _suppressCapture = true;
            ClipboardService.Clear();
        };
        menu.Items.Add(clearItem);

        var stickies = _historyService.GetStickyEntries();

        if (stickies.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(BuildStickiesMenu(stickies));
        }

        var addSticky = new ToolStripMenuItem("Add Clipboard content to Stickies");
        addSticky.Click += (_, _) => AddClipboardContentToStickies();
        menu.Items.Add(addSticky);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("Archive...", ShowArchive));
        menu.Items.Add(CreateMenuItem("Configure", ShowConfiguration));
        menu.Items.Add(CreateMenuItem("Quit", ExitApplication));
    }

    private ToolStripMenuItem BuildStickiesMenu(IReadOnlyList<ClipEntry> stickies)
    {
        var stickiesMenu = new ToolStripMenuItem("Stickies");

        foreach (var sticky in stickies)
        {
            stickiesMenu.DropDownItems.Add(CreateStickyMenuItem(sticky));
        }

        return stickiesMenu;
    }

    private ToolStripMenuItem CreateStickyMenuItem(ClipEntry sticky)
    {
        var item = new ToolStripMenuItem(TextFilters.Summarize(sticky.DisplayText, 62));
        item.Click += (_, _) => CopyToClipboard(sticky.Text, addToHistory: false);
        return item;
    }

    private static ToolStripMenuItem CreateMenuItem(string text, Action action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        return item;
    }
}
