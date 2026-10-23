using System.Diagnostics;

namespace ClipboardManagerWinForm.UI;

internal static class Ui
{
    public static Label Label(string text, int x, int y, int width, bool bold = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(width, 18),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = bold ? Theme.BoldFont : Theme.BodyFont
        };
    }

    public static Label WrapLabel(string text, int x, int y, int width, int height)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(width, height),
            TextAlign = ContentAlignment.TopLeft,
            Font = Theme.BodyFont
        };
    }

    public static LinkLabel LinkLabel(string text, string url, int x, int y, int width)
    {
        var linkLabel = new LinkLabel
        {
            Text = text,
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(width, 18),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Theme.BodyFont,
            LinkBehavior = LinkBehavior.SystemDefault,
            Cursor = Cursors.Hand
        };

        linkLabel.LinkClicked += (_, _) => OpenUrl(url);
        return linkLabel;
    }

    public static Button Button(string text, int x, int y, int width, EventHandler? onClick = null, int height = 25)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            UseVisualStyleBackColor = true,
            Font = Theme.BodyFont
        };

        if (onClick is not null)
        {
            button.Click += onClick;
        }

        return button;
    }

    public static CheckBox CheckBox(string text, int x, int y, int width, bool isChecked = false, EventHandler? onCheckedChanged = null)
    {
        var checkBox = new CheckBox
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 20),
            Checked = isChecked,
            UseVisualStyleBackColor = true,
            Font = Theme.BodyFont
        };

        if (onCheckedChanged is not null)
        {
            checkBox.CheckedChanged += onCheckedChanged;
        }

        return checkBox;
    }

    public static RadioButton RadioButton(string text, int x, int y, int width, bool isChecked = false)
    {
        return new RadioButton
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 20),
            Checked = isChecked,
            UseVisualStyleBackColor = true,
            Font = Theme.BodyFont
        };
    }

    public static TextBox TextBox(int x, int y, int width, string text = "", int height = 23, bool readOnly = false)
    {
        return new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            Text = text,
            ReadOnly = readOnly,
            Font = Theme.BodyFont,
            BackColor = SystemColors.Window
        };
    }

    public static TextBox MultilineTextBox(
        int x,
        int y,
        int width,
        int height,
        bool readOnly = false,
        ScrollBars scrollBars = ScrollBars.Vertical)
    {
        return new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            Multiline = true,
            ReadOnly = readOnly,
            ScrollBars = scrollBars,
            BackColor = SystemColors.Window,
            Font = Theme.BodyFont,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    public static NumericUpDown Numeric(int x, int y, int width, decimal minimum, decimal maximum, decimal value)
    {
        return new NumericUpDown
        {
            Location = new Point(x, y),
            Size = new Size(width, 23),
            Minimum = minimum,
            Maximum = maximum,
            Value = Math.Clamp(value, minimum, maximum),
            TextAlign = HorizontalAlignment.Right,
            Font = Theme.BodyFont
        };
    }

    public static ComboBox Combo(int x, int y, int width)
    {
        return new ComboBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.BodyFont
        };
    }

    public static GroupBox Group(string text, int x, int y, int width, int height)
    {
        return new GroupBox
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            Font = Theme.BodyFont
        };
    }

    public static ToolStrip ToolStrip(int x, int y, int width, params ToolStripItem[] items)
    {
        var strip = new ToolStrip
        {
            Location = new Point(x, y),
            Size = new Size(width, 25),
            Dock = DockStyle.None,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            Font = Theme.BodyFont,
            Padding = new Padding(1, 0, 1, 0),
            AutoSize = false,
            ImageScalingSize = new Size(16, 16),
            CanOverflow = false
        };

        strip.Items.AddRange(items);
        return strip;
    }

    public static ToolStripButton ToolButton(ToolIcon icon, string toolTip, EventHandler? onClick = null)
    {
        var button = new ToolStripButton
        {
            Image = ToolIcons.Get(icon),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = toolTip,
            AutoSize = false,
            Size = new Size(23, 22)
        };

        if (onClick is not null)
        {
            button.Click += onClick;
        }

        return button;
    }

    public static ToolStripSeparator ToolSeparator() => new();

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // A missing default browser must never break the window that shows the link.
        }
    }
}
