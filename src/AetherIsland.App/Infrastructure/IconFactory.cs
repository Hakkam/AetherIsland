using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;

namespace AetherIsland.App.Infrastructure;

/// <summary>Generates the application icon at runtime (rounded pill with a dot) — no binary asset needed.</summary>
public static class IconFactory
{
    public static System.Drawing.Icon CreateAppIcon(int size = 32)
    {
        using var bmp = new System.Drawing.Bitmap(size, size);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            float m = size * 0.08f;
            var rect = new System.Drawing.RectangleF(m, m, size - 2 * m, size - 2 * m);
            using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(
                rect,
                System.Drawing.Color.FromArgb(0x32, 0xA8, 0xFF),
                System.Drawing.Color.FromArgb(0xA5, 0x8B, 0xFF),
                35f);
            var radius = rect.Height / 2f;
            using var path = RoundedRect(rect, radius);
            g.FillPath(bg, path);

            // white "island" pill in the middle
            float iw = rect.Width * 0.42f, ih = rect.Height * 0.18f;
            var inner = new System.Drawing.RectangleF(
                rect.X + (rect.Width - iw) / 2f,
                rect.Y + (rect.Height - ih) / 2f, iw, ih);
            using var white = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            using var innerPath = RoundedRect(inner, ih / 2f);
            g.FillPath(white, innerPath);
        }
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public static BitmapSource CreateIconSource(int size = 32)
    {
        var icon = CreateAppIcon(size);
        var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        src.Freeze();
        return src;
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(System.Drawing.RectangleF r, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
