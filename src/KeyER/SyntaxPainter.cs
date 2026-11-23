using System.Drawing;
using System.Runtime.InteropServices;

namespace KeyER;

/// <summary>
/// Optional syntax tinting for the input editor. Token recognition is lexical
/// and never modifies the textual content. Large documents skip rich formatting.
/// </summary>
internal sealed class SyntaxPainter : IDisposable
{
    private const int WmSetRedraw = 0x000B;
    private const int EmGetScrollPos = 0x04DD;
    private const int EmSetScrollPos = 0x04DE;
    private const int MaxHighlightCharacters = 90_000;
    // RichEdit: WM_USER+120/121, IMF_SPELLCHECKING. Disable native red
    // proofing squiggles in the code editor without disabling normal IME.
    private const int EmSetLangOptions = 0x0478;
    private const int EmGetLangOptions = 0x0479;
    private const int ImfSpellChecking = 0x0800;
    private readonly RichTextBox _editor;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _disposed;
    private bool _painting;
    private string? _paintedText;
    private string? _paintedAppearance;
    private IntPtr _paintedHandle;
    private IntPtr _proofingHandle;
    internal int CompletedPaints { get; private set; }
    internal int SuppressedSchedules { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative { public int X; public int Y; }
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wp, ref PointNative point);

    public bool Enabled { get; set; } = true;
    public bool IsCode { get; set; }
    public string FileName { get; set; } = "";
    public string Language { get; set; } = "Auto";
    public bool IsPainting => _painting;

    public SyntaxPainter(RichTextBox editor)
    {
        _editor = editor;
        _timer = new System.Windows.Forms.Timer { Interval = 290 };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Render();
        };
        _editor.HandleCreated += OnEditorHandleCreated;
    }

    private void OnEditorHandleCreated(object? sender, EventArgs e)
    {
        _paintedHandle = IntPtr.Zero;
        DisableNativeProofing();
    }

    internal void DisableNativeProofing()
    {
        if (_disposed || !_editor.IsHandleCreated ||
            _proofingHandle == _editor.Handle) return;
        var options = SendMessage(_editor.Handle, EmGetLangOptions,
            IntPtr.Zero, IntPtr.Zero).ToInt64();
        _ = SendMessage(_editor.Handle, EmSetLangOptions, IntPtr.Zero,
            new IntPtr(options & ~ImfSpellChecking));
        _proofingHandle = _editor.Handle;
    }

    private string AppearanceKey() =>
        $"{Enabled}|{IsCode}|{FileName}|{Language}|{Theme.Foreground.ToArgb()}|{Theme.IsLight}";

    private bool NeedsPaint() =>
        _paintedHandle != _editor.Handle ||
        _paintedAppearance != AppearanceKey() ||
        !string.Equals(_paintedText, _editor.Text, StringComparison.Ordinal);

    public void Schedule()
    {
        if (_disposed || _painting || !_editor.IsHandleCreated) return;
        DisableNativeProofing();
        if (!NeedsPaint())
        {
            _timer.Stop();
            SuppressedSchedules++;
            return;
        }
        _timer.Stop();
        _timer.Start();
    }

    public void Render()
    {
        if (_disposed || _painting || !_editor.IsHandleCreated) return;
        _timer.Stop();
        DisableNativeProofing();
        if (!NeedsPaint()) { SuppressedSchedules++; return; }
        _painting = true;
        try
        {
            var source = _editor.Text;
            var appearance = AppearanceKey();
            var oldStart = _editor.SelectionStart;
            var oldLength = _editor.SelectionLength;
            var pos = new PointNative();
            _ = SendMessage(_editor.Handle, EmGetScrollPos, IntPtr.Zero, ref pos);
            var handle = _editor.Handle;
            _ = SendMessage(handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
            try
            {
                _editor.SelectAll();
                _editor.SelectionColor = Theme.Foreground;
                if (Enabled && IsCode && source.Length <= MaxHighlightCharacters)
                {

                var language = Enum.TryParse<CodeAnalyzer.Language>(Language,
                    out var selected) ? selected : CodeAnalyzer.Language.Auto;
                var plan = CodeAnalyzer.Analyze(source, FileName, language);
                foreach (var line in plan.Lines)
                foreach (var token in line.Tokens)
                {
                    var color = TokenColor(token.Kind);
                    if (color == Theme.Foreground || token.Text.Length == 0) continue;
                    var start = line.Offset + token.Offset;
                    if (start + token.Text.Length > source.Length) continue;
                    _editor.Select(start, token.Text.Length);
                    _editor.SelectionColor = color;
                }
                }
                // Only actual content/style changes perform a formatting pass.
                // Selection changes, polling and stats updates are not edits.
                _paintedText = source;
                _paintedAppearance = appearance;
                _paintedHandle = handle;
                CompletedPaints++;
            }
            finally
            {
                // Selecting tokens in RichEdit can move BOTH scroll axes, even
                // while redraw is suppressed. Restore the native pixel position
                // after restoring the selection, and then once redraw resumes.
                var safeStart = Math.Min(oldStart, _editor.TextLength);
                _editor.Select(safeStart,
                    Math.Min(oldLength, Math.Max(0, _editor.TextLength - safeStart)));
                _ = SendMessage(handle, EmSetScrollPos, IntPtr.Zero, ref pos);
                _ = SendMessage(handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                _ = SendMessage(handle, EmSetScrollPos, IntPtr.Zero, ref pos);
                _editor.Invalidate();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("syntax-highlight", ex);
        }
        finally { _painting = false; }
    }

    private static Color TokenColor(CodeAnalyzer.TokenKind kind) => kind switch
    {
        CodeAnalyzer.TokenKind.Keyword => Theme.IsLight ? Color.FromArgb(111, 74, 199) : Color.FromArgb(189, 148, 251),
        CodeAnalyzer.TokenKind.String => Theme.IsLight ? Color.FromArgb(17, 137, 105) : Color.FromArgb(122, 218, 172),
        CodeAnalyzer.TokenKind.Comment => Theme.IsLight ? Color.FromArgb(128, 138, 150) : Color.FromArgb(120, 138, 159),
        CodeAnalyzer.TokenKind.Number => Theme.IsLight ? Color.FromArgb(190, 93, 35) : Color.FromArgb(248, 178, 110),
        CodeAnalyzer.TokenKind.Operator => Theme.IsLight ? Color.FromArgb(42, 108, 193) : Color.FromArgb(121, 181, 255),
        CodeAnalyzer.TokenKind.Markup => Theme.IsLight ? Color.FromArgb(49, 104, 173) : Color.FromArgb(128, 184, 238),
        _ => Theme.Foreground
    };

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _editor.HandleCreated -= OnEditorHandleCreated;
        _timer.Dispose();
    }
}
