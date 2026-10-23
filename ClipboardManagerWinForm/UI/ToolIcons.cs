using System.Drawing.Drawing2D;

namespace ClipboardManagerWinForm.UI;

public enum ToolIcon
{
    Search,
    Clear,
    Copy,
    Delete,
    Add,
    Edit,
    Help,
    Play
}

public static class ToolIcons
{
    private static readonly Dictionary<ToolIcon, Image> Cache = [];

    public static Image Get(ToolIcon icon)
    {
        if (Cache.TryGetValue(icon, out var image))
        {
            return image;
        }

        image = Draw(icon);
        Cache[icon] = image;
        return image;
    }

    private static Image Draw(ToolIcon icon)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var blue = new Pen(Color.FromArgb(45, 90, 155), 1.6f);
        using var blueBrush = new SolidBrush(Color.FromArgb(45, 90, 155));
        using var gray = new Pen(Color.FromArgb(90, 90, 90), 1.4f);
        using var darkGray = new SolidBrush(Color.FromArgb(90, 90, 90));
        using var green = new SolidBrush(Color.FromArgb(40, 130, 60));
        using var greenPen = new Pen(Color.FromArgb(40, 130, 60), 1.8f);
        using var red = new Pen(Color.FromArgb(190, 50, 45), 2f);
        using var orange = new SolidBrush(Color.FromArgb(216, 138, 40));

        switch (icon)
        {
            case ToolIcon.Search:
                graphics.DrawEllipse(blue, 2f, 2f, 8f, 8f);
                graphics.DrawLine(blue, 9.5f, 9.5f, 13.5f, 13.5f);
                break;

            case ToolIcon.Clear:
                graphics.DrawLine(red, 3f, 3f, 12f, 12f);
                graphics.DrawLine(red, 12f, 3f, 3f, 12f);
                break;

            case ToolIcon.Copy:
                graphics.DrawRectangle(gray, 2f, 2f, 8f, 10f);
                graphics.DrawRectangle(gray, 5f, 5f, 8f, 9f);
                break;

            case ToolIcon.Delete:
                graphics.DrawLine(gray, 3f, 4f, 13f, 4f);
                graphics.DrawLine(gray, 6f, 2f, 10f, 2f);
                graphics.DrawRectangle(gray, 4.5f, 4.5f, 7f, 9.5f);
                graphics.DrawLine(gray, 7f, 6.5f, 7f, 12f);
                graphics.DrawLine(gray, 9f, 6.5f, 9f, 12f);
                break;

            case ToolIcon.Add:
                graphics.DrawLine(greenPen, 8f, 2.5f, 8f, 13.5f);
                graphics.DrawLine(greenPen, 2.5f, 8f, 13.5f, 8f);
                break;

            case ToolIcon.Edit:
                graphics.FillPolygon(orange, new[]
                {
                    new PointF(2f, 13.5f),
                    new PointF(4f, 13f),
                    new PointF(13f, 4f),
                    new PointF(11f, 2f),
                    new PointF(2.5f, 11.5f)
                });
                break;

            case ToolIcon.Help:
                graphics.FillEllipse(blueBrush, 1f, 1f, 13f, 13f);
                using (var font = new Font("Segoe UI", 8F, FontStyle.Bold, GraphicsUnit.Point))
                {
                    var textSize = graphics.MeasureString("?", font);
                    graphics.DrawString(
                        "?",
                        font,
                        Brushes.White,
                        8f - (textSize.Width / 2f),
                        8f - (textSize.Height / 2f) - 1f);
                }

                break;

            case ToolIcon.Play:
                graphics.FillPolygon(green, new[]
                {
                    new PointF(4f, 2.5f),
                    new PointF(13f, 8f),
                    new PointF(4f, 13.5f)
                });
                break;

            default:
                graphics.FillRectangle(darkGray, 3f, 3f, 10f, 10f);
                break;
        }

        return bitmap;
    }
}
