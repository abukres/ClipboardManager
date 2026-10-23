using ClipboardManagerWinForm.Data;
using ClipboardManagerWinForm.Services;
using ClipboardManagerWinForm.UI;

namespace ClipboardManagerWinForm.Forms;

public sealed class ConfigurationForm : Form
{
    private readonly ClipboardApplicationContext _context;
    private readonly ListBox _stickiesList = new();
    private ListBox _stickiesPageList = new();
    private readonly TabControl _tabs;
    private readonly TextBox _historyInfo = new();
    private readonly CheckBox _monitorClipboard;
    private readonly CheckBox _hotKeysActive;
    private readonly CheckBox _capsLock;
    private readonly CheckBox _hotKeyEnabled;
    private readonly TextBox _hotKeyBox;
    private readonly CheckBox _disableInFullScreen;
    private readonly NumericUpDown _ignoreLarger;
    private readonly NumericUpDown _itemsToRemember;
    private readonly NumericUpDown _ignoreSmaller;
    private readonly CheckBox _runAtStartup;
    private List<ClipEntry> _stickies = [];
    private bool _initializing;

    public ConfigurationForm(ClipboardApplicationContext context)
    {
        _context = context;

        AutoScaleMode = AutoScaleMode.None;
        Font = Theme.BodyFont;
        Text = "Clipboard Manager Configuration";
        ClientSize = new Size(652, 424);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Icon = Theme.ApplicationIcon;
        BackColor = SystemColors.Control;

        _tabs = new TabControl
        {
            Location = new Point(8, 8),
            Size = new Size(636, 408),
            Font = Theme.BodyFont
        };

        var historyPage = new TabPage("History") { BackColor = SystemColors.Control };
        var stickiesPage = new TabPage("Stickies") { BackColor = SystemColors.Control };
        _tabs.TabPages.Add(historyPage);
        _tabs.TabPages.Add(stickiesPage);
        Controls.Add(_tabs);

        var historyGroup = Ui.Group("History", 8, 8, 300, 150);
        _historyInfo.Multiline = true;
        _historyInfo.ReadOnly = true;
        _historyInfo.ScrollBars = ScrollBars.Vertical;
        _historyInfo.BorderStyle = BorderStyle.FixedSingle;
        _historyInfo.BackColor = SystemColors.Window;
        _historyInfo.Font = Theme.BodyFont;
        _historyInfo.Location = new Point(10, 19);
        _historyInfo.Size = new Size(280, 121);
        _historyInfo.Text =
            "When you copy text to the Clipboard over time it will be stored in this list. " +
            "Texts can be pasted back to any application by pressing the hotkey which can be " +
            "configured below. You can also copy these texts to the Clipboard by using the " +
            "Tray Icon.";
        historyGroup.Controls.Add(_historyInfo);
        historyPage.Controls.Add(historyGroup);

        var stickiesInfoGroup = Ui.Group("Stickies (permanent clips)", 350, 8, 270, 150);
        _stickiesList.Location = new Point(10, 19);
        _stickiesList.Size = new Size(250, 121);
        _stickiesList.IntegralHeight = false;
        _stickiesList.BorderStyle = BorderStyle.FixedSingle;
        _stickiesList.Font = Theme.BodyFont;
        _stickiesList.DoubleClick += (_, _) => EditSticky();
        stickiesInfoGroup.Controls.Add(_stickiesList);
        historyPage.Controls.Add(stickiesInfoGroup);

        var editStrip = Ui.ToolStrip(
            312,
            8,
            30,
            Ui.ToolButton(ToolIcon.Add, "Add a new sticky clip", (_, _) => AddSticky()),
            Ui.ToolButton(ToolIcon.Edit, "Edit the selected sticky clip", (_, _) => EditSticky()),
            Ui.ToolButton(ToolIcon.Delete, "Delete the selected sticky clip", (_, _) => DeleteSticky()),
            Ui.ToolButton(ToolIcon.Help, "What are Stickies?", (_, _) => ShowStickiesHelp()));
        editStrip.LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow;
        editStrip.Size = new Size(30, 150);
        historyPage.Controls.Add(editStrip);

        _monitorClipboard = Ui.CheckBox("Monitor Clipboard", 10, 166, 150, context.Settings.MonitorClipboard, OnMonitorChanged);
        historyPage.Controls.Add(_monitorClipboard);

        historyPage.Controls.Add(Ui.Button("Archive", 170, 163, 105, (_, _) => _context.ShowArchive()));

        historyPage.Controls.Add(Ui.Label(
            "Configure application wide hotkeys to show paste menu",
            10,
            196,
            420));

        _hotKeysActive = Ui.CheckBox(
            "Active",
            10,
            218,
            70,
            context.Settings.HotKeysActive,
            OnHotKeysActiveChanged);
        _capsLock = Ui.CheckBox("CapsLock", 82, 218, 90, context.Settings.CapsLockEnabled, OnCapsLockChanged);
        var capsHelp = Ui.Button("?", 174, 216, 24, (_, _) => ShowCapsLockHelp(), 22);
        _hotKeyEnabled = Ui.CheckBox("Hotkey", 320, 218, 70, context.Settings.HotKeyEnabled, OnHotKeyEnabledChanged);
        _hotKeyBox = Ui.TextBox(395, 216, 108, readOnly: true);
        _hotKeyBox.KeyDown += HotKeyBoxOnKeyDown;
        var hotKeyHelp = Ui.Button("?", 510, 216, 24, (_, _) => ShowHotKeyHelp(), 22);

        historyPage.Controls.AddRange([_hotKeysActive, _capsLock, capsHelp, _hotKeyEnabled, _hotKeyBox, hotKeyHelp]);

        _disableInFullScreen = Ui.CheckBox(
            "Disabled in fullscreen mode",
            320,
            248,
            250,
            context.Settings.DisableInFullScreen,
            (_, _) => SaveSettings());
        historyPage.Controls.Add(_disableInFullScreen);

        historyPage.Controls.Add(Ui.Label("Ignore text larger than", 10, 280, 135));
        _ignoreLarger = Ui.Numeric(148, 278, 60, 1, 10240, context.Settings.MaxItemSizeKb);
        _ignoreLarger.ValueChanged += (_, _) => SaveSettings();
        historyPage.Controls.Add(_ignoreLarger);
        historyPage.Controls.Add(Ui.Label("KBytes", 212, 280, 50));

        historyPage.Controls.Add(Ui.Label("Items to remember", 360, 280, 110));
        _itemsToRemember = Ui.Numeric(475, 278, 55, 2, 100, context.Settings.HistoryDepth);
        _itemsToRemember.ValueChanged += OnItemsToRememberChanged;
        historyPage.Controls.Add(_itemsToRemember);

        historyPage.Controls.Add(Ui.Label("Ignore text smaller than", 10, 308, 135));
        _ignoreSmaller = Ui.Numeric(148, 306, 60, 1, 100000, context.Settings.MinTextLength);
        _ignoreSmaller.ValueChanged += (_, _) => SaveSettings();
        historyPage.Controls.Add(_ignoreSmaller);
        historyPage.Controls.Add(Ui.Label("characters", 212, 308, 90));

        _runAtStartup = Ui.CheckBox(
            "Run Clipboard Manager at Windows startup",
            10,
            336,
            320,
            context.Settings.RunAtStartup,
            OnRunAtStartupChanged);
        historyPage.Controls.Add(_runAtStartup);

        historyPage.Controls.Add(Ui.Button("More options", 430, 360, 105, (_, _) => ShowMoreOptions()));
        historyPage.Controls.Add(Ui.Button("About", 543, 360, 80, (_, _) => ShowAbout()));

        BuildStickiesPage(stickiesPage);

        LoadSettingsIntoControls();
        RefreshStickies();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _context.NotifyConfigurationClosed(this);
    }

    private void BuildStickiesPage(TabPage page)
    {
        var group = Ui.Group("Permanent text clips", 8, 8, 620, 300);
        page.Controls.Add(group);

        var list = new ListBox
        {
            Location = new Point(10, 20),
            Size = new Size(430, 270),
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.BodyFont
        };

        list.DoubleClick += (_, _) => EditSticky();

        group.Controls.Add(list);

        var addButton = Ui.Button("Add...", 452, 20, 150, (_, _) => AddSticky());
        var editButton = Ui.Button("Edit...", 452, 50, 150, (_, _) => EditSticky());
        var deleteButton = Ui.Button("Delete", 452, 80, 150, (_, _) => DeleteSticky());

        group.Controls.AddRange(
        [
            addButton,
            editButton,
            deleteButton,
            Ui.WrapLabel(
                "Stickies are permanent text clips that are always available in the paste menu. " +
                "Double-click a sticky in the list to edit it.",
                452,
                115,
                150,
                80)
        ]);

        _stickiesPageList = list;
    }

    private void LoadSettingsIntoControls()
    {
        _initializing = true;

        _monitorClipboard.Checked = _context.Settings.MonitorClipboard;
        _hotKeysActive.Checked = _context.Settings.HotKeysActive;
        _capsLock.Checked = _context.Settings.CapsLockEnabled;
        _hotKeyEnabled.Checked = _context.Settings.HotKeyEnabled;
        _disableInFullScreen.Checked = _context.Settings.DisableInFullScreen;
        _ignoreLarger.Value = Math.Clamp(_context.Settings.MaxItemSizeKb, 1, 10240);
        _itemsToRemember.Value = Math.Clamp(_context.Settings.HistoryDepth, 2, 100);
        _ignoreSmaller.Value = Math.Clamp(_context.Settings.MinTextLength, 1, 100000);
        _runAtStartup.Checked = _context.Settings.RunAtStartup;
        UpdateHotKeyBox();

        _initializing = false;
    }

    private void SaveSettings()
    {
        if (_initializing)
        {
            return;
        }

        var settings = _context.Settings;
        settings.MonitorClipboard = _monitorClipboard.Checked;
        settings.HotKeysActive = _hotKeysActive.Checked;
        settings.CapsLockEnabled = _capsLock.Checked;
        settings.HotKeyEnabled = _hotKeyEnabled.Checked;
        settings.DisableInFullScreen = _disableInFullScreen.Checked;
        settings.MaxItemSizeKb = (int)_ignoreLarger.Value;
        settings.HistoryDepth = (int)_itemsToRemember.Value;
        settings.MinTextLength = (int)_ignoreSmaller.Value;

        _context.ApplySettings();
    }

    private void OnMonitorChanged(object? sender, EventArgs e) => SaveSettings();

    private void OnHotKeysActiveChanged(object? sender, EventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        SaveSettings();
    }

    private void OnCapsLockChanged(object? sender, EventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        if (_capsLock.Checked && !_hotKeysActive.Checked)
        {
            _hotKeysActive.Checked = true;
        }

        SaveSettings();
    }

    private void OnHotKeyEnabledChanged(object? sender, EventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        if (_hotKeyEnabled.Checked && !_hotKeysActive.Checked)
        {
            _hotKeysActive.Checked = true;
        }

        SaveSettings();
    }

    private void OnItemsToRememberChanged(object? sender, EventArgs e)
    {
        if (!_initializing)
        {
            SaveSettings();
        }
    }

    private void OnRunAtStartupChanged(object? sender, EventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _context.Settings.RunAtStartup = _runAtStartup.Checked;
        StartupManager.SetEnabled(_runAtStartup.Checked);
        SaveSettings();
    }

    private void HotKeyBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return;
        }

        if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
        {
            _context.Settings.HotKeyModifiers = 0;
            _context.Settings.HotKeyKey = 0;
            UpdateHotKeyBox();
            SaveSettings();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        var modifiers = 0;
        if (e.Control)
        {
            modifiers |= Interop.NativeMethods.MOD_CONTROL;
        }

        if (e.Alt)
        {
            modifiers |= Interop.NativeMethods.MOD_ALT;
        }

        if (e.Shift)
        {
            modifiers |= Interop.NativeMethods.MOD_SHIFT;
        }

        if (modifiers == 0)
        {
            return;
        }

        _context.Settings.HotKeyModifiers = modifiers;
        _context.Settings.HotKeyKey = (int)e.KeyCode;
        _hotKeyEnabled.Checked = true;
        UpdateHotKeyBox();
        SaveSettings();

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void UpdateHotKeyBox()
    {
        var settings = _context.Settings;

        if (settings.HotKeyKey == 0)
        {
            _hotKeyBox.Text = "none";
            return;
        }

        List<string> parts = [];

        if ((settings.HotKeyModifiers & Interop.NativeMethods.MOD_CONTROL) != 0)
        {
            parts.Add("CTRL");
        }

        if ((settings.HotKeyModifiers & Interop.NativeMethods.MOD_ALT) != 0)
        {
            parts.Add("ALT");
        }

        if ((settings.HotKeyModifiers & Interop.NativeMethods.MOD_SHIFT) != 0)
        {
            parts.Add("SHIFT");
        }

        if ((settings.HotKeyModifiers & Interop.NativeMethods.MOD_WIN) != 0)
        {
            parts.Add("WIN");
        }

        parts.Add(((Keys)settings.HotKeyKey).ToString());
        _hotKeyBox.Text = string.Join(" + ", parts);
    }

    private void ShowCapsLockHelp()
    {
        MessageBox.Show(
            this,
            "When Caps Lock is enabled, pressing the Caps Lock key shows the paste menu instead of " +
            "toggling the Caps Lock state. The menu works in every Windows application.",
            "Caps Lock",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowHotKeyHelp()
    {
        MessageBox.Show(
            this,
            "Click into the hotkey box and press the key combination you want to use, for example ALT + C. " +
            "Press Backspace to clear the shortcut.",
            "Hotkey",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowStickiesHelp()
    {
        MessageBox.Show(
            this,
            "Stickies are permanent text clips. They stay in the paste menu until you delete them and are " +
            "handy for snippets such as your email address or phone number.",
            "Stickies",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void AddSticky()
    {
        using var editor = new StickyEditorForm(_context.Repository, null);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            RefreshStickies();
        }
    }

    private void EditSticky()
    {
        var entry = GetSelectedSticky();
        if (entry is null)
        {
            return;
        }

        using var editor = new StickyEditorForm(_context.Repository, entry);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            RefreshStickies();
        }
    }

    private void DeleteSticky()
    {
        var entry = GetSelectedSticky();
        if (entry is null)
        {
            return;
        }

        if (_context.Settings.AskBeforeDeleting &&
            MessageBox.Show(
                this,
                "Delete the selected sticky clip?",
                "Delete sticky",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _context.Repository.DeleteSticky(entry.Id);
        RefreshStickies();
    }

    private ClipEntry? GetSelectedSticky()
    {
        var activeList = _tabs.SelectedIndex == 1 ? _stickiesPageList : _stickiesList;
        var index = activeList.SelectedIndex;

        return index >= 0 && index < _stickies.Count ? _stickies[index] : null;
    }

    private void RefreshStickies()
    {
        _stickies = [.. _context.Repository.GetStickies()];

        _stickiesList.BeginUpdate();
        _stickiesPageList.BeginUpdate();
        _stickiesList.Items.Clear();
        _stickiesPageList.Items.Clear();

        foreach (var sticky in _stickies)
        {
            _stickiesList.Items.Add(sticky.DisplayText);
            _stickiesPageList.Items.Add(sticky.DisplayText);
        }

        _stickiesList.EndUpdate();
        _stickiesPageList.EndUpdate();
    }

    private void ShowMoreOptions()
    {
        using var form = new MoreOptionsForm(_context);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadSettingsIntoControls();
        }
    }

    private void ShowAbout()
    {
        using var form = new AboutForm();
        form.ShowDialog(this);
    }
}
