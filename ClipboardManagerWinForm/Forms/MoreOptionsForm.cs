using ClipboardManagerWinForm.Services;
using ClipboardManagerWinForm.UI;

namespace ClipboardManagerWinForm.Forms;

public sealed class MoreOptionsForm : Form
{
    private const uint SoundAsync = 0x0001;
    private const uint SoundFileName = 0x00020000;

    private readonly ClipboardApplicationContext _context;
    private readonly AppSettings _draft;

    private readonly CheckBox _playSound;
    private readonly TextBox _soundFile;
    private readonly RadioButton _trayDoubleClick;
    private readonly RadioButton _cursorFirst;
    private readonly ComboBox _doubleClickAction;
    private readonly CheckBox _askBeforeDeleting;
    private readonly TextBox _ignoreContaining;
    private readonly NumericUpDown _maxSearchResults;

    public MoreOptionsForm(ClipboardApplicationContext context)
    {
        _context = context;
        _draft = context.Settings.Clone();

        AutoScaleMode = AutoScaleMode.None;
        Font = Theme.BodyFont;
        Text = "Clipboard Manager More options";
        ClientSize = new Size(624, 492);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Icon = Theme.ApplicationIcon;
        BackColor = SystemColors.Control;

        _soundFile = Ui.TextBox(10, 34, 546, _draft.SoundFile);
        _soundFile.Enabled = _draft.PlaySoundOnChange;
        Controls.Add(_soundFile);

        _playSound = Ui.CheckBox(
            "Play sound when clipboard content changes",
            10,
            10,
            340,
            _draft.PlaySoundOnChange,
            OnPlaySoundChanged);
        Controls.Add(_playSound);
        Controls.Add(Ui.Button("...", 560, 32, 34, (_, _) => BrowseSound(), 23));

        var playButton = Ui.Button(string.Empty, 596, 32, 24, (_, _) => PlaySound(), 23);
        playButton.Image = ToolIcons.Get(ToolIcon.Play);
        Controls.Add(playButton);

        var trayGroup = Ui.Group("Tray Icon - show configuration window using", 10, 68, 602, 62);
        _trayDoubleClick = new RadioButton
        {
            Text = "Left mouse double click",
            Location = new Point(16, 22),
            Size = new Size(240, 20),
            Checked = _draft.TrayLeftDoubleClickOpensConfiguration,
            Font = Theme.BodyFont
        };
        var trayRightClick = new RadioButton
        {
            Text = "Right mouse click",
            Location = new Point(300, 22),
            Size = new Size(240, 20),
            Checked = !_draft.TrayLeftDoubleClickOpensConfiguration,
            Font = Theme.BodyFont
        };
        trayGroup.Controls.Add(_trayDoubleClick);
        trayGroup.Controls.Add(trayRightClick);
        Controls.Add(trayGroup);

        var cursorGroup = Ui.Group("Place cursor in paste menu at", 10, 138, 602, 60);
        _cursorFirst = new RadioButton
        {
            Text = "First item",
            Location = new Point(16, 20),
            Size = new Size(240, 20),
            Checked = _draft.PasteMenuCursorPosition <= 0,
            Font = Theme.BodyFont
        };
        var cursorSecond = new RadioButton
        {
            Text = "Second item",
            Location = new Point(300, 20),
            Size = new Size(240, 20),
            Checked = _draft.PasteMenuCursorPosition > 0,
            Font = Theme.BodyFont
        };
        cursorGroup.Controls.Add(_cursorFirst);
        cursorGroup.Controls.Add(cursorSecond);
        Controls.Add(cursorGroup);

        Controls.Add(Ui.Label("List doubleclick action", 10, 208, 125));
        _doubleClickAction = Ui.Combo(137, 204, 205);
        _doubleClickAction.Items.AddRange(["Copy to clipboard", "Paste to active application"]);
        _doubleClickAction.SelectedIndex = _draft.DoubleClickPastesToApplication ? 1 : 0;
        Controls.Add(_doubleClickAction);

        _askBeforeDeleting = Ui.CheckBox(
            "Ask before deleting",
            380,
            206,
            220,
            _draft.AskBeforeDeleting);
        Controls.Add(_askBeforeDeleting);

        Controls.Add(Ui.Label("Ignore if text contains [separator ;]", 10, 242, 260));
        _ignoreContaining = Ui.TextBox(230, 240, 160, _draft.IgnoreContaining);
        Controls.Add(_ignoreContaining);
        Controls.Add(Ui.Button("?", 396, 238, 24, (_, _) => ShowIgnoreHelp(), 22));

        Controls.Add(Ui.Label("Maximum archive search results", 10, 276, 200));
        _maxSearchResults = Ui.Numeric(215, 274, 80, 10, 100000, _draft.MaxArchiveSearchResults);
        Controls.Add(_maxSearchResults);

        Controls.Add(Ui.Label("Language", 10, 308, 70));
        var language = Ui.Combo(137, 304, 160);
        language.Items.Add("English");
        language.SelectedIndex = 0;
        Controls.Add(language);

        Controls.Add(Ui.Label("Data files currently in use", 10, 336, 260));
        var files = new ListBox
        {
            Location = new Point(10, 356),
            Size = new Size(602, 84),
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.BodyFont
        };
        files.Items.Add(_context.Repository.Database.FilePath);
        files.Items.Add(_context.SettingsPath);
        Controls.Add(files);

        var okButton = Ui.Button("OK", 440, 452, 78, (_, _) => Commit());
        var cancelButton = Ui.Button("Cancel", 534, 452, 78, (_, _) => Close());
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void OnPlaySoundChanged(object? sender, EventArgs e)
    {
        _soundFile.Enabled = _playSound.Checked;
    }

    private void BrowseSound()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select a sound file",
            Filter = "Wave files (*.wav)|*.wav|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _soundFile.Text = dialog.FileName;
            _playSound.Checked = true;
        }
    }

    private void PlaySound()
    {
        if (string.IsNullOrWhiteSpace(_soundFile.Text) || !File.Exists(_soundFile.Text))
        {
            return;
        }

        Interop.NativeMethods.PlaySound(_soundFile.Text, IntPtr.Zero, SoundAsync | SoundFileName);
    }

    private void ShowIgnoreHelp()
    {
        MessageBox.Show(
            this,
            "List the texts you do not want to store, separated by a semicolon. " +
            "Example: password;secret;credit card",
            "Ignore text",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void Commit()
    {
        _draft.PlaySoundOnChange = _playSound.Checked;
        _draft.SoundFile = _soundFile.Text.Trim();
        _draft.TrayLeftDoubleClickOpensConfiguration = _trayDoubleClick.Checked;
        _draft.PasteMenuCursorPosition = _cursorFirst.Checked ? 0 : 1;
        _draft.DoubleClickPastesToApplication = _doubleClickAction.SelectedIndex == 1;
        _draft.AskBeforeDeleting = _askBeforeDeleting.Checked;
        _draft.IgnoreContaining = _ignoreContaining.Text;
        _draft.IgnoreContainingEnabled = !string.IsNullOrWhiteSpace(_ignoreContaining.Text);
        _draft.MaxArchiveSearchResults = (int)_maxSearchResults.Value;

        _context.ReplaceSettings(_draft);
        DialogResult = DialogResult.OK;
        Close();
    }
}
