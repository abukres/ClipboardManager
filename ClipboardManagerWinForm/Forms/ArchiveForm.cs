using ClipboardManagerWinForm.Clipboard;
using ClipboardManagerWinForm.Data;
using ClipboardManagerWinForm.UI;

namespace ClipboardManagerWinForm.Forms;

public sealed class ArchiveForm : Form
{
    private static readonly (string Text, TimeSpan? Window)[] SearchWindows =
    [
        ("complete archive", null),
        ("30 days", TimeSpan.FromDays(30)),
        ("6 months", TimeSpan.FromDays(182)),
        ("1 year", TimeSpan.FromDays(365)),
        ("3 years", TimeSpan.FromDays(1095))
    ];

    private static readonly (string Text, int SizeKb)[] SizeLimits =
    [
        ("Unlimited", 0),
        ("1 KB", 1),
        ("4 KB", 4),
        ("16 KB", 16),
        ("64 KB", 64),
        ("256 KB", 256)
    ];

    private static readonly (string Text, int Items)[] ItemLimits =
    [
        ("Unlimited", 0),
        ("10", 10),
        ("50", 50),
        ("100", 100),
        ("250", 250),
        ("500", 500),
        ("1000", 1000)
    ];

    private readonly ClipboardApplicationContext _context;
    private readonly CheckBox _addToArchive;
    private readonly CheckBox _closeAfterAdding;
    private readonly ComboBox _sizeLimit;
    private readonly ComboBox _itemLimit;
    private readonly TextBox _searchBox;
    private readonly ComboBox _searchWindow;
    private readonly DataGridView _grid;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _searchTimer;
    private List<ClipEntry> _entries = [];
    private bool _initializing;

    public ArchiveForm(ClipboardApplicationContext context)
    {
        _context = context;

        AutoScaleMode = AutoScaleMode.None;
        Font = Theme.BodyFont;
        Text = "Clipboard Manager Archive - double click a line or hit enter to add to clipboard";
        ClientSize = new Size(676, 486);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 360);
        ShowInTaskbar = false;
        Icon = Theme.ApplicationIcon;
        BackColor = SystemColors.Control;

        var options = Ui.Group("Options", 8, 6, 660, 78);
        _addToArchive = Ui.CheckBox(
            "Add new clipboard content to archive",
            12,
            20,
            290,
            context.Settings.AddToArchive,
            OnAddToArchiveChanged);
        _closeAfterAdding = Ui.CheckBox(
            "Close after adding to clipboard",
            340,
            20,
            300,
            context.Settings.CloseAfterAdding,
            OnCloseAfterAddingChanged);

        var sizeLabel = Ui.Label("Limit size", 12, 50, 60);
        _sizeLimit = Ui.Combo(76, 46, 120);
        var orLabel = Ui.Label("or", 206, 50, 20);
        _itemLimit = Ui.Combo(232, 46, 110);
        var itemsLabel = Ui.Label("items", 350, 50, 40);

        options.Controls.AddRange(
        [
            _addToArchive,
            _closeAfterAdding,
            sizeLabel,
            _sizeLimit,
            orLabel,
            _itemLimit,
            itemsLabel
        ]);
        Controls.Add(options);

        _searchBox = Ui.TextBox(12, 96, 286);
        _searchBox.TextChanged += (_, _) => RestartSearchTimer();
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                RunSearch();
            }
        };

        Controls.Add(_searchBox);
        Controls.Add(Ui.Button("?", 302, 94, 24, (_, _) => ShowSearchHelp(), 23));
        Controls.Add(Ui.Label("within", 334, 98, 42));

        _searchWindow = Ui.Combo(378, 96, 180);
        _searchWindow.Items.AddRange(SearchWindows.Select(window => (object)window.Text).ToArray());
        _searchWindow.SelectedIndex = 0;
        _searchWindow.SelectedIndexChanged += (_, _) => RunSearch();
        Controls.Add(_searchWindow);

        var tools = Ui.ToolStrip(
            566,
            94,
            90,
            Ui.ToolButton(ToolIcon.Search, "Search the archive", (_, _) => RunSearch()),
            Ui.ToolButton(ToolIcon.Copy, "Copy the selected text to the clipboard", (_, _) => CopySelected()),
            Ui.ToolButton(ToolIcon.Delete, "Delete the selected archive item", (_, _) => DeleteSelected()),
            Ui.ToolButton(ToolIcon.Clear, "Delete the complete archive", (_, _) => DeleteAll()));
        Controls.Add(tools);

        _grid = new DataGridView
        {
            Location = new Point(8, 128),
            Size = new Size(660, 320),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            Font = Theme.BodyFont,
            GridColor = SystemColors.ControlLight,
            RowTemplate = { Height = 19 },
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Date",
            Width = 150,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Text",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                CopySelected();
            }
        };

        _grid.KeyDown += GridOnKeyDown;
        Controls.Add(_grid);

        _statusLabel = Ui.Label("Items found: 0", 10, 458, 400);
        _statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        Controls.Add(_statusLabel);

        var closeButton = Ui.Button("Close", 590, 452, 78, (_, _) => Close());
        closeButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(closeButton);

        _searchTimer = new System.Windows.Forms.Timer { Interval = 350 };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            RunSearch();
        };

        InitializeLimitCombos();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _searchBox.Focus();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _context.NotifyArchiveClosed(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Handled here so Esc closes the window no matter which control has the focus.
        if ((keyData & Keys.KeyCode) == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void InitializeLimitCombos()
    {
        _initializing = true;

        foreach (var (text, _) in SizeLimits)
        {
            _sizeLimit.Items.Add(text);
        }

        foreach (var (text, _) in ItemLimits)
        {
            _itemLimit.Items.Add(text);
        }

        _sizeLimit.SelectedIndex = Math.Max(0, Array.FindIndex(SizeLimits, limit => limit.SizeKb == _context.Settings.ArchiveLimitSizeKb));
        _itemLimit.SelectedIndex = Math.Max(0, Array.FindIndex(ItemLimits, limit => limit.Items == _context.Settings.ArchiveLimitItems));

        _sizeLimit.SelectedIndexChanged += (_, _) => ApplyLimits();
        _itemLimit.SelectedIndexChanged += (_, _) => ApplyLimits();

        _initializing = false;
        RunSearch();
    }

    private void ApplyLimits()
    {
        if (_initializing)
        {
            return;
        }

        var sizeIndex = Math.Max(0, _sizeLimit.SelectedIndex);
        var itemIndex = Math.Max(0, _itemLimit.SelectedIndex);

        _context.Settings.ArchiveLimitSizeKb = SizeLimits[sizeIndex].SizeKb;
        _context.Settings.ArchiveLimitItems = ItemLimits[itemIndex].Items;
        _context.ApplySettings();
    }

    private void OnAddToArchiveChanged(object? sender, EventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _context.Settings.AddToArchive = _addToArchive.Checked;
        _context.ApplySettings();
    }

    private void OnCloseAfterAddingChanged(object? sender, EventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _context.Settings.CloseAfterAdding = _closeAfterAdding.Checked;
        _context.ApplySettings();
    }

    private void RestartSearchTimer()
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void RunSearch()
    {
        _searchTimer.Stop();

        var query = ArchiveSearchQuery.Parse(_searchBox.Text);
        var since = GetSelectedWindowStart();

        try
        {
            _entries = [.. _context.Repository.SearchArchive(query, since, _context.Settings.MaxArchiveSearchResults)];
        }
        catch (Microsoft.Data.Sqlite.SqliteException exception)
        {
            MessageBox.Show(this, exception.Message, "Search", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        PopulateGrid();
    }

    private DateTime? GetSelectedWindowStart()
    {
        var index = Math.Max(0, _searchWindow.SelectedIndex);
        var window = SearchWindows[index].Window;
        return window is null ? null : DateTime.UtcNow - window.Value;
    }

    private void PopulateGrid()
    {
        _grid.SuspendLayout();

        _grid.Rows.Clear();

        foreach (var entry in _entries)
        {
            _grid.Rows.Add(entry.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss"), entry.DisplayText);
        }

        _grid.ResumeLayout();

        var limit = _context.Settings.MaxArchiveSearchResults;
        _statusLabel.Text = _entries.Count >= limit
            ? $"Items found: {_entries.Count} (search limit reached)"
            : $"Items found: {_entries.Count}";

        if (_grid.Rows.Count > 0)
        {
            _grid.ClearSelection();
        }

        _grid.Refresh();
    }

    private ClipEntry? GetSelectedEntry()
    {
        if (_grid.CurrentRow is null)
        {
            return null;
        }

        var index = _grid.CurrentRow.Index;
        return index >= 0 && index < _entries.Count ? _entries[index] : null;
    }

    private void GridOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            CopySelected();
        }
        else if (e.KeyCode == Keys.Delete)
        {
            e.SuppressKeyPress = true;
            DeleteSelected();
        }
    }

    private void CopySelected()
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            return;
        }

        _context.CopyToClipboard(entry.Text);

        if (_context.Settings.CloseAfterAdding)
        {
            Close();
            return;
        }

        if (_context.Settings.DoubleClickPastesToApplication)
        {
            Hide();
            var timer = new System.Windows.Forms.Timer { Interval = 120 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                ClipboardService.SendPasteKeys();
                Show();
                Activate();
            };
            timer.Start();
        }
    }

    private void DeleteSelected()
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            return;
        }

        if (_context.Settings.AskBeforeDeleting &&
            MessageBox.Show(
                this,
                "Delete the selected archive item?",
                "Delete archive item",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _context.Repository.DeleteArchive(entry.Id);
        RunSearch();
    }

    private void DeleteAll()
    {
        if (MessageBox.Show(
                this,
                "Delete all items in the archive? This cannot be undone.",
                "Delete archive",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        _context.Repository.ClearArchive();
        RunSearch();
    }

    private void ShowSearchHelp()
    {
        MessageBox.Show(
            this,
            "Search the archive by keywords. Combine keywords with spaces and exclude words with a minus sign. " +
            "Example: foo -bar finds all items that contain foo but not bar. " +
            "Use quotes for phrases: \"hello world\".",
            "Search",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
