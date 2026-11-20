using System.Drawing;

namespace KeyER;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _working;
    private readonly ComboBox _profileSelect;
    private readonly FlowLayoutPanel _profileToolbar;
    private readonly Panel _fieldsHost;
    private readonly TextBox _newProfileName;
    private TypingProfile _selected;
    private bool _changingProfile;

    public AppSettings Result => _working;

    public SettingsForm(AppSettings source)
    {
        _working = source.Clone();
        _working.Validate();
        _selected = _working.GetProfile();

        Text = "KeyER — расширенные настройки";
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        Font = new Font("Segoe UI", 9.3F);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 560);
        Size = new Size(790, 710);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 8) };
        var profilesTab = new TabPage("Профили и поведение") { BackColor = Theme.Background, ForeColor = Theme.Foreground, Padding = new Padding(13) };
        var hotkeysTab = new TabPage("Горячие клавиши") { BackColor = Theme.Background, ForeColor = Theme.Foreground, Padding = new Padding(13) };
        tabs.TabPages.AddRange([profilesTab, hotkeysTab]);

        _profileToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 88, AutoScroll = true,
            WrapContents = true, Padding = new Padding(0, 0, 0, 5), BackColor = Theme.Background
        };
        _profileSelect = new ComboBox { Width = 220, Height = 32 };
        Theme.StyleCombo(_profileSelect);
        _profileSelect.SelectedIndexChanged += (_, _) =>
        {
            if (_changingProfile || _profileSelect.SelectedItem is not string name) return;
            _working.SelectedProfile = name;
            _selected = _working.GetProfile();
            RebuildFields();
        };
        _newProfileName = new TextBox
        {
            Width = 166, Height = 32, BackColor = Theme.Surface, ForeColor = Theme.Foreground,
            BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Название профиля"
        };
        var create = Button("Сохранить как", 125);
        var delete = Button("Удалить", 83);
        create.Click += (_, _) => CreateProfile();
        delete.Click += (_, _) => DeleteProfile();
        _profileToolbar.Controls.AddRange([_profileSelect, _newProfileName, create, delete]);

        _fieldsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Background };
        var profilesCaption = Theme.Label("Параметры текущего профиля сохраняются сразу в рабочую копию.", 8.5f);
        profilesCaption.ForeColor = Theme.Muted;
        profilesCaption.Dock = DockStyle.Top;
        profilesCaption.Height = 25;

        profilesTab.Controls.Add(_fieldsHost);
        profilesTab.Controls.Add(profilesCaption);
        profilesTab.Controls.Add(_profileToolbar);
        ReloadProfileList();

        var keysPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Padding = new Padding(10, 18, 10, 4),
            RowCount = 4, ColumnCount = 2, AutoSize = true,
            BackColor = Theme.Background
        };
        keysPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        keysPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddHotkey(keysPanel, 0, "Начать набор", () => _working.StartHotkey, v => _working.StartHotkey = v);
        AddHotkey(keysPanel, 1, "Пауза / продолжение", () => _working.PauseHotkey, v => _working.PauseHotkey = v);
        AddHotkey(keysPanel, 2, "Остановить", () => _working.StopHotkey, v => _working.StopHotkey = v);
        var note = Theme.Label("Нажмите на поле и затем на нужную комбинацию.\n\n" +
            "Функциональные клавиши F1–F24 разрешены сами по себе.\n" +
            "Для букв и цифр используйте хотя бы Ctrl или Alt.\n" +
            "Если комбинация уже занята Windows или другой программой,\nKeyER сообщит об этом при сохранении.", 9F);
        note.ForeColor = Theme.Muted;
        note.Padding = new Padding(10, 20, 0, 0);
        note.AutoSize = true;
        hotkeysTab.Controls.Add(note);
        hotkeysTab.Controls.Add(keysPanel);
        note.Dock = DockStyle.Top;

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 67, Padding = new Padding(12), BackColor = Theme.Surface };
        var save = Button("Сохранить", 124, primary: true);
        var cancel = Button("Отмена", 108);
        save.Dock = DockStyle.Right;
        cancel.Dock = DockStyle.Right;
        save.Click += (_, _) =>
        {
            if (_working.StartHotkey == _working.PauseHotkey ||
                _working.StartHotkey == _working.StopHotkey ||
                _working.PauseHotkey == _working.StopHotkey)
            {
                MessageBox.Show(this, "Старт, пауза и остановка должны иметь разные комбинации.",
                    "KeyER", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _working.Validate();
            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        bottom.Controls.AddRange([save, cancel]);
        Controls.Add(tabs);
        Controls.Add(bottom);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static Button Button(string title, int width, bool primary = false)
    {
        var b = new Button { Text = title, Width = width, Height = 33 };
        Theme.StyleButton(b, primary);
        return b;
    }

    private void ReloadProfileList()
    {
        _changingProfile = true;
        _profileSelect.Items.Clear();
        foreach (var p in _working.Profiles) _profileSelect.Items.Add(p.Name);
        _profileSelect.SelectedItem = _working.SelectedProfile;
        if (_profileSelect.SelectedIndex < 0) _profileSelect.SelectedIndex = 0;
        _selected = _working.GetProfile();
        _changingProfile = false;
        RebuildFields();
    }

    private void CreateProfile()
    {
        var name = _newProfileName.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Введите название профиля.", "KeyER", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (name.Length > 60 || _working.Profiles.Any(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "Название слишком длинное или уже занято.", "KeyER", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var profile = _selected.Clone();
        profile.Name = name;
        _working.Profiles.Add(profile);
        _working.SelectedProfile = name;
        _newProfileName.Clear();
        ReloadProfileList();
    }

    private void DeleteProfile()
    {
        if (_working.Profiles.Count <= 1)
        {
            MessageBox.Show(this, "Нельзя удалить последний профиль.", "KeyER", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _working.Profiles.Remove(_selected);
        _working.SelectedProfile = _working.Profiles[0].Name;
        ReloadProfileList();
    }

    private void RebuildFields()
    {
        _fieldsHost.SuspendLayout();
        _fieldsHost.Controls.Clear();
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            BackColor = Theme.Background, Padding = new Padding(4, 10, 15, 15)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));

        var p = _selected;
        Number(table, "Скорость набора (символов в минуту)", 20, 1200, 1, p.CharactersPerMinute, v => p.CharactersPerMinute = (int)v);
        Number(table, "Колебания скорости (%)", 0, 95, 1, p.SpeedVariationPercent, v => p.SpeedVariationPercent = (int)v);
        Number(table, "Вероятность опечатки (%)", 0, 30, 0.1m, p.TypoPercent, v => p.TypoPercent = v, 1);
        Number(table, "Вероятность позднего исправления (%)", 0, 100, 1, p.LateCorrectionPercent, v => p.LateCorrectionPercent = (int)v);
        Number(table, "Максимум символов до исправления", 0, 12, 1, p.CorrectionLagCharacters, v => p.CorrectionLagCharacters = (int)v);
        Number(table, "Вероятность длинной паузы (%)", 0, 20, 0.1m, p.LongPausePercent, v => p.LongPausePercent = v, 1);
        Number(table, "Минимальная длинная пауза (мс)", 0, 10000, 25, p.PauseMinMilliseconds, v => p.PauseMinMilliseconds = (int)v);
        Number(table, "Максимальная длинная пауза (мс)", 0, 20000, 25, p.PauseMaxMilliseconds, v => p.PauseMaxMilliseconds = (int)v);
        Number(table, "Усталость к концу большого текста (%)", 0, 100, 1, p.FatiguePercent, v => p.FatiguePercent = (int)v);
        Number(table, "Вероятность ускоренных серий (%)", 0, 80, 1, p.BurstPercent, v => p.BurstPercent = (int)v);
        Number(table, "Вероятность проверки строки курсором (%)", 0, 20, 1, p.CursorReviewPercent, v => p.CursorReviewPercent = (int)v);
        Number(table, "Отсчёт при старте с кнопки (сек)", 1, 10, 1, p.StartCountdownSeconds, v => p.StartCountdownSeconds = (int)v);

        var textHeading = Theme.Label("TEXT ENGINE 2.0 — естественный набор", 11F, true);
        textHeading.ForeColor = Theme.Accent;
        textHeading.Margin = new Padding(4, 22, 4, 12);
        var textHeadingRow = table.RowCount++;
        table.Controls.Add(textHeading, 0, textHeadingRow);
        table.SetColumnSpan(textHeading, 2);

        Number(table, "Скорость коротких слов (% от базовой)", 40, 250, 5, p.ShortWordSpeedPercent, v => p.ShortWordSpeedPercent = (int)v);
        Number(table, "Скорость длинных слов (% от базовой)", 40, 250, 5, p.LongWordSpeedPercent, v => p.LongWordSpeedPercent = (int)v);
        Number(table, "Замедление на цифрах (%)", 100, 350, 5, p.DigitSlowdownPercent, v => p.DigitSlowdownPercent = (int)v);
        Number(table, "Замедление на пунктуации (%)", 100, 350, 5, p.PunctuationSlowdownPercent, v => p.PunctuationSlowdownPercent = (int)v);
        Number(table, "Замедление специальных символов (%)", 100, 350, 5, p.SpecialSlowdownPercent, v => p.SpecialSlowdownPercent = (int)v);
        Number(table, "Ускорение частых сочетаний букв (%)", 0, 65, 1, p.FamiliarPairBoostPercent, v => p.FamiliarPairBoostPercent = (int)v);
        Number(table, "Сглаживание скорости (% реакции)", 1, 100, 1, p.SpeedSmoothingPercent, v => p.SpeedSmoothingPercent = (int)v);
        Number(table, "Пауза после предложения, мин. (мс)", 0, 10000, 25, p.SentencePauseMinMilliseconds, v => p.SentencePauseMinMilliseconds = (int)v);
        Number(table, "Пауза после предложения, макс. (мс)", 0, 15000, 25, p.SentencePauseMaxMilliseconds, v => p.SentencePauseMaxMilliseconds = (int)v);
        Number(table, "Пауза после абзаца, мин. (мс)", 0, 15000, 25, p.ParagraphPauseMinMilliseconds, v => p.ParagraphPauseMinMilliseconds = (int)v);
        Number(table, "Пауза после абзаца, макс. (мс)", 0, 20000, 25, p.ParagraphPauseMaxMilliseconds, v => p.ParagraphPauseMaxMilliseconds = (int)v);
        Number(table, "Перерыв между блоками, мин. (мс)", 0, 30000, 25, p.BlockPauseMinMilliseconds, v => p.BlockPauseMinMilliseconds = (int)v);
        Number(table, "Перерыв между блоками, макс. (мс)", 0, 60000, 50, p.BlockPauseMaxMilliseconds, v => p.BlockPauseMaxMilliseconds = (int)v);
        Number(table, "Поздняя коррекция: максимум слов", 1, 6, 1, p.LateCorrectionMaxWords, v => p.LateCorrectionMaxWords = (int)v);
        Number(table, "Поздняя коррекция: максимум символов", 20, 250, 5, p.LateCorrectionMaxDistance, v => p.LateCorrectionMaxDistance = (int)v);
        Number(table, "Исправление клавишей Delete (%)", 0, 100, 1, p.DeleteCorrectionPercent, v => p.DeleteCorrectionPercent = (int)v);
        Number(table, "Исправление выделением (%)", 0, 100, 1, p.SelectionCorrectionPercent, v => p.SelectionCorrectionPercent = (int)v);
        Number(table, "Количество пробелов вместо Tab", 1, 8, 1, p.TabSpaces, v => p.TabSpaces = (int)v);

        var enterRow = table.RowCount++;
        table.Controls.Add(Theme.Label("Новая строка в Text", 9.1F), 0, enterRow);
        var enterCombo = new ComboBox { Width = 190 };
        Theme.StyleCombo(enterCombo);
        enterCombo.Items.AddRange(["Enter", "Shift+Enter (для чатов)"]);
        enterCombo.SelectedIndex = (int)p.TextEnter;
        enterCombo.SelectedIndexChanged += (_, _) => p.TextEnter = (TextEnterMode)enterCombo.SelectedIndex;
        table.Controls.Add(enterCombo, 1, enterRow);

        var tabRow = table.RowCount++;
        table.Controls.Add(Theme.Label("Табуляция в Text", 9.1F), 0, tabRow);
        var tabCombo = new ComboBox { Width = 190 };
        Theme.StyleCombo(tabCombo);
        tabCombo.Items.AddRange(["Нажатие Tab", "Пробелы вместо Tab"]);
        tabCombo.SelectedIndex = (int)p.TextTab;
        tabCombo.SelectedIndexChanged += (_, _) => p.TextTab = (TextTabMode)tabCombo.SelectedIndex;
        table.Controls.Add(tabCombo, 1, tabRow);

        var textNoteRow = table.RowCount++;
        var textNote = Theme.Label("Для чатов выбирайте Shift+Enter: обычный Enter может отправить сообщение.\\n" +
            "Паузы и коррекции относятся только к Text; точность зависит от текстового поля.", 8.3F);
        textNote.ForeColor = Theme.Muted;
        textNote.Margin = new Padding(4, 14, 4, 12);
        table.Controls.Add(textNote, 0, textNoteRow);
        table.SetColumnSpan(textNote, 2);

        var codeTitle = Theme.Label("CODE INTELLIGENCE 0.3 — синтаксический ритм", 11F, true);
        codeTitle.ForeColor = Theme.Accent;
        codeTitle.Margin = new Padding(4, 22, 4, 12);
        var codeTitleRow = table.RowCount++;
        table.Controls.Add(codeTitle, 0, codeTitleRow);
        table.SetColumnSpan(codeTitle, 2);

        Number(table, "Code: пауза между строками (мс)", 0, 5000, 25, p.CodeLinePauseMilliseconds,
            v => p.CodeLinePauseMilliseconds = (int)v);
        Number(table, "Code: пауза перед классом/функцией (мс)", 0, 10000, 25, p.CodeFunctionPauseMilliseconds,
            v => p.CodeFunctionPauseMilliseconds = (int)v);
        Number(table, "Code: пауза перед блоком/ветвлением (мс)", 0, 10000, 25, p.CodeBlockPauseMilliseconds,
            v => p.CodeBlockPauseMilliseconds = (int)v);
        Number(table, "Code: пауза на синтаксисе (мс)", 0, 5000, 25, p.CodeSyntaxPauseMilliseconds,
            v => p.CodeSyntaxPauseMilliseconds = (int)v);
        Number(table, "Code: скорость идентификаторов (%)", 40, 250, 5, p.CodeIdentifierSpeedPercent,
            v => p.CodeIdentifierSpeedPercent = (int)v);
        Number(table, "Code: скорость строковых литералов (%)", 40, 250, 5, p.CodeStringSpeedPercent,
            v => p.CodeStringSpeedPercent = (int)v);
        Number(table, "Code: скорость комментариев (%)", 40, 250, 5, p.CodeCommentSpeedPercent,
            v => p.CodeCommentSpeedPercent = (int)v);
        Number(table, "Code: повторное выделение окончания имени (%)", 0, 30, 1, p.CodeReviewPercent,
            v => p.CodeReviewPercent = (int)v);

        var codeInfo = Theme.Label(
            "Анализ кода лексический: не полноценный AST компилятора. " +
            "IDE может автоматически вставлять отступы и скобки.\n" +
            "Автоматическая сверка результата появится с интеграциями IDE.", 8.3F);
        codeInfo.ForeColor = Theme.Muted;
        codeInfo.Margin = new Padding(4, 14, 4, 12);
        var codeInfoRow = table.RowCount++;
        table.Controls.Add(codeInfo, 0, codeInfoRow);
        table.SetColumnSpan(codeInfo, 2);

        var editorName = Theme.Label("Профиль редактора для режима Code", 9.2F);
        editorName.Margin = new Padding(4, 12, 4, 10);
        var editorRow = table.RowCount;
        table.RowCount = editorRow + 1;
        table.Controls.Add(editorName, 0, editorRow);
        var editorCombo = new ComboBox { Width = 190 };
        Theme.StyleCombo(editorCombo);
        editorCombo.Items.AddRange(["Универсальный", "Visual Studio", "VS Code"]);
        editorCombo.SelectedIndex = (int)p.Editor;
        editorCombo.SelectedIndexChanged += (_, _) => p.Editor = (EditorType)editorCombo.SelectedIndex;
        table.Controls.Add(editorCombo, 1, editorRow);

        var indentCheck = new CheckBox
        {
            Text = "Code: выделять и замещать автоотступ/автоскобку на новой строке (экспериментально)",
            Checked = p.ClearAutoIndent, AutoSize = true,
            ForeColor = Theme.Foreground, BackColor = Theme.Background,
            Margin = new Padding(4, 15, 4, 5)
        };
        indentCheck.CheckedChanged += (_, _) => p.ClearAutoIndent = indentCheck.Checked;
        var row = table.RowCount;
        table.RowCount = row + 1;
        table.Controls.Add(indentCheck, 0, row);
        table.SetColumnSpan(indentCheck, 2);

        var warning = Theme.Label(
            "Внешние IDE могут менять ввод. В режиме Code настройки форматирования\nи автодополнения целевого редактора по-прежнему влияют на результат.", 8.3F);
        warning.ForeColor = Theme.Muted;
        warning.Padding = new Padding(4, 13, 0, 6);
        row = table.RowCount;
        table.RowCount = row + 1;
        table.Controls.Add(warning, 0, row);
        table.SetColumnSpan(warning, 2);
        _fieldsHost.Controls.Add(table);
        _fieldsHost.ResumeLayout();
    }

    private static void Number(TableLayoutPanel table, string label, decimal min,
        decimal max, decimal step, decimal value, Action<decimal> changed, int decimals = 0)
    {
        var row = table.RowCount;
        table.RowCount = row + 1;
        var title = Theme.Label(label, 9.1F);
        title.Margin = new Padding(4, 9, 4, 9);
        var box = new NumericUpDown
        {
            Minimum = min, Maximum = max, Increment = step,
            DecimalPlaces = decimals, Value = Math.Clamp(value, min, max),
            Width = 148, Height = 28,
            BackColor = Theme.Surface, ForeColor = Theme.Foreground,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3, 6, 4, 6)
        };
        box.ValueChanged += (_, _) => changed(box.Value);
        table.Controls.Add(title, 0, row);
        table.Controls.Add(box, 1, row);
    }

    private static void AddHotkey(TableLayoutPanel table, int row, string caption,
        Func<Keys> get, Action<Keys> set)
    {
        var label = Theme.Label(caption, 10F);
        label.Margin = new Padding(6, 12, 6, 12);
        var box = new TextBox
        {
            Width = 245, Height = 34, ReadOnly = true, TabStop = true,
            Text = Native.HotkeyText(get()),
            BackColor = Theme.Surface, ForeColor = Theme.Foreground,
            BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3, 8, 0, 8)
        };
        box.KeyDown += (_, e) =>
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            var key = e.KeyCode;
            if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
                return;
            if (key is Keys.Escape or Keys.Tab) return;
            var mods = e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift);
            if (mods == Keys.None && (key < Keys.F1 || key > Keys.F24)) return;
            var candidate = mods | key;
            set(candidate);
            box.Text = Native.HotkeyText(candidate);
        };
        table.Controls.Add(label, 0, row);
        table.Controls.Add(box, 1, row);
    }
}
