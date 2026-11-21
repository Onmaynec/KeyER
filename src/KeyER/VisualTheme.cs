using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KeyER;

public enum AppearanceMode { Dark, Light, System }
public enum AccentTheme { Iris, Ocean, Mint, Amber }

/// <summary>Shared accessible palette, layout helpers and owner-painted widgets.</summary>
internal static class Theme
{
    private static AppearanceMode _mode = AppearanceMode.Dark;
    private static AccentTheme _accent = AccentTheme.Iris;
    public static bool IsLight => _mode == AppearanceMode.Light ||
        (_mode == AppearanceMode.System && SystemInformation.HighContrast == false &&
         IsWindowsLight());

    public static Color Background => IsLight ? Color.FromArgb(244, 246, 252) : Color.FromArgb(12, 15, 26);
    public static Color Surface => IsLight ? Color.White : Color.FromArgb(23, 28, 44);
    public static Color SurfaceRaised => IsLight ? Color.FromArgb(236, 240, 249) : Color.FromArgb(31, 37, 57);
    public static Color Input => IsLight ? Color.FromArgb(250, 251, 255) : Color.FromArgb(18, 22, 36);
    public static Color Foreground => IsLight ? Color.FromArgb(30, 40, 60) : Color.FromArgb(239, 243, 252);
    public static Color Muted => IsLight ? Color.FromArgb(107, 119, 139) : Color.FromArgb(152, 162, 184);
    public static Color Border => IsLight ? Color.FromArgb(215, 222, 236) : Color.FromArgb(51, 59, 82);
    public static Color Accent => _accent switch
    {
        AccentTheme.Ocean => IsLight ? Color.FromArgb(19, 116, 204) : Color.FromArgb(74, 168, 255),
        AccentTheme.Mint => IsLight ? Color.FromArgb(15, 130, 103) : Color.FromArgb(76, 217, 174),
        AccentTheme.Amber => IsLight ? Color.FromArgb(176, 96, 18) : Color.FromArgb(246, 182, 95),
        _ => IsLight ? Color.FromArgb(93, 74, 201) : Color.FromArgb(168, 140, 255)
    };
    public static Color AccentFill => _accent switch
    {
        AccentTheme.Ocean => Color.FromArgb(42, 143, 237),
        AccentTheme.Mint => Color.FromArgb(32, 177, 137),
        AccentTheme.Amber => Color.FromArgb(235, 158, 59),
        _ => Color.FromArgb(139, 110, 246)
    };
    public static Color AccentText => Color.White;
    public static Color Success => IsLight ? Color.FromArgb(15, 139, 105) : Color.FromArgb(92, 226, 177);
    public static Color Danger => IsLight ? Color.FromArgb(195, 66, 88) : Color.FromArgb(249, 124, 147);
    public static Color Warning => IsLight ? Color.FromArgb(181, 118, 19) : Color.FromArgb(246, 199, 99);

    private static bool IsWindowsLight()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme") ?? 0) == 1;
        }
        catch { return false; }
    }

    public static void Configure(AppearanceMode appearance, AccentTheme accent)
    {
        _mode = appearance;
        _accent = accent;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
        ref int value, int valueSize);

    public static void ApplyWindowFrame(Form window)
    {
        if (!window.IsHandleCreated) return;
        try
        {
            var dark = IsLight ? 0 : 1;
            var size = sizeof(int);
            if (DwmSetWindowAttribute(window.Handle, 20, ref dark, size) != 0)
                _ = DwmSetWindowAttribute(window.Handle, 19, ref dark, size);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Old Windows builds may not support immersive dark title bars.
        }
    }

    public static Font UiFont(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", size, style, GraphicsUnit.Point);

    public static Label Label(string text, float size = 9, bool bold = false)
    {
        return new Label
        {
            Text = text, ForeColor = Foreground, BackColor = Color.Transparent,
            AutoSize = true, Font = UiFont(size, bold ? FontStyle.Bold : FontStyle.Regular)
        };
    }

    public static void StyleButton(Button button, bool primary = false, bool danger = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.Transparent;
        button.FlatAppearance.MouseDownBackColor = Color.Transparent;
        button.BackColor = danger ? Danger : primary ? AccentFill : SurfaceRaised;
        button.ForeColor = primary ? Color.White : danger ? (IsLight ? Color.White : Background) : Foreground;
        button.Font = UiFont(9.4F, primary ? FontStyle.Bold : FontStyle.Regular);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(4);
    }

    public static void StyleCombo(ComboBox combo)
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.BackColor = SurfaceRaised;
        combo.ForeColor = Foreground;
        combo.Font = UiFont(9.6F);
        combo.Margin = new Padding(4, 7, 4, 4);
        combo.IntegralHeight = false;
    }

    public static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var width = Math.Min(radius * 2, Math.Max(1, bounds.Width));
        var height = Math.Min(radius * 2, Math.Max(1, bounds.Height));
        var d = Math.Min(width, height);
        if (d <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal enum ButtonTone { Quiet, Soft, Primary, Danger, Outline }

/// <summary>Native focus/keyboard accessible owner-painted rounded button.</summary>
internal sealed class StudioButton : Button
{
    private bool _hover;
    private bool _down;
    private ButtonTone _tone = ButtonTone.Soft;
    public ButtonTone Tone
    {
        get => _tone;
        set { _tone = value; Invalidate(); }
    }
    public int CornerRadius { get; set; } = 12;
    private bool _alignLeft;
    public bool AlignLeft
    {
        get => _alignLeft;
        set { _alignLeft = value; Invalidate(); }
    }

    public StudioButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = Theme.UiFont(9.4F, FontStyle.Bold);
        Height = 40;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        TabStop = true;
        Margin = new Padding(4, 0, 4, 0);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        using var shape = Theme.Rounded(bounds, CornerRadius);
        var fill = Tone switch
        {
            ButtonTone.Primary => Theme.AccentFill,
            ButtonTone.Danger => Theme.Danger,
            ButtonTone.Quiet => Theme.Background,
            ButtonTone.Outline => Theme.Surface,
            _ => Theme.SurfaceRaised
        };
        var textColor = Tone switch
        {
            ButtonTone.Primary => Color.White,
            ButtonTone.Danger => Theme.IsLight ? Color.White : Theme.Background,
            _ => Theme.Foreground
        };
        if (!Enabled)
        {
            fill = Theme.SurfaceRaised;
            textColor = Theme.Muted;
        }
        else if (_down) fill = ControlPaint.Dark(fill, 0.10F);
        else if (_hover) fill = ControlPaint.Light(fill, 0.075F);

        using (var brush = new SolidBrush(fill)) g.FillPath(brush, shape);
        if (Tone == ButtonTone.Outline || Focused)
        {
            using var pen = new Pen(Focused ? Theme.Accent : Theme.Border, Focused ? 1.8F : 1F);
            g.DrawPath(pen, shape);
        }
        var textBounds = _alignLeft
            ? new Rectangle(bounds.Left + 17, bounds.Top, Math.Max(0, bounds.Width - 31), bounds.Height)
            : bounds;
        TextRenderer.DrawText(g, Text, Font, textBounds, textColor,
            (_alignLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) |
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis);
        if (_alignLeft && Tone == ButtonTone.Primary && Enabled)
        {
            using var highlight = new SolidBrush(Color.FromArgb(185, 255, 255, 255));
            g.FillRectangle(highlight, bounds.Left + 7, bounds.Top + 11,
                3, Math.Max(4, bounds.Height - 22));
        }
    }
}

internal sealed class StudioCard : Panel
{
    public int Radius { get; set; } = 18;
    public bool Outline { get; set; } = true;

    public StudioCard()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Surface;
        Padding = new Padding(14);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        if (r.Width <= 0 || r.Height <= 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Rounded(r, Radius);
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);
        if (Outline)
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawPath(pen, path);
        }
    }
}

internal sealed class StudioProgress : Control
{
    private int _value;
    public int Maximum { get; } = 1000;
    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, Maximum); Invalidate(); }
    }

    public StudioProgress()
    {
        DoubleBuffered = true;
        Height = 6;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Surface);
        using var background = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 4);
        using var shade = new SolidBrush(Theme.SurfaceRaised);
        e.Graphics.FillPath(shade, background);
        if (Value <= 0) return;
        var width = Math.Max(4, (int)(Width * (double)Value / Maximum));
        using var fill = Theme.Rounded(new Rectangle(0, 0, Math.Min(width, Width), Height - 1), 4);
        using var brush = new SolidBrush(Theme.AccentFill);
        e.Graphics.FillPath(brush, fill);
    }
}

/// <summary>Line numbers do not modify the editor text or its undo stack.</summary>
internal sealed class EditorGutter : Control
{
    public RichTextBox? Editor { get; set; }

    public EditorGutter()
    {
        DoubleBuffered = true;
        Width = 51;
        Dock = DockStyle.Left;
        Font = new Font("Cascadia Code", 9F);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Input);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawLine(pen, Width - 1, 9, Width - 1, Height - 9);
        if (Editor is null || !Editor.IsHandleCreated) return;

        var editor = Editor;
        var visibleStart = Math.Max(0, editor.GetLineFromCharIndex(editor.GetCharIndexFromPosition(new Point(0, 0))));
        var activeLine = editor.GetLineFromCharIndex(editor.SelectionStart);
        var lines = editor.Lines.Length;
        for (var i = visibleStart; i < lines && i < visibleStart + 250; i++)
        {
            var charPos = editor.GetFirstCharIndexFromLine(i);
            if (charPos < 0) continue;
            var location = editor.GetPositionFromCharIndex(charPos);
            if (location.Y > Height + editor.Font.Height) break;
            if (location.Y < -editor.Font.Height) continue;
            var bounds = new Rectangle(0, location.Y + 1, Width - 13, editor.Font.Height + 5);
            TextRenderer.DrawText(e.Graphics, (i + 1).ToString(), Font, bounds,
                i == activeLine ? Theme.Accent : Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }
}
