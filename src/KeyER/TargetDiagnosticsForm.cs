namespace KeyER;

/// <summary>
/// User-initiated compatibility diagnosis, with no window title or document data.
/// Read-only report: a Ready result is not a guarantee of SendInput acceptance.
/// </summary>
internal sealed class TargetDiagnosticsForm : Form
{
    private readonly Icon _ownedIcon;

    internal TargetDiagnosticsForm(TargetHealthReport report)
    {
        _ownedIcon = BrandIcon.Create();
        Icon = _ownedIcon;
        Text = "KeyER Studio — Проверка совместимости";
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        Font = Theme.UiFont(9.5F);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(540, 369);
        MinimumSize = Size;

        var card = new StudioCard { Dock = DockStyle.Fill, Padding = new Padding(24) };
        var header = Theme.Label("Проверка целевого окна", 18F, true);
        header.AutoSize = false; header.Dock = DockStyle.Top; header.Height = 45;
        var caption = Theme.Label("Диагностика без чтения текста и заголовков окон", 8.8F);
        caption.AutoSize = false; caption.Dock = DockStyle.Top; caption.Height = 32;
        caption.ForeColor = Theme.Muted;

        var statusPanel = new Panel
        {
            Dock = DockStyle.Top, Height = 82, BackColor = Theme.SurfaceRaised,
            Padding = new Padding(14, 10, 14, 7), Tag = "raised"
        };
        var status = Theme.Label(report.CanType ? "✓  Проверка пройдена" : "!  Требуется действие",
            11F, true);
        status.ForeColor = report.CanType ? Theme.Success : Theme.Warning;
        status.Top = 11; status.Left = 12;
        var process = Theme.Label($"Приложение: {report.ProcessLabel}", 8.8F);
        process.ForeColor = Theme.Muted;
        process.Top = 43; process.Left = 13;
        statusPanel.Controls.AddRange([status, process]);

        var summary = Theme.Label(report.Summary, 10.5F, true);
        summary.Dock = DockStyle.Top; summary.AutoSize = false;
        summary.Height = 53; summary.Padding = new Padding(3, 16, 0, 0);
        var guidance = Theme.Label(report.Guidance, 9.3F);
        guidance.Dock = DockStyle.Fill; guidance.AutoSize = false;
        guidance.ForeColor = Theme.Muted;
        guidance.Padding = new Padding(3, 8, 2, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 57,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            BackColor = Theme.Surface
        };
        var close = new StudioButton
        {
            Text = "Понятно", Width = 125, Height = 41, Tone = ButtonTone.Primary,
            DialogResult = DialogResult.OK
        };
        var copy = new StudioButton
        {
            Text = "Копировать отчёт", Width = 176, Height = 41, Tone = ButtonTone.Soft
        };
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText($"KeyER — проверка целевого окна\n" +
                    $"{report.Code}: {report.Summary}\n" +
                    $"Приложение: {report.ProcessLabel}\n" +
                    $"Рекомендация: {report.Guidance}");
                copy.Text = "Скопировано";
            }
            catch
            {
                copy.Text = "Буфер недоступен";
            }
        };
        actions.Controls.Add(close);
        actions.Controls.Add(copy);
        card.Controls.Add(guidance);
        card.Controls.Add(summary);
        card.Controls.Add(statusPanel);
        card.Controls.Add(caption);
        card.Controls.Add(header);
        card.Controls.Add(actions);
        Controls.Add(card);
        AcceptButton = close; CancelButton = close;
        Shown += (_, _) => Theme.ApplyWindowFrame(this);
        FormClosed += (_, _) => _ownedIcon.Dispose();
    }
}
