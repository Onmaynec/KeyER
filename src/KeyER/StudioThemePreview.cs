using System.Drawing.Drawing2D;

namespace KeyER;

/// <summary>
/// Live theme preview; paints a representative mini-workspace using the proposed
/// colors without changing the theme of the dialog until Save is pressed.
/// </summary>
internal sealed class StudioThemePreview : Control
{
    private AppearanceMode _appearance = AppearanceMode.Dark;
    private AccentTheme _accent = AccentTheme.Iris;
    public AppearanceMode PreviewAppearance
    {
        get => _appearance;
        set { _appearance = value; Invalidate(); }
    }
    public AccentTheme PreviewAccent
    {
        get => _accent;
        set { _accent = value; Invalidate(); }
    }

    public StudioThemePreview()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Height = 196;
        MinimumSize = new Size(200, 135);
        AccessibleName = "Предпросмотр цветовой темы";
        AccessibleDescription = "Макет интерфейса показывает тему и акцент до сохранения.";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Surface);

        var light = _appearance switch
        {
            AppearanceMode.Light => true,
            AppearanceMode.System => SystemInformation.HighContrast == false && Theme.IsLight,
            _ => false
        };
        var background = light ? Color.FromArgb(244, 246, 252) : Color.FromArgb(12, 15, 26);
        var surface = light ? Color.White : Color.FromArgb(23, 28, 44);
        var input = light ? Color.FromArgb(250, 251, 255) : Color.FromArgb(18, 22, 36);
        var ink = light ? Color.FromArgb(30, 40, 60) : Color.FromArgb(239, 243, 252);
        var muted = light ? Color.FromArgb(105, 117, 140) : Color.FromArgb(150, 163, 187);
        var accent = _accent switch
        {
            AccentTheme.Ocean => Color.FromArgb(42, 143, 237),
            AccentTheme.Mint => Color.FromArgb(32, 177, 137),
            AccentTheme.Amber => Color.FromArgb(235, 158, 59),
            _ => Color.FromArgb(139, 110, 246)
        };
        var frame = new Rectangle(4, 4, Width - 9, Height - 9);
        if (frame.Width <= 10 || frame.Height <= 10) return;

        using (var outline = Theme.Rounded(frame, 12))
        using (var brush = new SolidBrush(background))
        {
            g.FillPath(brush, outline);
            using var pen = new Pen(light ? Color.FromArgb(208, 216, 230) : Color.FromArgb(57, 67, 92));
            g.DrawPath(pen, outline);
        }

        var sidebarWidth = Math.Clamp(frame.Width / 4, 52, 128);
        using (var brush = new SolidBrush(surface))
            g.FillRectangle(brush, frame.X + 1, frame.Y + 1, sidebarWidth, frame.Height - 2);

        using var headerFont = Theme.UiFont(10F, FontStyle.Bold);
        using var bodyFont = Theme.UiFont(8.2F);
        var sidebarX = frame.Left + 13;
        TextRenderer.DrawText(g, "KeyER", headerFont,
            new Rectangle(sidebarX, frame.Top + 13, sidebarWidth - 18, 24), ink,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        using (var selected = Theme.Rounded(
            new Rectangle(sidebarX - 4, frame.Top + 52, sidebarWidth - 15, 23), 7))
        using (var b = new SolidBrush(accent)) g.FillPath(b, selected);
        TextRenderer.DrawText(g, "Text", bodyFont,
            new Rectangle(sidebarX + 4, frame.Top + 57, sidebarWidth - 31, 18),
            Color.White, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "Code", bodyFont,
            new Rectangle(sidebarX + 4, frame.Top + 87, sidebarWidth - 31, 18),
            muted, TextFormatFlags.NoPadding);

        var contentLeft = frame.Left + sidebarWidth + 15;
        var contentRight = frame.Right - 12;
        if (contentRight - contentLeft < 60) return;
        TextRenderer.DrawText(g, "Рабочее пространство", headerFont,
            new Rectangle(contentLeft, frame.Top + 12, contentRight - contentLeft, 24), ink,
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        var editorRect = new Rectangle(contentLeft, frame.Top + 43,
            contentRight - contentLeft, Math.Max(44, frame.Height - 105));
        using (var panel = Theme.Rounded(editorRect, 8))
        using (var b = new SolidBrush(input)) g.FillPath(b, panel);
        TextRenderer.DrawText(g, "01   using System;", bodyFont,
            new Rectangle(editorRect.Left + 8, editorRect.Top + 8, editorRect.Width - 12, 17),
            accent, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, "02   // Введите текст", bodyFont,
            new Rectangle(editorRect.Left + 8, editorRect.Top + 27, editorRect.Width - 12, 17),
            muted, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        var buttonBounds = new Rectangle(contentLeft, frame.Bottom - 44,
            Math.Min(150, Math.Max(40, contentRight - contentLeft - 15)), 27);
        using (var buttonPath = Theme.Rounded(buttonBounds, 8))
        using (var b = new SolidBrush(accent)) g.FillPath(b, buttonPath);
        TextRenderer.DrawText(g, "Начать набор", bodyFont, buttonBounds, Color.White,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }
}
