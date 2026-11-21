using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KeyER;

/// <summary>Original app mark drawn locally at runtime; no image assets needed.</summary>
internal static class BrandIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);

    public static Icon Create()
    {
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            var outer = new Rectangle(3, 3, 58, 58);
            using var path = Theme.Rounded(outer, 17);
            using var fill = new LinearGradientBrush(outer, Theme.AccentFill,
                ControlPaint.Dark(Theme.AccentFill, 0.22F), LinearGradientMode.ForwardDiagonal);
            graphics.FillPath(fill, path);
            using var shine = new Pen(Color.FromArgb(98, 255, 255, 255), 1.1F);
            graphics.DrawPath(shine, path);
            using var font = new Font("Segoe UI", 31, FontStyle.Bold, GraphicsUnit.Pixel);
            TextRenderer.DrawText(graphics, "K", font, new Rectangle(9, 10, 47, 44),
                Color.White, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally { _ = DestroyIcon(handle); }
    }
}
