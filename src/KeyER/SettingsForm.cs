using System.Drawing;

namespace KeyER;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _working;
    private readonly Icon _ownedIcon;
    private readonly ComboBox _profileSelect;
    private readonly FlowLayoutPanel _profileToolbar;
    private readonly Panel _fieldsHost;
    private readonly TextBox _newProfileName;
    private TypingProfile _selected;
    private bool _changingProfile;

    public AppSettings Result => _working;

    private readonly Dictionary<string, StudioButton> _navButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Panel> _pages = new(StringComparer.Ordinal);
    private Label _pageHeading = null!;
    private Label _pageDescription = null!;
    private string _profileSection = "Поведение";
    private TextBox _settingsFind = null!;
    private Label _settingsFindStatus = null!;
    private StudioThemePreview _themePreview = null!;

    public SettingsForm(AppSettings source)
    {
        _working = source.Clone();
        _working.Validate();
        _ownedIcon = BrandIcon.Create();
        Icon = _ownedIcon;
        Shown += (_, _) => Theme.ApplyWindowFrame(this);
        FormClosed += (_, _) => _ownedIcon.Dispose();
        _selected = _working.GetProfile();

        Text = "KeyER Studio — Настройки";
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        Font = Theme.UiFont(9.5F);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 600);
        Size = new Size(1030, 780);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty, Padding = Padding.Empty,
            BackColor = Theme.Background
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var sidebar = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(15, 24, 13, 20),
            BackColor = Theme.Surface
        };
        var brandLabel = Theme.Label("KEYER  /  STUDIO", 10F, true);
        brandLabel.ForeColor = Theme.Accent;
        brandLabel.Left = 10; brandLabel.Top = 9;
        var brandCaption = Theme.Label("ПАРАМЕТРЫ", 8F);
        brandCaption.ForeColor = Theme.Muted;
        brandCaption.Left = 10; brandCaption.Top = 35;
        var sideNav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = Theme.Surface, Margin = Padding.Empty
        };
        sidebar.Controls.Add(sideNav);
        var brandPanel = new Panel { Dock = DockStyle.Top, Height = 91, BackColor = Theme.Surface };
        brandPanel.Controls.AddRange([brandLabel, brandCaption]);
        sidebar.Controls.Add(brandPanel);

        foreach (var (name, title) in new[]
        {
            ("Поведение", "◉    Поведение"),
            ("Text", "T    Обычный текст"),
            ("Code", "</>    Режим Code"),
            ("Hotkeys", "⌨    Горячие клавиши"),
            ("Appearance", "◐    Оформление")
        })
        {
            var navButton = new StudioButton
            {
                Text = title, Width = 172, Height = 45,
                Margin = new Padding(0, 3, 0, 3),
                AlignLeft = true,
                Tone = ButtonTone.Quiet
            };
            navButton.Click += (_, _) => ShowCategory(name);
            sideNav.Controls.Add(navButton);
            _navButtons.Add(name, navButton);
        }
        sideNav.SizeChanged += (_, _) =>
        {
            foreach (var button in _navButtons.Values)
                button.Width = Math.Max(145, sideNav.ClientSize.Width - 11);
        };
        shell.Controls.Add(sidebar, 0, 0);

        var contentHost = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Theme.Background,
            Padding = new Padding(26, 14, 26, 9)
        };
        var pageHeader = new Panel { Dock = DockStyle.Top, Height = 101, BackColor = Theme.Background };
        var caption = Theme.Label("НАСТРОЙКИ / KEYER", 8F, true);
        caption.ForeColor = Theme.Accent; caption.Left = 1; caption.Top = 10;
        _pageHeading = Theme.Label("Поведение", 21F, true);
        _pageHeading.Left = 0; _pageHeading.Top = 34;
        _pageDescription = Theme.Label("Управляйте характером печати и привычками.", 8.8F);
        _pageDescription.ForeColor = Theme.Muted; _pageDescription.Left = 1; _pageDescription.Top = 76;
        _settingsFind = new TextBox
        {
            Width = 188, Height = 30, Font = Theme.UiFont(9.5F),
            BackColor = Theme.SurfaceRaised, ForeColor = Theme.Foreground,
            BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "⌕  Поиск параметра",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Top = 37
        };
        _settingsFind.TextChanged += (_, _) => FilterProfileFields();
        _settingsFind.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                _settingsFind.Clear();
                e.SuppressKeyPress = true;
            }
        };
        _settingsFindStatus = Theme.Label("", 8F);
        _settingsFindStatus.ForeColor = Theme.Muted;
        _settingsFindStatus.AutoSize = false;
        _settingsFindStatus.TextAlign = ContentAlignment.MiddleRight;
        _settingsFindStatus.Width = 190;
        _settingsFindStatus.Height = 22;
        _settingsFindStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pageHeader.Controls.AddRange([caption, _pageHeading, _pageDescription,
            _settingsFind, _settingsFindStatus]);
        pageHeader.Resize += (_, _) =>
        {
            _settingsFind.Left = Math.Max(285, pageHeader.ClientSize.Width - 195);
            _settingsFindStatus.Left = Math.Max(285, pageHeader.ClientSize.Width - 196);
            _settingsFindStatus.Top = 70;
            _settingsFind.Visible = pageHeader.ClientSize.Width >= 540 &&
                _profileSection is "Поведение" or "Text" or "Code";
            _settingsFindStatus.Visible = _settingsFind.Visible;
        };

        var paneFrame = new StudioCard
        {
            Dock = DockStyle.Fill, Padding = new Padding(1),
            Radius = 17
        };
        var paneHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        paneFrame.Controls.Add(paneHost);
        contentHost.Controls.Add(paneFrame);
        contentHost.Controls.Add(pageHeader);
        shell.Controls.Add(contentHost, 1, 0);

        var profilePane = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Theme.Surface,
            Padding = new Padding(19, 13, 19, 10)
        };
        _profileToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 74, AutoScroll = true,
            WrapContents = false, Padding = new Padding(0, 7, 0, 11),
            BackColor = Theme.Surface
        };
        _profileSelect = new ComboBox { Width = 215, Height = 35 };
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
            Width = 168, Height = 35, BackColor = Theme.SurfaceRaised, ForeColor = Theme.Foreground,
            BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Новый профиль",
            Font = Theme.UiFont(9.1F)
        };
        var create = Button("Сохранить как", 123);
        var delete = Button("Удалить", 85);
        create.Click += (_, _) => CreateProfile();
        delete.Click += (_, _) => DeleteProfile();
        _profileToolbar.Controls.AddRange([_profileSelect, _newProfileName, create, delete]);
        _fieldsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Surface };
        profilePane.Controls.Add(_fieldsHost);
        profilePane.Controls.Add(_profileToolbar);
        paneHost.Controls.Add(profilePane);
        _pages.Add("profiles", profilePane);

        var keysPane = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            BackColor = Theme.Surface, Padding = new Padding(21, 17, 21, 14)
        };
        var keysPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Padding = new Padding(8, 15, 8, 4),
            RowCount = 3, ColumnCount = 2, AutoSize = true, BackColor = Theme.Surface
        };
        keysPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
        keysPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddHotkey(keysPanel, 0, "Начать набор", () => _working.StartHotkey, v => _working.StartHotkey = v);
        AddHotkey(keysPanel, 1, "Пауза / продолжить", () => _working.PauseHotkey, v => _working.PauseHotkey = v);
        AddHotkey(keysPanel, 2, "Остановить", () => _working.StopHotkey, v => _working.StopHotkey = v);
        var note = Theme.Label(
            "Выделите поле и нажмите новое сочетание.\n\n" +
            "F1–F24 можно использовать без модификаторов. Для букв и цифр\n" +
            "нужен Ctrl, Alt или Shift. Если сочетание занято, KeyER\n" +
            "сообщит об этом при сохранении.", 9F);
        note.ForeColor = Theme.Muted; note.Padding = new Padding(7, 15, 0, 0);
        note.Dock = DockStyle.Top; note.Height = 129; note.AutoSize = false;
        keysPane.Controls.Add(note);
        keysPane.Controls.Add(keysPanel);
        paneHost.Controls.Add(keysPane);
        _pages.Add("Hotkeys", keysPane);

        var appearancePane = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Theme.Surface,
            AutoScroll = true, Padding = new Padding(22, 17, 20, 12)
        };
        var appearanceGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2,
            BackColor = Theme.Surface, Padding = new Padding(0, 0, 0, 15)
        };
        appearanceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        appearanceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        void AddOption(string title, Control control)
        {
            var row = appearanceGrid.RowCount++;
            appearanceGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 57));
            var heading = Theme.Label(title, 9.3F, true);
            heading.Margin = new Padding(6, 17, 8, 10);
            appearanceGrid.Controls.Add(heading, 0, row);
            control.Margin = new Padding(3, 10, 8, 10);
            appearanceGrid.Controls.Add(control, 1, row);
        }

        _themePreview = new StudioThemePreview
        {
            Dock = DockStyle.Top, Height = 196,
            PreviewAppearance = _working.Appearance, PreviewAccent = _working.Accent
        };
        var appearanceSelect = new ComboBox { Width = 194 };
        Theme.StyleCombo(appearanceSelect);
        appearanceSelect.Items.AddRange(["Тёмная", "Светлая", "Как в Windows"]);
        appearanceSelect.SelectedIndex = (int)_working.Appearance;
        appearanceSelect.SelectedIndexChanged += (_, _) =>
        {
            _working.Appearance = (AppearanceMode)appearanceSelect.SelectedIndex;
            _themePreview.PreviewAppearance = _working.Appearance;
        };
        AddOption("Цветовая тема", appearanceSelect);

        var accentSelect = new ComboBox { Width = 194 };
        Theme.StyleCombo(accentSelect);
        accentSelect.Items.AddRange(["Iris · фиолетовый", "Ocean · синий", "Mint · зелёный", "Amber · янтарный"]);
        accentSelect.SelectedIndex = (int)_working.Accent;
        accentSelect.SelectedIndexChanged += (_, _) =>
        {
            _working.Accent = (AccentTheme)accentSelect.SelectedIndex;
            _themePreview.PreviewAccent = _working.Accent;
        };
        AddOption("Акцент интерфейса", accentSelect);

        CheckBox Toggle(string text, bool value, Action<bool> changed)
        {
            var box = new CheckBox
            {
                Text = text, Checked = value, AutoSize = true,
                ForeColor = Theme.Foreground, BackColor = Theme.Surface,
                Font = Theme.UiFont(9F)
            };
            box.CheckedChanged += (_, _) => changed(box.Checked);
            return box;
        }

        AddOption("Компоновка окна", Toggle("Компактный режим", _working.CompactMode, v => _working.CompactMode = v));
        AddOption("Поверх других окон", Toggle("Закрепить окно", _working.AlwaysOnTop, v => _working.AlwaysOnTop = v));
        AddOption("Редактор текста", Toggle("Номера строк", _working.ShowLineNumbers, v => _working.ShowLineNumbers = v));
        AddOption("Подсветка кода", Toggle("Цветной синтаксис", _working.SyntaxHighlighting, v => _working.SyntaxHighlighting = v));
        var fontSize = new NumericUpDown
        {
            Minimum = 9, Maximum = 18, Value = _working.EditorFontSize,
            Width = 86, Height = 32, BackColor = Theme.SurfaceRaised,
            ForeColor = Theme.Foreground, Font = Theme.UiFont(10.5F)
        };
        fontSize.ValueChanged += (_, _) => _working.EditorFontSize = (int)fontSize.Value;
        AddOption("Размер текста в редакторе", fontSize);

        var previewCaption = Theme.Label("ЖИВОЙ ПРЕДПРОСМОТР ТЕМЫ", 8.2F, true);
        previewCaption.Dock = DockStyle.Top;
        previewCaption.Height = 39;
        previewCaption.AutoSize = false;
        previewCaption.Padding = new Padding(5, 13, 0, 0);
        previewCaption.ForeColor = Theme.Accent;
        var previewNote = Theme.Label(
            "Тема и акцент обновляются в макете выше. В приложении они\n" +
            "применятся только после сохранения изменений.", 8.5F);
        previewNote.AutoSize = false;
        previewNote.Dock = DockStyle.Top;
        previewNote.Height = 57;
        previewNote.ForeColor = Theme.Muted;
        previewNote.Padding = new Padding(5, 10, 0, 0);
        appearancePane.Controls.Add(previewNote);
        appearancePane.Controls.Add(_themePreview);
        appearancePane.Controls.Add(previewCaption);
        appearancePane.Controls.Add(appearanceGrid);
        paneHost.Controls.Add(appearancePane);
        _pages.Add("Appearance", appearancePane);

        // Sticky footer outside scrollable settings body.
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom, Height = 76, BackColor = Theme.Background,
            Padding = new Padding(20, 11, 24, 11)
        };
        var save = new StudioButton { Text = "Сохранить изменения", Width = 194, Height = 45, Tone = ButtonTone.Primary, Dock = DockStyle.Right };
        var cancel = new StudioButton { Text = "Отмена", Width = 117, Height = 45, Tone = ButtonTone.Quiet, Dock = DockStyle.Right };
        save.Click += (_, _) =>
        {
            if (_working.StartHotkey == _working.PauseHotkey ||
                _working.StartHotkey == _working.StopHotkey ||
                _working.PauseHotkey == _working.StopHotkey)
            {
                MessageBox.Show(this, "Старт, пауза и остановка должны иметь разные сочетания.",
                    "KeyER — горячие клавиши", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _working.Validate();
            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        bottom.Controls.Add(save);
        bottom.Controls.Add(cancel);
        Controls.Add(shell);
        Controls.Add(bottom);
        AcceptButton = save;
        CancelButton = cancel;
        ReloadProfileList();
        ShowCategory("Поведение");
    }

    private void ShowCategory(string category)
    {
        _profileSection = category;
        var profileVisible = category is "Поведение" or "Text" or "Code";
        _pages["profiles"].Visible = profileVisible;
        _pages["Hotkeys"].Visible = category == "Hotkeys";
        _pages["Appearance"].Visible = category == "Appearance";
        _settingsFind.Visible = profileVisible;
        _settingsFindStatus.Visible = profileVisible;
        _settingsFind.Clear();

        foreach (var (name, button) in _navButtons)
            button.Tone = name == category ? ButtonTone.Primary : ButtonTone.Quiet;

        (_pageHeading.Text, _pageDescription.Text) = category switch
        {
            "Text" => ("Обычный текст", "Паузы, скорость, переносы и исправления в сообщениях."),
            "Code" => ("Режим Code", "Темп программиста, подсказки, IDE и контроль корректности."),
            "Hotkeys" => ("Горячие клавиши", "Действия KeyER всегда под рукой."),
            "Appearance" => ("Оформление", "Цвета, плотность, шрифт и рабочее пространство."),
            _ => ("Поведение", "Стиль печати, концентрация, усталость и ошибки.")
        };
        FilterProfileFields();
    }

    private void FilterProfileFields()
    {
        if (_fieldsHost.Controls.OfType<TableLayoutPanel>().FirstOrDefault() is not { } table)
            return;

        var query = _settingsFind?.Text.Trim() ?? "";
        var current = "Поведение";
        var count = 0;
        table.SuspendLayout();
        for (var row = 0; row < table.RowCount; row++)
        {
            var caption = table.GetControlFromPosition(0, row) as Label;
            if (caption?.Text.StartsWith("TEXT ENGINE", StringComparison.Ordinal) == true)
                current = "Text";
            else if (caption?.Text.StartsWith("CODE INTELLIGENCE", StringComparison.Ordinal) == true)
                current = "Code";

            var inCategory = current == _profileSection;
            var show = inCategory &&
                (query.Length == 0 ||
                 (caption?.Text.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            if (show && caption is not null) count++;
            foreach (Control control in table.Controls)
                if (table.GetRow(control) == row) control.Visible = show;
        }
        table.ResumeLayout(true);
        _fieldsHost.AutoScrollPosition = Point.Empty;
        if (_settingsFindStatus is not null)
            _settingsFindStatus.Text = query.Length == 0 ? ""
                : count == 0 ? "Ничего не найдено" : $"Найдено: {count}";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F) && _settingsFind.Visible)
        {
            _settingsFind.Focus();
            _settingsFind.SelectAll();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static StudioButton Button(string title, int width, bool primary = false)
    {
        return new StudioButton
        {
            Text = title, Width = width, Height = 36,
            Tone = primary ? ButtonTone.Primary : ButtonTone.Soft
        };
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
            BackColor = Theme.Surface, Padding = new Padding(4, 10, 15, 15)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));

        var p = _selected;
        var styleRow = table.RowCount++;
        table.Controls.Add(Theme.Label("Human Behavior 2.0 — стиль набора", 9.1F, true), 0, styleRow);
        var stylePicker = new ComboBox { Width = 190 };
        Theme.StyleCombo(stylePicker);
        stylePicker.Items.AddRange(Enum.GetNames<HumanStyle>());
        stylePicker.SelectedIndex = (int)p.Style;
        stylePicker.SelectedIndexChanged += (_, _) =>
        {
            if (stylePicker.SelectedIndex < 0) return;
            p.ApplyStyle((HumanStyle)stylePicker.SelectedIndex);
            BeginInvoke(new Action(RebuildFields));
        };
        table.Controls.Add(stylePicker, 1, styleRow);

        var styleNoteRow = table.RowCount++;
        var styleNote = Theme.Label(
            "Beginner: медленно, часто исправляет ошибки. Casual: обычный набор. " +
            "Developer: ритм программиста. Professional: быстро, редко ошибается. " +
            "Custom: ручные параметры (без сброса).", 8.3F);
        styleNote.ForeColor = Theme.Muted;
        styleNote.MaximumSize = new Size(450, 0);
        styleNote.Margin = new Padding(4, 8, 4, 12);
        table.Controls.Add(styleNote, 0, styleNoteRow);
        table.SetColumnSpan(styleNote, 2);

        Number(table, "Окно концентрации: минимум символов", 5, 1000, 5, p.FocusMinCharacters,
            v => p.FocusMinCharacters = (int)v);
        Number(table, "Окно концентрации: максимум символов", 5, 2000, 5, p.FocusMaxCharacters,
            v => p.FocusMaxCharacters = (int)v);
        Number(table, "Восстановление после паузы (%)", 0, 100, 1, p.FocusRecoveryPercent,
            v => p.FocusRecoveryPercent = (int)v);
        Number(table, "Вероятность перерыва между периодами (%)", 0, 100, 1, p.AttentionBreakPercent,
            v => p.AttentionBreakPercent = (int)v);
        Number(table, "Перерыв: минимальная длина (мс)", 0, 15000, 25, p.AttentionBreakMinMilliseconds,
            v => p.AttentionBreakMinMilliseconds = (int)v);
        Number(table, "Перерыв: максимальная длина (мс)", 0, 30000, 25, p.AttentionBreakMaxMilliseconds,
            v => p.AttentionBreakMaxMilliseconds = (int)v);
        Number(table, "Вероятность паузы на обдумывание (%)", 0, 100, 1, p.ThinkingPausePercent,
            v => p.ThinkingPausePercent = (int)v);
        Number(table, "Обдумывание: минимум (мс)", 0, 15000, 25, p.ThinkingPauseMinMilliseconds,
            v => p.ThinkingPauseMinMilliseconds = (int)v);
        Number(table, "Обдумывание: максимум (мс)", 0, 30000, 25, p.ThinkingPauseMaxMilliseconds,
            v => p.ThinkingPauseMaxMilliseconds = (int)v);
        Number(table, "Шанс просмотра предыдущего слова (%)", 0, 30, 1, p.LookbackPercent,
            v => p.LookbackPercent = (int)v);
        Number(table, "Просмотр: максимум символов", 1, 12, 1, p.LookbackMaxCharacters,
            v => p.LookbackMaxCharacters = (int)v);

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
        var textNote = Theme.Label("Для чатов выбирайте Shift+Enter: обычный Enter может отправить сообщение.\n" +
            "Паузы и коррекции относятся только к Text; точность зависит от текстового поля.", 8.3F);
        textNote.ForeColor = Theme.Muted;
        textNote.Margin = new Padding(4, 14, 4, 12);
        table.Controls.Add(textNote, 0, textNoteRow);
        table.SetColumnSpan(textNote, 2);

        var codeTitle = Theme.Label("CODE INTELLIGENCE — синтаксис и IDE", 11F, true);
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
            "Сверка результата доступна при установленной интеграции IDE.", 8.3F);
        codeInfo.ForeColor = Theme.Muted;
        codeInfo.Margin = new Padding(4, 14, 4, 12);
        var codeInfoRow = table.RowCount++;
        table.Controls.Add(codeInfo, 0, codeInfoRow);
        table.SetColumnSpan(codeInfo, 2);

        var integrityRow = table.RowCount++;
        var integrityTitle = Theme.Label("IDE: проверка итогового кода", 9.2F);
        integrityTitle.Margin = new Padding(4, 12, 4, 10);
        table.Controls.Add(integrityTitle, 0, integrityRow);
        var integrity = new ComboBox { Width = 230 };
        Theme.StyleCombo(integrity);
        integrity.Items.AddRange([
            "Отключено (обычный набор)",
            "Проверить без исправления",
            "Проверить и исправить"
        ]);
        integrity.SelectedIndex = (int)p.CodeIntegrity;
        integrity.SelectedIndexChanged += (_, _) =>
            p.CodeIntegrity = (CodeIntegrityMode)integrity.SelectedIndex;
        table.Controls.Add(integrity, 1, integrityRow);

        var integrityNoteRow = table.RowCount++;
        var integrityNote = Theme.Label(
            "Для VS Code нужно установить расширение KeyER. Для Visual Studio\n" +
            "используется экспериментальный адаптер DTE.\n" +
            "Проверка доступна только в пустом документе. Исправление выполняется\n" +
            "средствами IDE после физического набора и не сохраняет файл.", 8.3F);
        integrityNote.ForeColor = Theme.Muted;
        integrityNote.Margin = new Padding(4, 10, 4, 12);
        table.Controls.Add(integrityNote, 0, integrityNoteRow);
        table.SetColumnSpan(integrityNote, 2);

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
        FilterProfileFields();
    }

    private static void Number(TableLayoutPanel table, string label, decimal min,
        decimal max, decimal step, decimal value, Action<decimal> changed, int decimals = 0)
    {
        var row = table.RowCount;
        table.RowCount = row + 1;
        var title = Theme.Label(label, 9.5F);
        title.Margin = new Padding(6, 13, 10, 11);
        var box = new NumericUpDown
        {
            Minimum = min, Maximum = max, Increment = step,
            DecimalPlaces = decimals, Value = Math.Clamp(value, min, max),
            Width = 164, Height = 32,
            BackColor = Theme.Surface, ForeColor = Theme.Foreground,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(4, 8, 8, 9)
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
