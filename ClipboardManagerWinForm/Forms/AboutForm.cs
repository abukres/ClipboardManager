using ClipboardManagerWinForm.UI;

namespace ClipboardManagerWinForm.Forms;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        Font = Theme.BodyFont;
        Text = "About Clipboard Manager";
        ClientSize = new Size(440, 236);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Icon = Theme.ApplicationIcon;
        BackColor = SystemColors.Control;

        var iconBox = new PictureBox
        {
            Location = new Point(16, 16),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Image = Theme.ApplicationIcon.ToBitmap()
        };
        Controls.Add(iconBox);

        Controls.Add(Ui.Label("Clipboard Manager", 78, 16, 330, bold: true));

        var versionLabel = Ui.Label($"Version {Application.ProductVersion}", 78, 38, 330);
        Controls.Add(versionLabel);

        Controls.Add(Ui.WrapLabel(
            "A clipboard history manager for Windows. It remembers the text you copy and lets you " +
            "paste it back into any application, keeps an archive in a SQLite database and stays " +
            "out of your way in the system tray.",
            16,
            78,
            406,
            90));

        Controls.Add(Ui.LinkLabel(
            "https://github.com/abukres/ClipboardManager",
            "https://github.com/abukres/ClipboardManager",
            16,
            172,
            406));

        Controls.Add(Ui.Button("OK", 348, 196, 78, (_, _) => Close()));

        AcceptButton = (Button)Controls[Controls.Count - 1];
    }
}
