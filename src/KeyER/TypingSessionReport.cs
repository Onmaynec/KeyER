namespace KeyER;

/// <summary>
/// Ephemeral summary of the last attempt. Never stores typed content,
/// document names, window titles, or file paths.
/// </summary>
internal sealed record TypingSessionReport(
    TypingOutcome Outcome, int Processed, int Total, TimeSpan Duration, string Application)
{
    public static TypingSessionReport Create(TypingOutcome outcome, int processed,
        int total, TimeSpan duration, string? application)
    {
        var safeTotal = Math.Max(0, total);
        var safeProcessed = Math.Clamp(processed, 0, safeTotal);
        var safeDuration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        // The process name has already been sanitized by TargetCompatibility.
        var safeApplication = string.IsNullOrWhiteSpace(application) ? "Неизвестно"
            : new string(application.Where(c => char.IsLetterOrDigit(c) ||
                c is '_' or '-' or '.').Take(48).ToArray());
        return new TypingSessionReport(outcome, safeProcessed, safeTotal,
            safeDuration, safeApplication);
    }

    public string Title => Outcome switch
    {
        TypingOutcome.Completed => "Набор завершён",
        TypingOutcome.Cancelled => "Набор остановлен",
        TypingOutcome.FocusLost => "Целевое окно изменилось",
        TypingOutcome.Interrupted => "Ввод прерван клавиатурой",
        TypingOutcome.Failed => "Ошибка набора",
        _ => "Сеанс не завершён"
    };

    public string Guidance => Outcome switch
    {
        TypingOutcome.Completed =>
            "Клавиши отправлены. Если требуется точность, сравните результат в редакторе. " +
            "Без интеграции IDE KeyER не читает итоговый текст чужого приложения.",
        TypingOutcome.Cancelled =>
            "Остановлен по команде Stop. В целевом поле может остаться часть текста. " +
            "Не запускайте исходник повторно без проверки уже введённого.",
        TypingOutcome.FocusLost =>
            "Фокус или экземпляр целевого окна изменился. KeyER отключил дальнейший ввод. " +
            "Проверьте, какая часть текста осталась, затем выберите нужное окно снова.",
        TypingOutcome.Interrupted =>
            "Обнаружено непредвиденное удержание Ctrl/Alt/Shift/Win. " +
            "Отпустите модификаторы и проверьте введённый фрагмент перед новым запуском.",
        TypingOutcome.Failed =>
            "Windows или редактор отклонили ввод. Проверьте права процесса, фокус " +
            "и совместимость окна. Не повторяйте весь текст поверх частичного результата.",
        _ => "Для нового запуска выберите целевое окно и установите курсор."
    };

    public string ElapsedText => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours} ч {Duration.Minutes} мин {Duration.Seconds} с"
        : Duration.TotalMinutes >= 1
            ? $"{(int)Duration.TotalMinutes} мин {Duration.Seconds} с"
            : $"{Duration.TotalSeconds:0.0} с";

    public string ClipboardText =>
        $"KeyER — отчёт последнего запуска\n" +
        $"Итог: {Title}\nПриложение: {Application}\n" +
        $"Обработано: {Processed} из {Total}\nДлительность: {ElapsedText}\n" +
        $"Рекомендация: {Guidance}";
}

internal sealed class SessionReportForm : Form
{
    private readonly Icon _ownedIcon;

    internal SessionReportForm(TypingSessionReport report)
    {
        _ownedIcon = BrandIcon.Create();
        Icon = _ownedIcon;
        Text = "KeyER Studio — Последний запуск";
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        Font = Theme.UiFont(9.5F);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(550, 380);

        var card = new StudioCard { Dock = DockStyle.Fill, Padding = new Padding(23) };
        var headline = Theme.Label(report.Title, 17.5F, true);
        headline.Dock = DockStyle.Top; headline.AutoSize = false; headline.Height = 45;
        headline.ForeColor = report.Outcome == TypingOutcome.Completed ? Theme.Success :
            Theme.Warning;
        var meta = Theme.Label(
            $"Приложение: {report.Application}\n" +
            $"Обработано: {report.Processed:N0} из {report.Total:N0} символов\n" +
            $"Длительность: {report.ElapsedText}", 9.8F);
        meta.Dock = DockStyle.Top; meta.AutoSize = false; meta.Height = 99;
        meta.Padding = new Padding(4, 10, 0, 4);

        var description = Theme.Label(report.Guidance, 9.5F);
        description.Dock = DockStyle.Fill; description.AutoSize = false;
        description.ForeColor = Theme.Muted;
        description.Padding = new Padding(4, 17, 3, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 58,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            BackColor = Theme.Surface
        };
        var close = new StudioButton
        {
            Text = "Понятно", Tone = ButtonTone.Primary,
            Width = 130, Height = 41, DialogResult = DialogResult.OK
        };
        var copy = new StudioButton
        {
            Text = "Копировать отчёт", Tone = ButtonTone.Soft,
            Width = 176, Height = 41
        };
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(report.ClipboardText); copy.Text = "Скопировано"; }
            catch { copy.Text = "Буфер недоступен"; }
        };
        actions.Controls.Add(close);
        actions.Controls.Add(copy);

        card.Controls.Add(description);
        card.Controls.Add(meta);
        card.Controls.Add(headline);
        card.Controls.Add(actions);
        Controls.Add(card);
        AcceptButton = close; CancelButton = close;
        Shown += (_, _) => Theme.ApplyWindowFrame(this);
        FormClosed += (_, _) => _ownedIcon.Dispose();
    }
}
