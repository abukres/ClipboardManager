using ClipboardManagerWinForm.Data;
using ClipboardManagerWinForm.Input;
using ClipboardManagerWinForm.Interop;
using ClipboardManagerWinForm.UI;

namespace ClipboardManagerWinForm.Forms;

public enum PasteMenuCommand
{
    None,
    Paste,
    MoreHistory,
    ClearClipboard,
    AddClipboardToStickies,
    Archive,
    Configure,
    Close
}

public sealed record PasteMenuResult(PasteMenuCommand Command, ClipEntry? Entry, int Width = 0);

public sealed class PasteMenuForm : Form
{
    private const int BorderWidth = 2;

    private const string ArchiveAccelerator = "A";

    private const int MoreHistoryHoverDelayMs = 1000;

    private readonly MenuListControl _list;
    private readonly IReadOnlyList<ClipEntry> _stickies;
    private readonly Func<IReadOnlyList<ClipEntry>>? _moreHistoryProvider;
    private readonly List<MenuRow> _defaultRows;
    private readonly System.Windows.Forms.Timer _moreHistoryTimer;
    private readonly int _initialRow;
    private GlobalMouseHook? _mouseHook;
    private bool _closing;
    private bool _moreHistoryShown;

    public PasteMenuForm(
        IReadOnlyList<ClipEntry> history,
        IReadOnlyList<ClipEntry> stickies,
        int initialIndex,
        int preferredWidth,
        Func<IReadOnlyList<ClipEntry>>? moreHistoryProvider = null)
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Font = Theme.BodyFont;
        BackColor = Theme.MenuBorder;
        Padding = new Padding(1);
        DoubleBuffered = true;

        _stickies = stickies;
        _moreHistoryProvider = moreHistoryProvider;

        _moreHistoryTimer = new System.Windows.Forms.Timer
        {
            Interval = MoreHistoryHoverDelayMs
        };
        _moreHistoryTimer.Tick += HandleMoreHistoryTimerTick;

        var rows = BuildRows(history, stickies, moreHistoryProvider is not null);
        _defaultRows = [.. rows];

        _list = new MenuListControl(rows, preferredWidth)
        {
            Dock = DockStyle.Fill
        };

        _list.ItemActivated += HandleItemActivated;
        _list.ItemHovered += HandleItemHovered;
        _list.HoverLeft += HandleHoverLeft;
        _list.CancelRequested += (_, _) => Close();
        Controls.Add(_list);

        var size = _list.MeasureContent();
        ClientSize = new Size(size.Width + BorderWidth, size.Height + BorderWidth);

        // "Place cursor in paste menu at" refers to the history items only, never to a command.
        _initialRow = history.Count == 0 ? -1 : Math.Min(initialIndex, history.Count - 1);
        _list.SelectInitial(_initialRow);

        PositionNearCursor();
    }

    public PasteMenuResult Result { get; private set; } = new(PasteMenuCommand.None, null);

    /// <summary>
    /// Width of the popup without the 1 pixel border.
    /// </summary>
    public int MenuWidth => Math.Max(0, ClientSize.Width - BorderWidth);

    /// <summary>
    /// True when the user changed the width of the popup, so the automatic width is kept otherwise.
    /// </summary>
    public bool WasWidthResized => _list.WidthResized;

    public static PasteMenuResult ShowMenu(
        IReadOnlyList<ClipEntry> history,
        IReadOnlyList<ClipEntry> stickies,
        int initialIndex,
        int preferredWidth,
        Func<IReadOnlyList<ClipEntry>>? moreHistoryProvider = null)
    {
        using var form = new PasteMenuForm(history, stickies, initialIndex, preferredWidth, moreHistoryProvider);
        form.ShowDialog();
        return form.Result with { Width = form.WasWidthResized ? form.MenuWidth : 0 };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // The popup is opened from a global hotkey of a background process, so Windows may refuse the
        // activation and the keyboard shortcuts would go to the previously active window instead.
        ActivateWindow();

        // The popup may never become the active window and then OnDeactivate would not fire.
        // A mouse hook closes it on any click outside instead.
        _mouseHook = new GlobalMouseHook();
        _mouseHook.ButtonPressed += HandleGlobalButtonPressed;
        _mouseHook.Install();
    }

    private void ActivateWindow()
    {
        WindowActivation.BringToForeground(Handle);
        Activate();
        _list.Focus();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _moreHistoryTimer.Stop();
        _moreHistoryTimer.Dispose();
        ReleaseMouseHook();
        base.OnFormClosed(e);
    }

    private void HandleGlobalButtonPressed(object? sender, IntPtr window)
    {
        if (_closing || IsDisposed || !Visible || window == Handle)
        {
            return;
        }

        // The older clips are hidden by an outside click, the popup with the usual clips stays open.
        if (_moreHistoryShown)
        {
            BeginInvoke((Action)HideMoreHistory);
            return;
        }

        _closing = true;
        BeginInvoke((Action)Close);
    }

    private void ReleaseMouseHook()
    {
        if (_mouseHook is null)
        {
            return;
        }

        _mouseHook.ButtonPressed -= HandleGlobalButtonPressed;
        _mouseHook.Dispose();
        _mouseHook = null;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        var modifiers = keyData & Keys.Modifiers;

        if (modifiers == Keys.None)
        {
            switch (keyCode)
            {
                case Keys.Escape:
                    Close();
                    return true;

                case Keys.Up:
                    _list.MoveSelection(-1);
                    return true;

                case Keys.Down:
                    _list.MoveSelection(1);
                    return true;

                case Keys.Enter:
                    _list.ActivateSelected();
                    return true;

                case Keys.PageUp:
                    _list.MoveSelection(-5);
                    return true;

                case Keys.PageDown:
                    _list.MoveSelection(5);
                    return true;

                case Keys.Home:
                    _list.MoveSelection(int.MinValue);
                    return true;

                case Keys.End:
                    _list.MoveSelection(int.MaxValue);
                    return true;
            }

            if (_list.TryActivateAccelerator(KeyCodeToAccelerator(keyCode)))
            {
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);

        if (IsDisposed)
        {
            return;
        }

        // The older clips are only hidden, because the popup with the usual clips is still wanted.
        if (_moreHistoryShown)
        {
            HideMoreHistory();
            return;
        }

        Close();
    }

    private static char KeyCodeToAccelerator(Keys keyCode)
    {
        if (keyCode is >= Keys.D0 and <= Keys.D9)
        {
            return (char)('0' + (keyCode - Keys.D0));
        }

        if (keyCode is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            return (char)('0' + (keyCode - Keys.NumPad0));
        }

        if (keyCode is >= Keys.A and <= Keys.Z)
        {
            return (char)('A' + (keyCode - Keys.A));
        }

        return '\0';
    }

    private void HandleItemActivated(object? sender, MenuRow row)
    {
        if (row.Command == PasteMenuCommand.MoreHistory)
        {
            ShowMoreHistory();
            return;
        }

        Result = row.Entry is not null
            ? new PasteMenuResult(PasteMenuCommand.Paste, row.Entry)
            : new PasteMenuResult(row.Command, null);

        Close();
    }

    private void HandleItemHovered(object? sender, MenuRow row)
    {
        // The older clips only appear when the pointer rests on the command for a moment, so a slide
        // over the row does not expand the popup by accident.
        if (row.Command == PasteMenuCommand.MoreHistory)
        {
            StartMoreHistoryTimer();
            return;
        }

        StopMoreHistoryTimer();
    }

    private void HandleHoverLeft(object? sender, EventArgs e)
    {
        StopMoreHistoryTimer();
    }

    private void StartMoreHistoryTimer()
    {
        if (_moreHistoryShown || _moreHistoryProvider is null || _moreHistoryTimer.Enabled)
        {
            return;
        }

        _moreHistoryTimer.Start();
    }

    private void StopMoreHistoryTimer()
    {
        if (_moreHistoryTimer.Enabled)
        {
            _moreHistoryTimer.Stop();
        }
    }

    private void HandleMoreHistoryTimerTick(object? sender, EventArgs e)
    {
        StopMoreHistoryTimer();
        ShowMoreHistory();
    }

    private void ShowMoreHistory()
    {
        StopMoreHistoryTimer();

        // The row is activated by hovering it, so the archive is queried only once.
        if (_moreHistoryShown)
        {
            return;
        }

        // Only the older clips are listed, the clips that are already shown are skipped.
        var entries = _moreHistoryProvider?.Invoke() ?? [];
        if (entries.Count == 0)
        {
            return;
        }

        _moreHistoryShown = true;

        var rows = BuildRows(entries, _stickies, showMoreHistory: false);

        _list.SetRows(rows, expanded: true);
        _list.SelectInitial(0);

        ResizeToContent();
        PositionNearCursor();

        _list.Focus();
    }

    /// <summary>
    /// Hides the older clips again, so the popup shows the usual clipboard history.
    /// </summary>
    private void HideMoreHistory()
    {
        StopMoreHistoryTimer();

        if (!_moreHistoryShown)
        {
            return;
        }

        _moreHistoryShown = false;

        _list.SetRows([.. _defaultRows], expanded: false);
        _list.SelectInitial(_initialRow);

        ResizeToContent();
    }

    private void ResizeToContent()
    {
        var size = _list.MeasureContent();
        ClientSize = new Size(size.Width + BorderWidth, size.Height + BorderWidth);
    }

    private void PositionNearCursor()
    {
        var cursor = Cursor.Position;
        var workingArea = Screen.FromPoint(cursor).WorkingArea;

        var x = cursor.X;
        var y = cursor.Y;

        if (x + Width > workingArea.Right)
        {
            x = workingArea.Right - Width;
        }

        if (y + Height > workingArea.Bottom)
        {
            y = workingArea.Bottom - Height;
        }

        x = Math.Max(workingArea.Left, x);
        y = Math.Max(workingArea.Top, y);

        Location = new Point(x, y);
    }

    private static List<MenuRow> BuildRows(
        IReadOnlyList<ClipEntry> history,
        IReadOnlyList<ClipEntry> stickies,
        bool showMoreHistory)
    {
        List<MenuRow> rows = [];

        for (var index = 0; index < history.Count; index++)
        {
            rows.Add(MenuRow.ForEntry(history[index], index < 9 ? (index + 1).ToString() : string.Empty));
        }

        rows.Add(MenuRow.Separator());

        if (showMoreHistory)
        {
            rows.Add(MenuRow.ForCommand("More History", PasteMenuCommand.MoreHistory));
        }

        rows.Add(MenuRow.ForCommand("Clear Clipboard content", PasteMenuCommand.ClearClipboard));

        if (stickies.Count > 0)
        {
            rows.Add(MenuRow.Separator());

            // "A" opens the archive, so the stickies start at "B" and use the remaining 25 letters.
            for (var index = 0; index < stickies.Count; index++)
            {
                var accelerator = index < 25 ? ((char)('B' + index)).ToString() : string.Empty;
                rows.Add(MenuRow.ForEntry(stickies[index], accelerator));
            }
        }

        rows.Add(MenuRow.ForCommand("Add Clipboard content to Stickies", PasteMenuCommand.AddClipboardToStickies));
        rows.Add(MenuRow.Separator());
        rows.Add(MenuRow.ForCommand("Archive...", PasteMenuCommand.Archive, ArchiveAccelerator));
        rows.Add(MenuRow.ForCommand("Configure", PasteMenuCommand.Configure));
        rows.Add(MenuRow.ForCommand("Close", PasteMenuCommand.Close));

        return rows;
    }

    private sealed class MenuRow
    {
        private MenuRow()
        {
        }

        public string Accelerator { get; private init; } = string.Empty;

        public string Text { get; private init; } = string.Empty;

        public ClipEntry? Entry { get; private init; }

        public PasteMenuCommand Command { get; private init; }

        public bool IsSeparator { get; private init; }

        public static MenuRow Separator() => new() { IsSeparator = true };

        public static MenuRow ForEntry(ClipEntry entry, string accelerator) => new()
        {
            Entry = entry,
            Text = entry.DisplayText,
            Accelerator = accelerator
        };

        public static MenuRow ForCommand(string text, PasteMenuCommand command, string accelerator = "") => new()
        {
            Text = text,
            Command = command,
            Accelerator = accelerator
        };
    }

    private sealed class MenuListControl : Control
    {
        private const int HorizontalPadding = 8;
        private const int AcceleratorSpacing = 10;
        private const int MinimumWidth = 240;
        private const int AutoWidth = 620;
        private const int MaximumWidth = 1200;
        private const int ResizeGripWidth = 6;

        private readonly List<MenuRow> _rows;
        private readonly int _preferredWidth;
        private int _selectedIndex = -1;
        private int _hoveredIndex = -1;
        private int _scrollOffset;
        private int _acceleratorWidth;
        private bool _expanded;
        private bool _resizingWidth;
        private int _resizeStartCursorX;
        private int _resizeStartWidth;

        public bool WidthResized { get; private set; }

        public MenuListControl(List<MenuRow> rows, int preferredWidth)
        {
            _rows = rows;
            _preferredWidth = preferredWidth;
            Font = Theme.BodyFont;
            BackColor = Theme.MenuBackground;
            UpdateMetrics();
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
        }

        public event EventHandler<MenuRow>? ItemActivated;

        /// <summary>
        /// Raised when the mouse pointer moves onto another row, so commands can react to hovering.
        /// </summary>
        public event EventHandler<MenuRow>? ItemHovered;

        /// <summary>
        /// Raised when the mouse pointer leaves the list, so a pending hover action can be cancelled.
        /// </summary>
        public event EventHandler? HoverLeft;

        public event EventHandler? CancelRequested;

        private int ItemHeight => Font.Height + 6;

        private int SeparatorHeight => 8;

        /// <summary>
        /// Replaces the rows, optionally expanding the list to the full screen height for older clips.
        /// </summary>
        public void SetRows(List<MenuRow> rows, bool expanded)
        {
            _rows.Clear();
            _rows.AddRange(rows);
            _expanded = expanded;
            _selectedIndex = -1;
            _hoveredIndex = -1;
            _scrollOffset = 0;
            UpdateMetrics();
            Invalidate();
        }

        public Size MeasureContent()
        {
            var textWidth = _rows
                .Where(row => !row.IsSeparator)
                .Select(row => TextRenderer.MeasureText(row.Text, Font, new Size(int.MaxValue, ItemHeight)).Width)
                .DefaultIfEmpty(0)
                .Max();

            var width = _preferredWidth > 0
                ? ClampWidth(_preferredWidth)
                : Math.Clamp(
                    HorizontalPadding + _acceleratorWidth + AcceleratorSpacing + textWidth + HorizontalPadding + 12,
                    MinimumWidth,
                    AutoWidth);

            var contentHeight = ContentHeight;
            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;

            // Older clips use the full height of the screen so that as many clips as possible are visible.
            var maxHeight = _expanded
                ? Math.Max(0, workingArea.Height - PasteMenuForm.BorderWidth)
                : (workingArea.Height * 6) / 10;

            return new Size(width, Math.Min(contentHeight, maxHeight));
        }

        public void SelectInitial(int index)
        {
            _selectedIndex = index >= 0 && index < _rows.Count && _rows[index].Entry is not null ? index : -1;
            _scrollOffset = 0;
            Invalidate();
        }

        public void MoveSelection(int delta)
        {
            if (_rows.Count == 0)
            {
                return;
            }

            if (delta == int.MinValue)
            {
                SelectFirst();
                return;
            }

            if (delta == int.MaxValue)
            {
                SelectLast();
                return;
            }

            var index = _selectedIndex;
            var remaining = Math.Abs(delta);
            var step = Math.Sign(delta);

            while (remaining > 0)
            {
                index = FindNextSelectable(index, step);
                remaining--;
            }

            _selectedIndex = index;
            EnsureSelectionVisible();
            Invalidate();
        }

        public void ActivateSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                return;
            }

            var row = _rows[_selectedIndex];
            if (!row.IsSeparator)
            {
                ItemActivated?.Invoke(this, row);
            }
        }

        public bool TryActivateAccelerator(char accelerator)
        {
            if (accelerator == '\0')
            {
                return false;
            }

            var comparison = char.IsDigit(accelerator)
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            var row = _rows.FirstOrDefault(candidate =>
                !candidate.IsSeparator &&
                candidate.Accelerator.Length > 0 &&
                string.Equals(candidate.Accelerator, accelerator.ToString(), comparison));

            if (row is null)
            {
                return false;
            }

            ItemActivated?.Invoke(this, row);
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
            using (var background = new SolidBrush(Theme.MenuBackground))
            {
                e.Graphics.FillRectangle(background, ClientRectangle);
            }

            var acceleratorWidth = _acceleratorWidth;
            var textLeft = HorizontalPadding + acceleratorWidth + AcceleratorSpacing;
            var y = 2 - _scrollOffset;

            for (var index = 0; index < _rows.Count; index++)
            {
                var row = _rows[index];
                var rowHeight = row.IsSeparator ? SeparatorHeight : ItemHeight;

                if (y + rowHeight >= 0 && y <= Height)
                {
                    if (row.IsSeparator)
                    {
                        var lineY = y + (SeparatorHeight / 2);
                        using var separatorPen = new Pen(Theme.SeparatorColor);
                        e.Graphics.DrawLine(separatorPen, 6, lineY, Width - 6, lineY);
                    }
                    else
                    {
                        var selected = index == _selectedIndex;
                        var bounds = new Rectangle(2, y, Width - 4, ItemHeight);

                        if (selected)
                        {
                            using var highlight = new SolidBrush(Theme.MenuHighlight);
                            e.Graphics.FillRectangle(highlight, bounds);
                        }

                        var foreground = selected ? SystemColors.HighlightText : SystemColors.MenuText;
                        TextRenderer.DrawText(
                            e.Graphics,
                            row.Accelerator,
                            Font,
                            new Rectangle(HorizontalPadding, y, acceleratorWidth, ItemHeight),
                            foreground,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                        var textBounds = new Rectangle(textLeft, y, Width - textLeft - 6, ItemHeight);
                        TextRenderer.DrawText(
                            e.Graphics,
                            row.Text,
                            Font,
                            textBounds,
                            foreground,
                            TextFormatFlags.VerticalCenter |
                            TextFormatFlags.Left |
                            TextFormatFlags.EndEllipsis |
                            TextFormatFlags.NoPrefix);
                    }
                }

                y += rowHeight;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_resizingWidth)
            {
                ResizeTo(_resizeStartWidth + (Cursor.Position.X - _resizeStartCursorX));
                return;
            }

            var resizeCursor = IsInResizeGrip(e.X) ? Cursors.SizeWE : Cursors.Default;
            if (!ReferenceEquals(Cursor, resizeCursor))
            {
                Cursor = resizeCursor;
            }

            var index = HitTest(e.Y);
            if (index == _hoveredIndex)
            {
                return;
            }

            _hoveredIndex = index;

            if (index < 0)
            {
                // The pointer is on a separator or the empty space below the rows, so the hover ends.
                HoverLeft?.Invoke(this, EventArgs.Empty);
                return;
            }

            _selectedIndex = index;
            Invalidate();
            ItemHovered?.Invoke(this, _rows[index]);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            // The right edge of the popup is a resize grip instead of a menu item.
            if (IsInResizeGrip(e.X))
            {
                _resizingWidth = true;
                _resizeStartCursorX = Cursor.Position.X;
                _resizeStartWidth = Width;
                Capture = true;
                return;
            }

            var index = HitTest(e.Y);
            if (index < 0)
            {
                return;
            }

            _selectedIndex = index;
            Invalidate();
            ItemActivated?.Invoke(this, _rows[index]);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (!_resizingWidth)
            {
                return;
            }

            _resizingWidth = false;
            Capture = false;
            Cursor = IsInResizeGrip(e.X) ? Cursors.SizeWE : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (!_resizingWidth)
            {
                Cursor = Cursors.Default;
                _hoveredIndex = -1;
                HoverLeft?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            _scrollOffset = Math.Clamp(_scrollOffset - (e.Delta / 120 * ItemHeight), 0, MaxScrollOffset);
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData is Keys.Up or Keys.Down or Keys.Enter or Keys.Escape or Keys.PageUp or Keys.PageDown
                || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    MoveSelection(-1);
                    e.Handled = true;
                    break;

                case Keys.Down:
                    MoveSelection(1);
                    e.Handled = true;
                    break;

                case Keys.Enter:
                    ActivateSelected();
                    e.Handled = true;
                    break;

                case Keys.Escape:
                    CancelRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        private int ContentHeight
        {
            get
            {
                var height = 4;
                foreach (var row in _rows)
                {
                    height += row.IsSeparator ? SeparatorHeight : ItemHeight;
                }

                return height;
            }
        }

        private int MaxScrollOffset => Math.Max(0, ContentHeight - Height);

        private void UpdateMetrics()
        {
            _acceleratorWidth = _rows
                .Where(row => !row.IsSeparator)
                .Select(row => TextRenderer.MeasureText(row.Accelerator, Font).Width)
                .DefaultIfEmpty(0)
                .Max();
        }

        private bool IsInResizeGrip(int x)
        {
            return x >= Width - ResizeGripWidth;
        }

        private int ClampWidth(int width)
        {
            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;

            return Math.Clamp(width, MinimumWidth, Math.Min(MaximumWidth, workingArea.Width));
        }

        private void ResizeTo(int contentWidth)
        {
            var form = FindForm();
            if (form is null)
            {
                return;
            }

            // The list is docked inside the 1 pixel border of the popup, so the border has to grow too.
            var clientWidth = ClampWidth(contentWidth) + PasteMenuForm.BorderWidth;
            if (form.ClientSize.Width == clientWidth)
            {
                return;
            }

            form.ClientSize = new Size(clientWidth, form.ClientSize.Height);
            WidthResized = true;
        }

        private int HitTest(int y)
        {
            var currentY = 2 - _scrollOffset;

            for (var index = 0; index < _rows.Count; index++)
            {
                var row = _rows[index];
                var rowHeight = row.IsSeparator ? SeparatorHeight : ItemHeight;

                if (y >= currentY && y < currentY + rowHeight)
                {
                    return row.IsSeparator ? -1 : index;
                }

                currentY += rowHeight;
            }

            return -1;
        }

        private int FindNextSelectable(int index, int step)
        {
            var next = index;

            for (var attempt = 0; attempt < _rows.Count; attempt++)
            {
                next += step;

                if (next < 0)
                {
                    next = _rows.Count - 1;
                }
                else if (next >= _rows.Count)
                {
                    next = 0;
                }

                if (!_rows[next].IsSeparator)
                {
                    return next;
                }
            }

            return index;
        }

        private void SelectFirst()
        {
            var index = _rows.FindIndex(row => row.Entry is not null);
            _selectedIndex = index >= 0 ? index : FindNextSelectable(-1, 1);
            EnsureSelectionVisible();
            Invalidate();
        }

        private void SelectLast()
        {
            _selectedIndex = _rows.FindLastIndex(row => !row.IsSeparator);
            EnsureSelectionVisible();
            Invalidate();
        }

        private void EnsureSelectionVisible()
        {
            if (_selectedIndex < 0)
            {
                return;
            }

            var top = 2;
            for (var index = 0; index < _selectedIndex; index++)
            {
                top += _rows[index].IsSeparator ? SeparatorHeight : ItemHeight;
            }

            var height = _rows[_selectedIndex].IsSeparator ? SeparatorHeight : ItemHeight;

            if (top - _scrollOffset < 0)
            {
                _scrollOffset = top - 2;
            }
            else if (top + height - _scrollOffset > Height)
            {
                _scrollOffset = top + height - Height + 2;
            }

            _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset);
        }
    }
}
