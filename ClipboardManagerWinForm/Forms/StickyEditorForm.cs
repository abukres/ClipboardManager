using ClipboardManagerWinForm.Data;
using ClipboardManagerWinForm.UI;

namespace ClipboardManagerWinForm.Forms;

public sealed class StickyEditorForm : Form
{
    private readonly ClipboardRepository _repository;
    private readonly ClipEntry? _entry;
    private readonly TextBox _textBox;

    public StickyEditorForm(ClipboardRepository repository, ClipEntry? entry, string? initialText = null)
    {
        _repository = repository;
        _entry = entry;

        AutoScaleMode = AutoScaleMode.None;
        Font = Theme.BodyFont;
        Text = entry is null ? "Add Sticky" : "Edit Sticky";
        ClientSize = new Size(440, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Icon = Theme.ApplicationIcon;
        BackColor = SystemColors.Control;

        Controls.Add(Ui.Label("Sticky text", 12, 10, 200, bold: true));

        _textBox = Ui.MultilineTextBox(12, 32, 416, 180);
        _textBox.Text = entry?.Text ?? initialText ?? string.Empty;
        Controls.Add(_textBox);

        var okButton = Ui.Button("OK", 260, 226, 78, (_, _) => Save());
        var cancelButton = Ui.Button("Cancel", 350, 226, 78, (_, _) => Close());
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        if (entry is not null)
        {
            var deleteButton = Ui.Button("Delete", 12, 226, 90, (_, _) => Delete());
            Controls.Add(deleteButton);
        }

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void Save()
    {
        var text = _textBox.Text.Trim();
        if (text.Length == 0)
        {
            MessageBox.Show(this, "Please enter the text for the sticky clip.", "Sticky", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _textBox.Focus();
            return;
        }

        if (_entry is null)
        {
            _repository.AddSticky(text);
        }
        else
        {
            _repository.UpdateSticky(_entry.Id, text);
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void Delete()
    {
        if (_entry is null)
        {
            return;
        }

        _repository.DeleteSticky(_entry.Id);
        DialogResult = DialogResult.OK;
        Close();
    }
}
