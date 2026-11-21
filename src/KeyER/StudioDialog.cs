namespace KeyER;

/// <summary>
/// Theme-consistent, keyboard accessible dialogs for destructive actions and help.
/// </summary>
internal sealed class StudioDialog : Form
{
    private readonly Icon _dialogIcon;

    private StudioDialog(string title, string body, bool confirmation, bool dangerous)
    {
        Text = "KeyER Studio — " + title;
        _dialogIcon = BrandIcon.Create();
        Icon = _dialogIcon;
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        Font = Theme.UiFont(9.4F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(495, confirmation ? 260 : 440);
        MinimumSize = Size;

        var card = new StudioCard { Dock = DockStyle.Fill, Padding = new Padding(24) };
        var heading = Theme.Label(title, 18F, true);
        heading.Dock = DockStyle.Top;
        heading.Height = 45;
        heading.AutoSize = false;

        var description = Theme.Label(body, 9.6F);
        description.Dock = DockStyle.Fill;
        description.AutoSize = false;
        description.ForeColor = Theme.Muted;
        description.Padding = new Padding(1, 11, 1, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 59, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Theme.Surface,
            Padding = new Padding(0, 9, 0, 0)
        };
        var primary = new StudioButton
        {
            Text = confirmation ? "Продолжить" : "Понятно",
            Width = 148, Height = 40,
            Tone = dangerous ? ButtonTone.Danger : ButtonTone.Primary,
            DialogResult = DialogResult.OK
        };
        actions.Controls.Add(primary);
        AcceptButton = primary;

        if (confirmation)
        {
            var cancel = new StudioButton
            {
                Text = "Отмена", Width = 116, Height = 40,
                Tone = ButtonTone.Quiet, DialogResult = DialogResult.Cancel
            };
            actions.Controls.Add(cancel);
            CancelButton = cancel;
        }
        else CancelButton = primary;

        card.Controls.Add(description);
        card.Controls.Add(heading);
        card.Controls.Add(actions);
        Controls.Add(card);
        Shown += (_, _) => Theme.ApplyWindowFrame(this);
        FormClosed += (_, _) => _dialogIcon.Dispose();
    }

    public static bool Confirm(IWin32Window owner, string title, string message)
    {
        using var dialog = new StudioDialog(title, message, true, true);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    public static void Guide(IWin32Window owner, string start, string pause, string stop)
    {
        var message =
            "01   ПОДГОТОВЬТЕ ИСХОДНИК\n" +
            "Выберите Text или Code, затем вставьте текст либо откройте файл.\n\n" +
            "02   ВЫБЕРИТЕ ЦЕЛЕВОЕ ПОЛЕ\n" +
            "Откройте Блокнот, браузер или IDE и установите там курсор.\n\n" +
            "03   ЗАПУСТИТЕ НАБОР\n" +
            "Старт: " + start + "\nПауза: " + pause + "\nСтоп: " + stop + "\n\n" +
            "Ctrl+F — поиск в исходнике   ·   Ctrl+Shift+S — сохранить файл.\n" +
            "При потере фокуса целевого окна ввод останавливается.";
        using var dialog = new StudioDialog("Быстрый старт", message, false, false);
        dialog.ShowDialog(owner);
    }
}
