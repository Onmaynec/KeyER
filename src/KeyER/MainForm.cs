using System.Drawing;
using System.Text.Json;

namespace KeyER;

/// <summary>KeyER Studio: full responsive WinForms shell, no web runtime or paid assets.</summary>
public sealed partial class MainForm : Form
{
    private AppSettings _settings;
    private readonly TypingEngine _engine = new();
    private Icon? _brandIcon;
    private readonly IdeBridgeServer _ideBridge = new();
    private readonly System.Windows.Forms.Timer _visualStudioMonitor = new() { Interval = 300 };
    private VisualStudioDteSession? _visualStudioSession;
    private string? _vsCodeSessionId;
    private bool _ideBusy;
    private CancellationTokenSource? _idePreflight;

    private readonly RichTextBox _editor;
    private readonly SyntaxPainter _syntaxPainter;
    private readonly EditorGutter _gutter;
    private readonly ComboBox _mode;
    private readonly ComboBox _profiles;
    private readonly ComboBox _languagePicker;
    private readonly Label _language;
    private readonly Label _stats;
    private readonly Label _status;
    private readonly Label _hotkeys;
    private readonly Label _progressPercent;
    private readonly Label _caretInfo;
    private readonly Label _emptyHint;
    private readonly Label _emptyDescription;
    private readonly Label _pageTitle;
    private readonly Label _pageSubtitle;
    private readonly Label _wordCount;
    private readonly StudioProgress _progress;
    private readonly StudioButton _start;
    private readonly StudioButton _pause;
    private readonly StudioButton _stop;
    private readonly StudioButton _diagnostics;
    private readonly StudioButton _sessionReportButton;
    private TypingSessionReport? _lastSessionReport;
    private bool _checkingTarget;
    private readonly StudioButton _textModeButton;
    private readonly StudioButton _codeModeButton;
    private readonly StudioButton _themeToggle;
    private readonly StudioButton _compactToggle;
    private readonly StudioButton _topToggle;
    private readonly StudioButton _searchToggle;
    private readonly StudioButton _focusToggle;
    private readonly StudioButton _wrapToggle;
    private readonly StudioButton _lineToggle;
    private readonly Panel _pageHeader;
    private readonly Panel _workPadding;
    private readonly Panel _footerPadding;
    private bool _focusMode;
    private bool _wordWrap;
    private readonly StudioCard _workspaceCard;
    private readonly Panel _sidebar;
    private readonly Panel _editorSurface;
    private readonly Panel _searchPanel;
    private readonly TextBox _findField;
    private readonly Label _findCount;
    private readonly TableLayoutPanel _root;
    private readonly TableLayoutPanel _mainLayout;
    private readonly System.Windows.Forms.Timer _statsTimer = new() { Interval = 250 };
    private readonly SnippetSearch _search = new();
    private readonly ToolTip _tips = new() { AutomaticDelay = 350, AutoPopDelay = 9000 };
    private StudioButton _searchPrevious = null!;
    private StudioButton _searchNext = null!;
    private StudioButton _caseToggle = null!;
    private Label _sourceBadge = null!;
    private Label _profileDetails = null!;
    private Label _readyStep = null!;
    private Label _estimateLabel = null!;
    private Panel _emptyActions = null!;
    private Panel _emptyOverlay = null!;
    private StudioButton _saveSource = null!;
    private string? _openedFilePath;
    private bool _modified;
    private bool _caseSensitive;

    private HotkeyRegistrationCoordinator? _hotkeyCoordinator;

    private bool _registering;
    private bool _countingDown;
    private bool _importing;
    private bool _editingBlank;
    private CancellationTokenSource? _countdownCts;

    public MainForm()
    {
        _settings = SettingsStore.Load();
        Theme.Configure(_settings.Appearance, _settings.Accent);
        _brandIcon = BrandIcon.Create();
        Icon = _brandIcon;

        Text = "KeyER Studio — Text & Code";
        MinimumSize = new Size(785, 560);
        Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        Font = Theme.UiFont(9.5F);
        KeyPreview = true;
        TopMost = _settings.AlwaysOnTop;

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Theme.Background
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 231));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // SIDE NAVIGATION: brand, mode switch, selected human profile, quick controls.
        _sidebar = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Theme.Surface,
            Padding = new Padding(18, 18, 16, 16), Tag = "surface"
        };

        var brand = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Theme.Surface, Tag = "surface" };
        var logo = new Label
        {
            Text = "K", AutoSize = false, Width = 43, Height = 43,
            Left = 0, Top = 3, TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Theme.AccentFill, ForeColor = Theme.AccentText,
            Font = Theme.UiFont(22F, FontStyle.Bold), Tag = "logo"
        };
        var wordmark = Theme.Label("KeyER", 20F, true);
        wordmark.Left = 55; wordmark.Top = 1;
        var version = Theme.Label("STUDIO  /  v1.1", 8F, true);
        version.Left = 57; version.Top = 35;
        version.ForeColor = Theme.Accent; version.Tag = "accent";
        var brandCaption = Theme.Label("ВАШЕ РАБОЧЕЕ ПРОСТРАНСТВО", 8F, true);
        brandCaption.ForeColor = Theme.Muted; brandCaption.Tag = "muted";
        brandCaption.Left = 0; brandCaption.Top = 78;
        brand.Controls.AddRange([logo, wordmark, version, brandCaption]);

        var nav = new Panel { Dock = DockStyle.Top, Height = 162, BackColor = Theme.Surface, Tag = "surface" };
        var navHeader = Theme.Label("РЕЖИМ РАБОТЫ", 8F, true);
        navHeader.ForeColor = Theme.Muted; navHeader.Tag = "muted";
        navHeader.Location = new Point(1, 11);
        _textModeButton = new StudioButton { Text = "T   Обычный текст", Height = 46, Dock = DockStyle.Top, AlignLeft = true };
        _codeModeButton = new StudioButton { Text = "</>   Редактор кода", Height = 46, Dock = DockStyle.Top, AlignLeft = true };
        var modeList = new Panel { Left = 0, Top = 40, Height = 110, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        modeList.Width = 194; modeList.BackColor = Theme.Surface; modeList.Tag = "surface";
        _textModeButton.Click += (_, _) => SwitchMode(TypingMode.Text);
        _codeModeButton.Click += (_, _) => SwitchMode(TypingMode.Code);
        // Dock order: second added appears below first after reordering.
        modeList.Controls.Add(_codeModeButton);
        modeList.Controls.Add(_textModeButton);
        nav.Controls.AddRange([navHeader, modeList]);
        nav.Resize += (_, _) => modeList.Width = Math.Max(130, nav.ClientSize.Width - 3);

        var profileArea = new Panel { Dock = DockStyle.Top, Height = 122, BackColor = Theme.Surface, Tag = "surface" };
        var profileTitle = Theme.Label("ПРОФИЛЬ ПЕЧАТИ", 8F, true);
        profileTitle.ForeColor = Theme.Muted; profileTitle.Tag = "muted";
        profileTitle.Left = 0; profileTitle.Top = 12;
        _profiles = new ComboBox { Left = 0, Top = 39, Width = 192, Height = 36, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        Theme.StyleCombo(_profiles);
        _profiles.Margin = Padding.Empty;
        _profiles.SelectedIndexChanged += (_, _) =>
        {
            if (_registering || _profiles.SelectedItem is not string value) return;
            _settings.SelectedProfile = value;
            UpdateStats();
            PersistSettings();
        };
        _profileDetails = Theme.Label("Параметры выбранного профиля", 8.1F);
        _profileDetails.ForeColor = Theme.Muted; _profileDetails.Tag = "muted";
        _profileDetails.Left = 1; _profileDetails.Top = 86;
        profileArea.Controls.AddRange([profileTitle, _profiles, _profileDetails]);
        profileArea.Resize += (_, _) => _profiles.Width = Math.Max(130, profileArea.ClientSize.Width - 4);

        _mode = new ComboBox();
        _mode.Items.AddRange(["Text", "Code"]);
        _mode.SelectedIndex = (int)_settings.Mode;
        _mode.SelectedIndexChanged += (_, _) =>
        {
            if (_registering || _mode.SelectedIndex < 0) return;
            _settings.Mode = (TypingMode)_mode.SelectedIndex;
            UpdateStats();
            PersistSettings();
        };

        var sideFooter = new Panel { Dock = DockStyle.Bottom, Height = 166, Tag = "surface", BackColor = Theme.Surface };
        var sideDivider = new Panel { Dock = DockStyle.Top, Height = 1, Tag = "border", BackColor = Theme.Border };
        var footerCaption = Theme.Label("БЫСТРЫЕ НАСТРОЙКИ", 8F, true);
        footerCaption.Tag = "muted"; footerCaption.ForeColor = Theme.Muted;
        footerCaption.Left = 1; footerCaption.Top = 17;
        _themeToggle = new StudioButton { Text = "◐   Светлая / тёмная", Tone = ButtonTone.Quiet, Height = 38 };
        _compactToggle = new StudioButton { Text = "▣   Компактный вид", Tone = ButtonTone.Quiet, Height = 38 };
        _topToggle = new StudioButton { Text = "◇   Поверх окон", Tone = ButtonTone.Quiet, Height = 38 };
        _themeToggle.Click += (_, _) => ToggleAppearance();
        _compactToggle.Click += (_, _) => ToggleCompact();
        _topToggle.Click += (_, _) =>
        {
            _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
            TopMost = _settings.AlwaysOnTop;
            UpdateVisualButtons();
            PersistSettings();
        };
        var quickStack = new TableLayoutPanel
        {
            Left = 0, Top = 39, Height = 122, ColumnCount = 1, RowCount = 3,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Margin = Padding.Empty, Padding = Padding.Empty, Tag = "surface", BackColor = Theme.Surface
        };
        for (var i = 0; i < 3; i++) quickStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        quickStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        quickStack.Controls.Add(_themeToggle, 0, 0);
        quickStack.Controls.Add(_compactToggle, 0, 1);
        quickStack.Controls.Add(_topToggle, 0, 2);
        foreach (var button in new[] { _themeToggle, _compactToggle, _topToggle })
        { button.Dock = DockStyle.Fill; button.Margin = new Padding(0, 2, 0, 2); }
        sideFooter.Controls.AddRange([sideDivider, footerCaption, quickStack]);
        sideFooter.Resize += (_, _) => quickStack.Width = Math.Max(125, sideFooter.ClientSize.Width);

        var readiness = new Panel
        {
            Dock = DockStyle.Top, Height = 120,
            BackColor = Theme.Surface, Tag = "surface"
        };
        var readinessHeader = Theme.Label("ПЕРЕД ЗАПУСКОМ", 8F, true);
        readinessHeader.Left = 1; readinessHeader.Top = 9;
        readinessHeader.ForeColor = Theme.Muted; readinessHeader.Tag = "muted";
        _readyStep = Theme.Label("○  1. Добавьте исходный текст", 8.7F, true);
        _readyStep.Left = 1; _readyStep.Top = 39;
        _readyStep.ForeColor = Theme.Accent; _readyStep.Tag = "accent";
        var targetStep = Theme.Label("○  2. Выберите окно для набора", 8.4F);
        targetStep.Left = 1; targetStep.Top = 66;
        targetStep.ForeColor = Theme.Muted; targetStep.Tag = "muted";
        var launchStep = Theme.Label("○  3. Запустите горячей клавишей", 8.4F);
        launchStep.Left = 1; launchStep.Top = 93;
        launchStep.ForeColor = Theme.Muted; launchStep.Tag = "muted";
        readiness.Controls.AddRange([readinessHeader, _readyStep, targetStep, launchStep]);
        _sidebar.Controls.Add(readiness);
        _sidebar.Controls.Add(profileArea);
        _sidebar.Controls.Add(nav);
        _sidebar.Controls.Add(brand);
        _sidebar.Controls.Add(sideFooter);
        _sidebar.Resize += (_, _) =>
        {
            var shortWindow = _sidebar.ClientSize.Height < 635;
            readiness.Visible = !shortWindow;
            brand.Height = shortWindow ? 78 : 96;
            nav.Height = shortWindow ? 143 : 162;
            profileArea.Height = shortWindow ? 110 : 122;
            sideFooter.Height = shortWindow ? 157 : 166;
        };
        _root.Controls.Add(_sidebar, 0, 0);

        // MAIN: airy header, command-oriented editor and clear persistent run bar.
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Margin = Padding.Empty, Padding = Padding.Empty,
            BackColor = Theme.Background, Tag = "background"
        };
        _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 149));
        _root.Controls.Add(_mainLayout, 1, 0);

        _pageHeader = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(17, 12, 16, 6),
            BackColor = Theme.Background, Tag = "background"
        };
        var pageHeader = _pageHeader;
        _pageTitle = Theme.Label("Текстовый редактор", 20F, true);
        _pageTitle.Left = 17; _pageTitle.Top = 14;
        _pageTitle.AutoSize = false; _pageTitle.Height = 37;
        _pageTitle.AutoEllipsis = true;
        _pageSubtitle = Theme.Label("Подготовьте текст и запустите естественный набор", 9.2F);
        _pageSubtitle.ForeColor = Theme.Muted; _pageSubtitle.Tag = "muted";
        _pageSubtitle.Left = 19; _pageSubtitle.Top = 52;
        _pageSubtitle.AutoSize = false; _pageSubtitle.Height = 23;
        _pageSubtitle.AutoEllipsis = true;

        var headerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, Width = 485, Height = 48,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            BackColor = Theme.Background, Tag = "background",
            Padding = new Padding(0, 7, 0, 0)
        };
        var settingsButton = new StudioButton { Text = "⚙   Настройки", Width = 126, Height = 38, Tone = ButtonTone.Soft };
        settingsButton.Click += (_, _) => OpenSettings();
        _searchToggle = new StudioButton { Text = "⌕   Поиск", Width = 102, Height = 38, Tone = ButtonTone.Quiet };
        _searchToggle.Click += (_, _) => ToggleFind();
        var helpButton = new StudioButton { Text = "?   Справка", Width = 101, Height = 38, Tone = ButtonTone.Quiet };
        helpButton.Click += (_, _) => ShowHelp();
        _focusToggle = new StudioButton { Text = "⛶   Фокус F11", Width = 117, Height = 38,
            Tone = ButtonTone.Outline, AccessibleName = "Режим фокуса редактора. F11" };
        _focusToggle.Click += (_, _) => ToggleFocusMode();
        headerActions.Controls.AddRange([_searchToggle, _focusToggle, helpButton, settingsButton]);
        pageHeader.Resize += (_, _) =>
        {
            var small = pageHeader.ClientSize.Width < 885;
            helpButton.Visible = !small;
            headerActions.Width = small ? 336 : 485;
            _searchToggle.Width = small ? 70 : 102;
            _searchToggle.Text = small ? "⌕" : "⌕   Поиск";
            _focusToggle.Width = small ? 112 : 117;
            settingsButton.Width = small ? 114 : 126;
            _pageTitle.Width = Math.Max(135, pageHeader.ClientSize.Width - headerActions.Width - 30);
            _pageSubtitle.Width = _pageTitle.Width;
            _pageSubtitle.Visible = !small && !_settings.CompactMode;
            _pageTitle.Font = Theme.UiFont(small || _settings.CompactMode ? 16F : 19F,
                FontStyle.Bold);
        };
        pageHeader.Controls.AddRange([_pageTitle, _pageSubtitle, headerActions]);
        _mainLayout.Controls.Add(pageHeader, 0, 0);

        _workPadding = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(14, 0, 14, 11),
            Tag = "background", BackColor = Theme.Background
        };
        var workPadding = _workPadding;
        _workspaceCard = new StudioCard
        {
            Dock = DockStyle.Fill, Padding = new Padding(2), Radius = 18
        };
        var editorMain = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Tag = "surface" };
        var editorToolbar = new Panel
        {
            Dock = DockStyle.Top, Height = 62, Padding = new Padding(15, 7, 12, 7),
            BackColor = Theme.Surface, Tag = "surface"
        };
        _sourceBadge = Theme.Label("◉  НОВЫЙ ФРАГМЕНТ", 8.7F, true);
        _sourceBadge.Tag = "accent"; _sourceBadge.ForeColor = Theme.Accent;
        _sourceBadge.AutoSize = false;
        _sourceBadge.AutoEllipsis = true;
        _sourceBadge.SetBounds(15, 17, 145, 25);

        _languagePicker = new ComboBox { Width = 132, Height = 32,
            AccessibleName = "Язык исходного кода" };
        Theme.StyleCombo(_languagePicker);
        _languagePicker.Margin = new Padding(4, 6, 0, 0);
        _languagePicker.Items.AddRange(Enum.GetNames<CodeAnalyzer.Language>());
        _languagePicker.SelectedItem = _settings.CodeLanguage;
        if (_languagePicker.SelectedIndex < 0) _languagePicker.SelectedIndex = 0;
        _languagePicker.SelectedIndexChanged += (_, _) =>
        {
            if (_registering || _languagePicker.SelectedItem is not string value) return;
            _settings.CodeLanguage = value;
            UpdateStats();
            _syntaxPainter?.Schedule();
            PersistSettings();
        };

        _wrapToggle = new StudioButton
        {
            Text = "⇄  Перенос", Width = 106, Height = 36,
            Left = 313, Top = 6, Tone = ButtonTone.Quiet,
            AccessibleName = "Перенос длинных строк редактора, Alt+Z"
        };
        _wrapToggle.Click += (_, _) => ToggleWordWrap();
        _lineToggle = new StudioButton
        {
            Text = "№  Строки", Width = 99, Height = 36,
            Left = 422, Top = 6, Tone = ButtonTone.Quiet,
            AccessibleName = "Показать или скрыть номера строк"
        };
        _lineToggle.Click += (_, _) => ToggleLineNumbers();
        var commandRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, Width = 492, Height = 45,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Padding = Padding.Empty, BackColor = Theme.Surface, Tag = "surface"
        };
        var pasteButton = new StudioButton { Text = "Вставить", Width = 80, Height = 36 };
        var openButton = new StudioButton { Text = "Файл", Width = 68, Height = 36 };
        var clearButton = new StudioButton { Text = "Очистить", Width = 85, Height = 36, Tone = ButtonTone.Quiet };
        var copyButton = new StudioButton { Text = "Копия", Width = 75, Height = 36, Tone = ButtonTone.Quiet };
        _saveSource = new StudioButton
        {
            Text = "Сохранить", Width = 91, Height = 36, Tone = ButtonTone.Soft
        };
        _saveSource.Click += (_, _) => SaveCurrentSource();
        pasteButton.Click += (_, _) => PasteIntoEditor();
        openButton.Click += (_, _) => OpenFile();
        clearButton.Click += (_, _) => ClearSource();
        copyButton.Click += (_, _) =>
        {
            try
            {
                if (!string.IsNullOrEmpty(_editor!.Text)) Clipboard.SetText(_editor.Text);
                SetStatus("Текст скопирован в буфер обмена.");
            }
            catch (Exception ex) { SetStatus("Копирование не удалось: " + ex.Message); }
        };
        commandRow.Controls.AddRange([pasteButton, openButton, _saveSource, copyButton, clearButton]);
        _languagePicker.Left = 163;
        _languagePicker.Top = 7;
        editorToolbar.Controls.Add(_sourceBadge);
        editorToolbar.Controls.Add(_languagePicker);
        editorToolbar.Controls.Add(_wrapToggle);
        editorToolbar.Controls.Add(_lineToggle);
        editorToolbar.Controls.Add(commandRow);
        editorToolbar.Resize += (_, _) =>
        {
            var wide = editorToolbar.ClientSize.Width >= 1060;
            var tight = editorToolbar.ClientSize.Width < 850;
            var narrow = editorToolbar.ClientSize.Width < 610;
            copyButton.Visible = !tight;
            clearButton.Visible = !tight;
            commandRow.Width = tight ? 267 : 492;
            _sourceBadge.Visible = !narrow;
            _languagePicker.Left = narrow ? 13 : 163;
            _languagePicker.Width = narrow ? 107 : 132;
            _wrapToggle.Visible = wide;
            _lineToggle.Visible = wide;
            _wrapToggle.Left = _languagePicker.Right + 12;
            _lineToggle.Left = _wrapToggle.Right + 4;
        };

        var separatorTop = new Panel { Dock = DockStyle.Top, Height = 1, Tag = "border", BackColor = Theme.Border };
        _searchPanel = new Panel
        {
            Dock = DockStyle.Top, Height = 43, Visible = false,
            Padding = new Padding(18, 4, 12, 4), Tag = "surface", BackColor = Theme.Surface
        };
        var findGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1,
            Margin = Padding.Empty, Padding = Padding.Empty,
            BackColor = Theme.Surface, Tag = "surface"
        };
        findGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var width in new float[] { 30, -1, 86, 36, 36, 44, 36 })
            findGrid.ColumnStyles.Add(width < 0
                ? new ColumnStyle(SizeType.Percent, 100)
                : new ColumnStyle(SizeType.Absolute, width));

        var findIcon = Theme.Label("⌕", 15F, true);
        findIcon.ForeColor = Theme.Accent; findIcon.Tag = "accent";
        findIcon.Dock = DockStyle.Fill; findIcon.AutoSize = false;
        findIcon.TextAlign = ContentAlignment.MiddleCenter;

        _findField = new TextBox
        {
            BorderStyle = BorderStyle.None, Font = Theme.UiFont(10.1F),
            BackColor = Theme.SurfaceRaised, ForeColor = Theme.Foreground,
            PlaceholderText = "Поиск по исходнику", Dock = DockStyle.Fill,
            Margin = new Padding(6, 9, 6, 5)
        };
        _findField.TextChanged += (_, _) => FindInEditor(forward: true, quiet: true);
        _findField.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                FindInEditor(!e.Shift);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ToggleFind(false);
                e.SuppressKeyPress = true;
            }
        };
        _findCount = Theme.Label("0 из 0", 8F);
        _findCount.ForeColor = Theme.Muted; _findCount.Tag = "muted";
        _findCount.Dock = DockStyle.Fill; _findCount.AutoSize = false;
        _findCount.TextAlign = ContentAlignment.MiddleCenter;

        _searchPrevious = new StudioButton { Text = "↑", Dock = DockStyle.Fill, Tone = ButtonTone.Quiet };
        _searchNext = new StudioButton { Text = "↓", Dock = DockStyle.Fill, Tone = ButtonTone.Quiet };
        _caseToggle = new StudioButton { Text = "Aa", Dock = DockStyle.Fill, Tone = ButtonTone.Quiet };
        var closeFind = new StudioButton { Text = "×", Dock = DockStyle.Fill, Tone = ButtonTone.Quiet };
        foreach (var button in new[] { _searchPrevious, _searchNext, _caseToggle, closeFind })
            button.Margin = new Padding(1, 3, 1, 3);
        _searchPrevious.Click += (_, _) => FindInEditor(false);
        _searchNext.Click += (_, _) => FindInEditor(true);
        _caseToggle.Click += (_, _) =>
        {
            _caseSensitive = !_caseSensitive;
            _caseToggle.Tone = _caseSensitive ? ButtonTone.Primary : ButtonTone.Quiet;
            FindInEditor(true, quiet: true);
        };
        closeFind.Click += (_, _) => ToggleFind(false);
        findGrid.Controls.Add(findIcon, 0, 0);
        findGrid.Controls.Add(_findField, 1, 0);
        findGrid.Controls.Add(_findCount, 2, 0);
        findGrid.Controls.Add(_searchPrevious, 3, 0);
        findGrid.Controls.Add(_searchNext, 4, 0);
        findGrid.Controls.Add(_caseToggle, 5, 0);
        findGrid.Controls.Add(closeFind, 6, 0);
        _searchPanel.Controls.Add(findGrid);
        _tips.SetToolTip(_searchPrevious, "Предыдущее совпадение (Shift+Enter)");
        _tips.SetToolTip(_searchNext, "Следующее совпадение (Enter)");
        _tips.SetToolTip(_caseToggle, "Учитывать регистр");
        _tips.SetToolTip(closeFind, "Закрыть поиск (Esc)");

        _editorSurface = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(10, 14, 17, 12),
            BackColor = Theme.Input, Tag = "input"
        };
        _editor = new RichTextBox
        {
            Dock = DockStyle.Fill, Multiline = true, AcceptsTab = true,
            ScrollBars = RichTextBoxScrollBars.Both, WordWrap = false,
            BorderStyle = BorderStyle.None, DetectUrls = false,
            HideSelection = false, EnableAutoDragDrop = false,
            Font = new Font("Cascadia Code", _settings.EditorFontSize),
            BackColor = Theme.Input, ForeColor = Theme.Foreground,
            ShortcutsEnabled = true,
            AccessibleName = "Редактор исходного текста и кода"
        };
        _editor.TextChanged += (_, _) =>
        {
            if (!_importing) _modified = true;
            if (_editor.TextLength > 0) _editingBlank = true;
            RefreshEmptyState();
            // An old stopped session must not display 17.8% progress on
            // an empty or newly edited source. Keep the optional old report.
            if (!_engine.IsRunning && !_countingDown && !_ideBusy &&
                _progress is not null && _progress.Value != 0)
            {
                ResetSourceProgress();
                SetStatus("Исходник изменён. Готов к новому запуску.");
            }
            _statsTimer.Stop();
            _statsTimer.Start();
            _syntaxPainter!.Schedule();
            _gutter!.Invalidate();
            if (_searchPanel.Visible) FindInEditor(true, quiet: true);
            UpdateStats();
            UpdateButtons();
        };
        _editor.SelectionChanged += (_, _) => { UpdateCaret(); _gutter!.Invalidate(); };
        _editor.VScroll += (_, _) => _gutter!.Invalidate();
        _editor.MouseWheel += (_, _) => _gutter!.Invalidate();
        _editor.KeyDown += (_, e) =>
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.S)
            { SaveSourceAs(); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.F)
            { ToggleFind(show: true); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Escape && _searchPanel.Visible)
            { ToggleFind(); e.SuppressKeyPress = true; }
        };
        AllowDrop = true;
        _editor.AllowDrop = true;
        _editorSurface.AllowDrop = true;
        DragEnter += FileDragEnter;
        DragDrop += FileDropped;
        _editor.DragEnter += FileDragEnter;
        _editor.DragDrop += FileDropped;
        _editorSurface.DragEnter += FileDragEnter;
        _editorSurface.DragDrop += FileDropped;
        _gutter = new EditorGutter { Editor = _editor, Visible = _settings.ShowLineNumbers };
        _emptyHint = Theme.Label("Начните с исходника", 16F, true);
        _emptyHint.ForeColor = Theme.Foreground;
        _emptyHint.AutoSize = false;
        _emptyHint.SetBounds(87, 48, 440, 42);
        _emptyHint.TextAlign = ContentAlignment.MiddleLeft;
        _emptyHint.Click += (_, _) => BeginManualEntry();
        _emptyDescription = Theme.Label(
            "Вставьте текст или откройте файл. Он останется здесь,\n" +
            "пока вы не очистите редактор вручную.", 9.2F);
        _emptyDescription.AutoSize = false;
        _emptyDescription.SetBounds(87, 98, 468, 59);
        _emptyDescription.ForeColor = Theme.Muted;
        _emptyDescription.Tag = "muted";
        _emptyDescription.Click += (_, _) => BeginManualEntry();

        _emptyActions = new Panel
        {
            Width = 310, Height = 96, Left = 84, Top = 143,
            BackColor = Theme.Input, Tag = "input"
        };
        var emptyPaste = new StudioButton
        {
            Text = "Вставить текст", Width = 148, Height = 42,
            Left = 0, Top = 2, Tone = ButtonTone.Primary
        };
        var emptyOpen = new StudioButton
        {
            Text = "Открыть файл", Width = 145, Height = 42,
            Left = 157, Top = 2, Tone = ButtonTone.Outline
        };
        emptyPaste.Click += (_, _) => PasteIntoEditor();
        emptyOpen.Click += (_, _) => OpenFile();
        var emptyManual = new StudioButton
        {
            Text = "Или начать печатать вручную", Width = 300,
            Height = 37, Left = 1, Top = 53, Tone = ButtonTone.Quiet
        };
        emptyManual.Click += (_, _) => BeginManualEntry();
        _emptyActions.Controls.AddRange([emptyPaste, emptyOpen, emptyManual]);
        _emptyOverlay = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Theme.Input, Tag = "input",
            AccessibleName = "Подготовка исходного текста"
        };
        _emptyOverlay.Click += (_, _) => BeginManualEntry();
        _emptyOverlay.AllowDrop = true;
        _emptyOverlay.DragEnter += FileDragEnter;
        _emptyOverlay.DragDrop += FileDropped;
        _emptyOverlay.Controls.Add(_emptyHint);
        _emptyOverlay.Controls.Add(_emptyDescription);
        _emptyOverlay.Controls.Add(_emptyActions);
        _emptyOverlay.Resize += (_, _) =>
        {
            var x = Math.Max(30, Math.Min(87, _emptyOverlay.ClientSize.Width / 8));
            _emptyHint.Left = x;
            _emptyDescription.Left = x;
            _emptyHint.Width = Math.Max(200, _emptyOverlay.ClientSize.Width - x - 18);
            _emptyDescription.Width = _emptyHint.Width;
            _emptyActions.Left = x - 3;
            _emptyHint.Top = Math.Max(20, _emptyOverlay.ClientSize.Height / 6);
            _emptyDescription.Top = _emptyHint.Top + 53;
            _emptyActions.Top = _emptyHint.Top + 116;
        };
        _editorSurface.Controls.Add(_editor);
        _editorSurface.Controls.Add(_gutter);
        _editorSurface.Controls.Add(_emptyOverlay);
        _emptyOverlay.BringToFront();
        _editor.Visible = false; // The native RichEdit window cannot obscure onboarding.
        _gutter.Visible = false;
        _syntaxPainter = new SyntaxPainter(_editor)
        {
            IsCode = _settings.Mode == TypingMode.Code,
            Enabled = _settings.SyntaxHighlighting,
            FileName = _settings.SourceFileName,
            Language = _settings.CodeLanguage
        };
        _statsTimer.Tick += (_, _) => { _statsTimer.Stop(); UpdateStats(); };

        var editorMeta = new Panel
        {
            Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(16, 5, 14, 5),
            Tag = "surface", BackColor = Theme.Surface
        };
        _language = Theme.Label("Text", 8.5F, true);
        _language.ForeColor = Theme.Accent; _language.Tag = "accent";
        _language.Dock = DockStyle.Left;
        _language.Width = 205; _language.AutoSize = false;
        _language.TextAlign = ContentAlignment.MiddleLeft;
        _caretInfo = Theme.Label("Ln 1, Col 1", 8.5F);
        _caretInfo.ForeColor = Theme.Muted; _caretInfo.Tag = "muted";
        _caretInfo.Dock = DockStyle.Right;
        _caretInfo.AutoSize = false; _caretInfo.Width = 115;
        _caretInfo.TextAlign = ContentAlignment.MiddleRight;
        _stats = Theme.Label("0 символов", 8.5F);
        _stats.ForeColor = Theme.Muted; _stats.Tag = "muted";
        _stats.AutoSize = false; _stats.Width = 166; _stats.Dock = DockStyle.Right;
        _stats.TextAlign = ContentAlignment.MiddleRight;
        _wordCount = Theme.Label("0 слов", 8.5F);
        _wordCount.ForeColor = Theme.Muted; _wordCount.Tag = "muted";
        _wordCount.AutoSize = false; _wordCount.Width = 105; _wordCount.Dock = DockStyle.Right;
        _wordCount.TextAlign = ContentAlignment.MiddleRight;
        editorMeta.Controls.AddRange([_language, _caretInfo, _stats, _wordCount]);
        editorMeta.Resize += (_, _) =>
        {
            var tight = editorMeta.ClientSize.Width < 670;
            _wordCount.Visible = !tight;
            _language.Width = tight ? 160 : 205;
            _stats.Width = tight ? 122 : 166;
            _caretInfo.Width = tight ? 100 : 115;
        };

        editorMain.Controls.Add(_editorSurface);
        editorMain.Controls.Add(_searchPanel);
        editorMain.Controls.Add(separatorTop);
        editorMain.Controls.Add(editorToolbar);
        editorMain.Controls.Add(editorMeta);
        _workspaceCard.Controls.Add(editorMain);
        workPadding.Controls.Add(_workspaceCard);
        _mainLayout.Controls.Add(workPadding, 0, 1);

        _footerPadding = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(14, 0, 14, 14),
            BackColor = Theme.Background, Tag = "background"
        };
        var footerPadding = _footerPadding;
        var commandCard = new StudioCard { Dock = DockStyle.Fill, Radius = 18, Padding = new Padding(18, 11, 18, 9) };
        var bottomLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            BackColor = Theme.Surface, Tag = "surface", Margin = Padding.Empty, Padding = Padding.Empty
        };
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 9));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var statusRow = new Panel { Dock = DockStyle.Fill, Tag = "surface", BackColor = Theme.Surface };
        _status = Theme.Label("●   Готово к работе", 9.1F, true);
        _status.ForeColor = Theme.Success; _status.Tag = "success";
        _status.Dock = DockStyle.Fill; _status.AutoSize = false;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _progressPercent = Theme.Label("0%", 9F, true);
        _progressPercent.ForeColor = Theme.Muted; _progressPercent.Tag = "muted";
        _progressPercent.Dock = DockStyle.Right; _progressPercent.AutoSize = false;
        _progressPercent.TextAlign = ContentAlignment.MiddleRight; _progressPercent.Width = 72;
        _sessionReportButton = new StudioButton
        {
            Text = "Отчёт", Width = 87, Height = 27,
            Dock = DockStyle.Right, Tone = ButtonTone.Quiet, Enabled = false,
            CornerRadius = 8, Margin = Padding.Empty
        };
        _sessionReportButton.Click += (_, _) =>
        {
            if (_lastSessionReport is null) return;
            using var report = new SessionReportForm(_lastSessionReport);
            report.ShowDialog(this);
        };
        statusRow.Controls.Add(_status);
        statusRow.Controls.Add(_progressPercent);
        statusRow.Controls.Add(_sessionReportButton);
        bottomLayout.Controls.Add(statusRow, 0, 0);

        _progress = new StudioProgress { Dock = DockStyle.Fill, Value = 0 };
        bottomLayout.Controls.Add(_progress, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Tag = "surface", BackColor = Theme.Surface,
            WrapContents = false, AutoScroll = true,
            Padding = new Padding(0, 9, 0, 1)
        };
        _start = new StudioButton { Text = "▶    Начать набор", Width = 184, Height = 44, Tone = ButtonTone.Primary };
        _pause = new StudioButton { Text = "Ⅱ    Пауза", Width = 141, Height = 44, Tone = ButtonTone.Soft };
        _stop = new StudioButton { Text = "■    Остановить", Width = 151, Height = 44, Tone = ButtonTone.Outline };
        _diagnostics = new StudioButton
        {
            Text = "◉   Проверить окно", Width = 171, Height = 44,
            Tone = ButtonTone.Quiet
        };
        _start.Click += async (_, _) => await StartWithCountdownAsync();
        _pause.Click += (_, _) => { _engine.TogglePause(); UpdateButtons(); };
        _stop.Click += (_, _) => StopTyping();
        _diagnostics.Click += async (_, _) => await CheckTargetAsync();
        actions.Controls.AddRange([_start, _pause, _stop, _diagnostics]);
        actions.Resize += (_, _) =>
        {
            var tight = actions.ClientSize.Width < 530;
            _start.Width = tight ? 135 : 184;
            _pause.Width = tight ? 107 : 141;
            _stop.Width = tight ? 105 : 151;
            _diagnostics.Width = tight ? 104 : 171;
            _start.Text = tight ? "▶   Начать" : "▶    Начать набор";
            _stop.Text = tight ? "■   Стоп" : "■    Остановить";
            _diagnostics.Text = tight ? "◉  Окно" : "◉   Проверить окно";
        };
        bottomLayout.Controls.Add(actions, 0, 2);

        var footerInfo = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Theme.Surface, Tag = "surface"
        };
        _hotkeys = Theme.Label("", 8.2F);
        _hotkeys.ForeColor = Theme.Muted; _hotkeys.Tag = "muted";
        _hotkeys.Dock = DockStyle.Fill; _hotkeys.AutoSize = false;
        _hotkeys.TextAlign = ContentAlignment.BottomLeft;
        _estimateLabel = Theme.Label("", 8F);
        _estimateLabel.ForeColor = Theme.Muted; _estimateLabel.Tag = "muted";
        _estimateLabel.Dock = DockStyle.Right; _estimateLabel.AutoSize = false;
        _estimateLabel.Width = 202;
        _estimateLabel.TextAlign = ContentAlignment.BottomRight;
        footerInfo.Controls.Add(_hotkeys);
        footerInfo.Controls.Add(_estimateLabel);
        footerInfo.Resize += (_, _) =>
            _estimateLabel.Visible = footerInfo.ClientSize.Width >= 665;
        bottomLayout.Controls.Add(footerInfo, 0, 3);
        commandCard.Controls.Add(bottomLayout);
        footerPadding.Controls.Add(commandCard);
        _mainLayout.Controls.Add(footerPadding, 0, 2);
        Controls.Add(_root);

        RefreshProfiles();
        UpdateStats();
        UpdateButtons();
        _tips.SetToolTip(_start, "Старт с обратным отсчётом. Переключитесь в целевое окно и установите курсор.");
        _tips.SetToolTip(_pause, "Приостановить или возобновить текущий набор");
        _tips.SetToolTip(_stop, "Остановить набор без дальнейшего ввода");
        _tips.SetToolTip(_diagnostics,
            "Через три секунды проверить выбранное окно, его состояние и совместимость по правам доступа.");
        _tips.SetToolTip(_sessionReportButton,
            "Итог последней попытки без текста, имён файлов и заголовка целевого окна");
        _tips.SetToolTip(_saveSource,
            "Ctrl+S: сохранить текущий файл. Ctrl+Shift+S: сохранить под новым именем.");
        _tips.SetToolTip(_focusToggle, "Фокус F11: скрыть боковую панель, увеличить редактор");
        _tips.SetToolTip(_wrapToggle, "Alt+Z: переносить длинные строки без изменения исходника");
        _tips.SetToolTip(_lineToggle, "Показать или скрыть номера строк");
        _tips.SetToolTip(_profiles, "Выберите скорость, характер ошибок и стиль набора");
        ApplyCompactLayout();
        UpdateVisualButtons();
        UpdateCaret();

        _engine.StatusChanged += message => Ui(() => SetStatus(message));
        _engine.ProgressChanged += (completed, total) => Ui(() =>
        {
            var percent = Math.Clamp(total == 0 ? 0 : (int)((long)completed * 1000 / total), 0, 1000);
            _progress.Value = percent;
            _progressPercent.Text = $"{percent / 10.0:0.0}%";
        });

        _ideBridge.ConnectionChanged += connected => Ui(() =>
        {
            if (!connected && _vsCodeSessionId is not null)
            {
                _engine.Stop();
                SetStatus("VS Code отключился. Набор остановлен.");
            }
            else if (connected && !_engine.IsRunning && !_ideBusy)
                SetStatus("VS Code: расширение KeyER подключено.");
        });
        _ideBridge.SessionInvalidated += session => Ui(() =>
        {
            if (_vsCodeSessionId == session)
            {
                _engine.Stop();
                SetStatus("Редактор VS Code изменился — набор остановлен.");
            }
        });
        _visualStudioMonitor.Tick += (_, _) =>
        {
            if (_visualStudioSession is not null && _engine.IsRunning &&
                !_visualStudioSession.IsStillActive())
            {
                _engine.Stop();
                SetStatus("Активный документ Visual Studio изменился. Набор остановлен.");
            }
        };

        Shown += (_, _) =>
        {
            Theme.ApplyWindowFrame(this);
            if (Environment.GetEnvironmentVariable("KEYER_UI_PREVIEW") == "1") return;
            RegisterHotkeys(showWarnings: true);
            _ideBridge.Start();
            _syntaxPainter.Schedule();
            BeginInvoke(new Action(() =>
            {
                if (!_editor.IsDisposed && _editor.Visible) _editor.Focus();
            }));
        };
        ResizeEnd += (_, _) =>
        {
            if (WindowState != FormWindowState.Normal) return;
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            PersistSettings();
        };
        FormClosing += (_, e) =>
        {
            if (Environment.GetEnvironmentVariable("KEYER_UI_PREVIEW") != "1" &&
                _modified && _editor.TextLength > 0 &&
                !StudioDialog.Confirm(this, "Закрыть KeyER?",
                    "В редакторе остался несохранённый текст. При закрытии KeyER он будет потерян.\n\n" +
                    "Можно отменить закрытие и сохранить исходник через Ctrl+S."))
            {
                e.Cancel = true;
                return;
            }
            _idePreflight?.Cancel();
            _countdownCts?.Cancel();
            _engine.Stop();
            _visualStudioMonitor.Stop();
            _visualStudioMonitor.Dispose();
            _statsTimer.Dispose();
            _syntaxPainter.Dispose();
            _tips.Dispose();
            _brandIcon?.Dispose();
            _ideBridge.Dispose();
            UnregisterHotkeys();
        };
    }

    private void BeginManualEntry()
    {
        _editingBlank = true;
        RefreshEmptyState();
        _editor.Focus();
    }

    private void RefreshEmptyState()
    {
        if (_emptyOverlay is null || _gutter is null || _editor is null) return;
        var welcome = _editor.TextLength == 0 && !_editingBlank;
        _emptyOverlay.Visible = welcome;
        _editor.Visible = !welcome;
        _gutter.Visible = !welcome && _settings.ShowLineNumbers;
        if (welcome) _emptyOverlay.BringToFront();
        else
        {
            _editor.BringToFront();
            _gutter.BringToFront();
        }
    }

    private void SwitchMode(TypingMode mode)
    {
        if (_engine.IsRunning || _countingDown || _ideBusy) return;
        _settings.Mode = mode;
        _registering = true;
        _mode.SelectedIndex = (int)mode;
        _registering = false;
        UpdateStats();
        PersistSettings();
    }

    private void UpdateCaret()
    {
        if (_editor is null || _caretInfo is null) return;
        var index = _editor.SelectionStart;
        var line = _editor.GetLineFromCharIndex(index);
        var offset = _editor.GetFirstCharIndexFromLine(line);
        _caretInfo.Text = $"Ln {line + 1}, Col {Math.Max(0, index - offset) + 1}";
    }

    private void ToggleFind(bool? show = null)
    {
        var visible = show ?? !_searchPanel.Visible;
        _searchPanel.Visible = visible;
        if (visible)
        {
            if (_editor.SelectionLength > 0 && _editor.SelectedText.Length <= 100 &&
                !_editor.SelectedText.Contains('\n'))
                _findField.Text = _editor.SelectedText;
            _findField.Focus();
            _findField.SelectAll();
            FindInEditor(true, quiet: true);
        }
        else _editor.Focus();
    }

    private void FindInEditor(bool forward, bool quiet = false)
    {
        _search.Update(_editor.Text, _findField.Text, _caseSensitive);
        var total = _search.Offsets.Count;
        if (total == 0)
        {
            _findCount.Text = _findField.TextLength == 0 ? "0 из 0" : "Не найдено";
            _findCount.ForeColor = _findField.TextLength > 0 ? Theme.Danger : Theme.Muted;
            _searchPrevious.Enabled = false;
            _searchNext.Enabled = false;
            return;
        }
        _findCount.ForeColor = Theme.Muted;
        _searchPrevious.Enabled = true;
        _searchNext.Enabled = true;
        var start = quiet ? _editor.SelectionStart :
            forward ? _editor.SelectionStart + (_editor.SelectionLength > 0 ? 1 : 0)
                    : _editor.SelectionStart;
        var index = _search.Next(start, forward, includeCurrent: quiet);
        if (index >= 0)
        {
            _editor.Select(index, _search.Length);
            _editor.ScrollToCaret();
            var ordinal = _search.OrdinalAt(index);
            _findCount.Text = $"{ordinal} из {total}" + (_search.Truncated ? "+" : "");
            if (!quiet) _findField.Focus();
        }
    }

    private void PasteIntoEditor()
    {
        if (_editor.ReadOnly) return;
        try
        {
            if (Clipboard.ContainsText()) _editor.SelectedText = Clipboard.GetText();
            else SetStatus("В буфере обмена нет текста.");
        }
        catch (Exception ex) { SetStatus("Не удалось вставить: " + ex.Message); }
    }

    private void ShowHelp()
    {
        StudioDialog.Guide(this,
            Native.HotkeyText(_settings.StartHotkey),
            Native.HotkeyText(_settings.PauseHotkey),
            Native.HotkeyText(_settings.StopHotkey));
    }

    private void ToggleAppearance()
    {
        _settings.Appearance = Theme.IsLight ? AppearanceMode.Dark : AppearanceMode.Light;
        ApplyAppearance();
        PersistSettings();
    }

    private void ToggleCompact()
    {
        _settings.CompactMode = !_settings.CompactMode;
        ApplyCompactLayout();
        UpdateVisualButtons();
        PersistSettings();
    }

    private void ToggleFocusMode()
    {
        _focusMode = !_focusMode;
        // F11 gives the editor almost the full window width, but the Start,
        // Pause and Stop bar always remains visible for safe operation.
        _sidebar.Visible = !_focusMode;
        _focusToggle.Text = _focusMode ? "▣   Панель F11" : "⛶   Фокус F11";
        _focusToggle.Tone = _focusMode ? ButtonTone.Primary : ButtonTone.Outline;
        ApplyCompactLayout();
        _editor.Focus();
    }

    private void ToggleWordWrap()
    {
        _wordWrap = !_wordWrap;
        _editor.WordWrap = _wordWrap;
        _editor.ScrollBars = _wordWrap ? RichTextBoxScrollBars.Vertical :
            RichTextBoxScrollBars.Both;
        _gutter.Invalidate();
        UpdateEditorViewButtons();
        // Reflow is a view-only operation: no changes to source or undo history.
    }

    private void ToggleLineNumbers()
    {
        _settings.ShowLineNumbers = !_settings.ShowLineNumbers;
        RefreshEmptyState();
        UpdateEditorViewButtons();
        PersistSettings();
    }

    private void UpdateEditorViewButtons()
    {
        if (_wrapToggle is null || _lineToggle is null) return;
        _wrapToggle.Tone = _wordWrap ? ButtonTone.Outline : ButtonTone.Quiet;
        _wrapToggle.Text = _wordWrap ? "↵  Перенос: вкл" : "⇄  Перенос";
        _wrapToggle.Width = _wordWrap ? 133 : 106;
        _lineToggle.Left = _wrapToggle.Right + 4;
        _lineToggle.Tone = _settings.ShowLineNumbers
            ? ButtonTone.Outline : ButtonTone.Quiet;
        _lineToggle.Text = _settings.ShowLineNumbers ? "№  Строки" : "№  Без строк";
    }

    private void ApplyCompactLayout()
    {
        var compact = _settings.CompactMode;
        _root.ColumnStyles[0].Width = _focusMode ? 0 : compact ? 191 : 231;
        _mainLayout.RowStyles[0].Height = _focusMode ? 75 : compact ? 78 : 94;
        // The persistent Start/Pause/Stop bar must stay at least 44 px tall.
        _mainLayout.RowStyles[2].Height = 149;
        _pageTitle.Font = Theme.UiFont(compact ? 16F : 19F, FontStyle.Bold);
        _pageSubtitle.Visible = !compact && !_focusMode && _pageHeader.ClientSize.Width >= 885;
        _pageHeader.Invalidate();
    }

    private void UpdateVisualButtons()
    {
        _textModeButton.Tone = _settings.Mode == TypingMode.Text ? ButtonTone.Primary : ButtonTone.Quiet;
        _codeModeButton.Tone = _settings.Mode == TypingMode.Code ? ButtonTone.Primary : ButtonTone.Quiet;
        _topToggle.Text = _settings.AlwaysOnTop ? "◆   Поверх окон: ВКЛ" : "◇   Поверх окон";
        _compactToggle.Text = _settings.CompactMode ? "▤   Обычный вид" : "▣   Компактный вид";
        _themeToggle.Text = Theme.IsLight ? "☾   Тёмная тема" : "◐   Светлая тема";
        _topToggle.Invalidate();
    }

    private void ApplyAppearance()
    {
        Theme.Configure(_settings.Appearance, _settings.Accent);
        var previousIcon = _brandIcon;
        _brandIcon = BrandIcon.Create();
        Icon = _brandIcon;
        previousIcon?.Dispose();
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        Theme.ApplyWindowFrame(this);
        ApplyColorsRecursive(this);
        _syntaxPainter.Schedule();
        UpdateVisualButtons();
        Invalidate(true);
    }

    private static void ApplyColorsRecursive(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child is StudioButton b) b.Invalidate();
            else if (child is StudioProgress || child is EditorGutter) child.Invalidate();
            else if (child is StudioCard card)
            {
                card.BackColor = Theme.Surface;
                card.Invalidate();
            }
            else if (child is ComboBox combo)
            {
                combo.BackColor = Theme.SurfaceRaised;
                combo.ForeColor = Theme.Foreground;
            }
            else if (child is RichTextBox rich)
            {
                rich.BackColor = Theme.Input;
                rich.ForeColor = Theme.Foreground;
            }
            else if (child is TextBox box)
            {
                box.BackColor = Theme.SurfaceRaised;
                box.ForeColor = Theme.Foreground;
            }
            else if (child is Label label)
            {
                switch (label.Tag as string)
                {
                    case "accent": label.ForeColor = Theme.Accent; break;
                    case "muted": label.ForeColor = Theme.Muted; break;
                    case "success": label.ForeColor = Theme.Success; break;
                    case "danger": label.ForeColor = Theme.Danger; break;
                    case "warning": label.ForeColor = Theme.Warning; break;
                    case "logo": label.ForeColor = Theme.AccentText; label.BackColor = Theme.AccentFill; break;
                    default: label.ForeColor = Theme.Foreground; break;
                }
            }
            else if (child is Panel panel)
            {
                panel.BackColor = (panel.Tag as string) switch
                {
                    "surface" => Theme.Surface, "border" => Theme.Border,
                    "input" => Theme.Input, "raised" => Theme.SurfaceRaised,
                    _ => Theme.Background
                };
            }
            else if (child is TableLayoutPanel or FlowLayoutPanel)
            {
                child.BackColor = (child.Tag as string) switch
                {
                    "surface" => Theme.Surface, "input" => Theme.Input,
                    _ => Theme.Background
                };
            }
            ApplyColorsRecursive(child);
        }
    }

    private void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { /* Window is closing. */ }
    }

    private void SetStatus(string value)
    {
        _status.Text = "●   " + value;
        _tips.SetToolTip(_status, value);
        var negative = value.Contains("ошиб", StringComparison.OrdinalIgnoreCase) ||
                       value.Contains("не удалось", StringComparison.OrdinalIgnoreCase) ||
                       value.Contains("отклон", StringComparison.OrdinalIgnoreCase);
        _status.Tag = negative ? "danger" : "success";
        _status.ForeColor = negative ? Theme.Danger : Theme.Success;
    }

    private void UpdateStats()
    {
        if (_editor is null || _wordCount is null) return;
        var content = _editor.Text;
        var words = 0;
        var inside = false;
        foreach (var c in content)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (!inside) words++;
                inside = true;
            }
            else inside = false;
        }
        _stats.Text = $"{_editor.TextLength:N0} символов";
        _wordCount.Text = $"{words:N0} слов";
        _languagePicker.Visible = _settings.Mode == TypingMode.Code;
        _language.Text = _settings.Mode == TypingMode.Text
            ? "TEXT  /  " + (_settings.GetProfile().TextEnter == TextEnterMode.ShiftEnter
                ? "Shift+Enter" : "Enter")
            : "CODE  /  " + CodeAnalyzer.Name(CodeAnalyzer.Detect(_settings.SourceFileName,
                content, Enum.TryParse<CodeAnalyzer.Language>(_settings.CodeLanguage,
                    out var selected) ? selected : CodeAnalyzer.Language.Auto));
        _pageTitle.Text = _settings.Mode == TypingMode.Text ? "Текстовый редактор" : "Рабочее поле кода";
        _pageSubtitle.Text = _settings.Mode == TypingMode.Text
            ? "Естественный набор текста с вашим стилем поведения"
            : "Исходный код, сохранение структуры и интеграция с IDE";
        _syntaxPainter.IsCode = _settings.Mode == TypingMode.Code;
        _syntaxPainter.FileName = _settings.SourceFileName;
        _syntaxPainter.Language = _settings.CodeLanguage;
        _syntaxPainter.Enabled = _settings.SyntaxHighlighting;
        _syntaxPainter.Schedule();
        UpdateVisualButtons();
        UpdateDocumentChrome();
        UpdateCaret();
        UpdateEditorViewButtons();
    }

    private void RefreshProfiles()
    {
        _registering = true;
        _profiles.Items.Clear();
        foreach (var profile in _settings.Profiles) _profiles.Items.Add(profile.Name);
        _profiles.SelectedItem = _settings.SelectedProfile;
        if (_profiles.SelectedIndex < 0 && _profiles.Items.Count > 0) _profiles.SelectedIndex = 0;
        _mode.SelectedIndex = (int)_settings.Mode;
        _languagePicker.SelectedItem = _settings.CodeLanguage;
        _registering = false;
        _hotkeys.Text = $"Старт: {Native.HotkeyText(_settings.StartHotkey)}    " +
            $"Пауза: {Native.HotkeyText(_settings.PauseHotkey)}    Стоп: {Native.HotkeyText(_settings.StopHotkey)}";
    }

    private void UpdateButtons()
    {
        var running = _engine.IsRunning;
        _start.Enabled = !running && !_countingDown && !_ideBusy && !_checkingTarget && _editor.TextLength > 0;
        _diagnostics.Enabled = !running && !_countingDown && !_ideBusy && !_checkingTarget;
        _sessionReportButton.Enabled = _lastSessionReport is not null && !running && !_countingDown && !_ideBusy;
        _pause.Enabled = running;
        _stop.Enabled = running || _countingDown || _ideBusy || _checkingTarget;
        _pause.Text = _engine.IsPaused ? "▶    Продолжить" : "Ⅱ    Пауза";
        _mode.Enabled = !running && !_countingDown && !_ideBusy;
        _profiles.Enabled = !running && !_countingDown && !_ideBusy;
        _textModeButton.Enabled = !running && !_countingDown && !_ideBusy;
        _codeModeButton.Enabled = !running && !_countingDown && !_ideBusy;
        _saveSource.Enabled = !running && !_countingDown && !_ideBusy && _editor.TextLength > 0;
        _languagePicker.Enabled = !running && !_ideBusy;
        _editor.ReadOnly = running || _ideBusy;
        _findField.Enabled = !running && !_ideBusy;
    }

    private void OpenFile()
    {
        if (_engine.IsRunning || _countingDown || _ideBusy)
        {
            SetStatus("Остановите набор перед импортом.");
            return;
        }
        using var dialog = new OpenFileDialog
        {
            Title = "Открыть текст или код",
            Filter = "Текст и код|*.txt;*.cs;*.py;*.cpp;*.h;*.hpp;*.js;*.ts;*.java;*.go;*.rs;*.html;*.css;*.json;*.xml;*.sql;*.md;*.sh;*.ps1|Все файлы|*.*",
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            OpenDocument(dialog.FileName);
    }

    private void OpenSettings()
    {
        if (_engine.IsRunning || _countingDown || _ideBusy)
        {
            SetStatus("Сначала остановите текущий набор.");
            return;
        }
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        var previous = _settings;
        _settings = form.Result;
        if (!RegisterHotkeys(showWarnings: false))
        {
            _settings = previous;
            // Transactional registrar already restored the prior set if possible.
            MessageBox.Show(this, "Не удалось зарегистрировать одну из комбинаций. Возможно, она уже используется другой программой. Настройки не изменены.",
                "KeyER — горячие клавиши", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            PersistSettings();
        }
        RefreshProfiles();
        TopMost = _settings.AlwaysOnTop;
        ApplyCompactLayout();
        RefreshEmptyState();
        _editor.Font = new Font("Cascadia Code", _settings.EditorFontSize);
        ApplyAppearance();
        UpdateStats();
    }

    private async Task CheckTargetAsync()
    {
        if (_engine.IsRunning || _countingDown || _ideBusy || _checkingTarget) return;
        _checkingTarget = true;
        _countdownCts = new CancellationTokenSource();
        UpdateButtons();
        try
        {
            for (var remaining = 3; remaining > 0; remaining--)
            {
                SetStatus($"Проверка окна через {remaining} сек. Перейдите в редактор и установите курсор.");
                await Task.Delay(1000, _countdownCts.Token);
            }
            _countdownCts.Token.ThrowIfCancellationRequested();
            var report = TargetCompatibility.Inspect(Native.GetForegroundWindow());
            _checkingTarget = false;
            UpdateButtons();
            SetStatus(report.CanType ? "Окно готово к попытке ввода. Проверьте фокус курсора."
                                    : report.Summary);
            using var form = new TargetDiagnosticsForm(report);
            form.ShowDialog(this);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Проверка совместимости отменена.");
        }
        finally
        {
            _checkingTarget = false;
            _countdownCts?.Dispose();
            _countdownCts = null;
            UpdateButtons();
        }
    }

    private async Task StartWithCountdownAsync()
    {
        if (_engine.IsRunning || _countingDown || _ideBusy || _checkingTarget) return;
        _countdownCts = new CancellationTokenSource();
        _countingDown = true;
        UpdateButtons();
        try
        {
            var seconds = _settings.GetProfile().StartCountdownSeconds;
            for (var remaining = seconds; remaining > 0; remaining--)
            {
                SetStatus($"Переключитесь в нужное окно и установите курсор — старт через {remaining}...");
                await Task.Delay(1000, _countdownCts.Token);
            }
            _countdownCts.Token.ThrowIfCancellationRequested();
            await StartNowAsync(Native.GetForegroundWindow(), _countdownCts.Token);
        }
        catch (OperationCanceledException) { SetStatus("Запуск отменён."); }
        finally
        {
            _countingDown = false;
            _countdownCts.Dispose();
            _countdownCts = null;
            UpdateButtons();
        }
    }

    private async Task StartNowAsync(IntPtr target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_engine.IsRunning || _ideBusy || _checkingTarget) return;
        if (string.IsNullOrEmpty(_editor.Text))
        {
            SetStatus("Сначала вставьте или откройте текст.");
            return;
        }
        var readiness = TargetCompatibility.Inspect(target);
        if (!readiness.CanType)
        {
            SetStatus(readiness.Summary + " " + readiness.Guidance);
            return;
        }

        _ideBusy = true;
        _idePreflight = new CancellationTokenSource();
        _progress.Value = 0;
        _progressPercent.Text = "0%";
        UpdateButtons();
        var snapshot = _editor.Text;
        var expected = snapshot.Replace("\r\n", "\n").Replace('\r', '\n');
        var profile = _settings.GetProfile().Clone();

        try
        {
            if (_settings.Mode == TypingMode.Code && profile.CodeIntegrity != CodeIntegrityMode.Off)
            {
                var process = Native.GetWindowProcessName(target) ?? "";
                if (process.Equals("Code", StringComparison.OrdinalIgnoreCase) ||
                    process.StartsWith("Code - Insiders", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_ideBridge.IsConnected)
                    {
                        SetStatus("VS Code: установите расширение KeyER IDE и перезапустите редактор. " +
                            "Или отключите проверку IDE в профиле.");
                        return;
                    }

                    var preflight = await _ideBridge.RequestAsync("prepare",
                        new { sourceLength = expected.Length }, _idePreflight.Token);
                    if (!preflight.GetProperty("ok").GetBoolean())
                    {
                        SetStatus("VS Code: " + BridgeError(preflight));
                        return;
                    }
                    _vsCodeSessionId = preflight.GetProperty("sessionId").GetString();
                    SetStatus("VS Code: пустой редактор подтверждён; запускаю проверяемый набор.");
                }
                else if (process.Equals("devenv", StringComparison.OrdinalIgnoreCase))
                {
                    if (!VisualStudioDteSession.TryPrepare(target,
                        out _visualStudioSession, out var diagnostic))
                    {
                        SetStatus(diagnostic);
                        return;
                    }
                    _visualStudioMonitor.Start();
                }
                else
                {
                    SetStatus("Проверка IDE работает только в VS Code и Visual Studio. " +
                        "Для других программ отключите её в профиле.");
                    return;
                }

                if (Native.GetForegroundWindow() != target)
                {
                    SetStatus("Активное окно изменилось до начала набора.");
                    return;
                }
            }

            // Stop can arrive while an IDE preflight is awaiting a reply.
            // Recheck both cancellation sources at the final hand-off: once
            // keyboard input begins it must never revive a cancelled attempt.
            cancellationToken.ThrowIfCancellationRequested();
            _idePreflight.Token.ThrowIfCancellationRequested();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var activeTask = _engine.StartAsync(snapshot, _settings.Mode, profile, target,
                _settings.SourceFileName, _settings.CodeLanguage);
            UpdateButtons();
            await activeTask;
            stopwatch.Stop();
            _lastSessionReport = TypingSessionReport.Create(
                _engine.LastOutcome, _engine.LastProcessedCharacters,
                _engine.LastTotalCharacters, stopwatch.Elapsed, readiness.ProcessLabel);
            _tips.SetToolTip(_sessionReportButton, _lastSessionReport.Title +
                ". Нажмите для подробностей и безопасного восстановления.");
            UpdateButtons();

            _visualStudioMonitor.Stop();
            if (_vsCodeSessionId is not null)
            {
                var sessionId = _vsCodeSessionId;
                if (_engine.LastOutcome == TypingOutcome.Completed)
                {
                    SetStatus("VS Code: сравниваю результат с исходным кодом...");
                    try
                    {
                        var result = await _ideBridge.RequestAsync("complete",
                            new { sessionId, source = expected,
                                strategy = profile.CodeIntegrity == CodeIntegrityMode.VerifyAndRepair
                                    ? "repair" : "verify" }, timeoutMilliseconds: 12000);
                        if (!result.GetProperty("ok").GetBoolean())
                            SetStatus("VS Code: " + BridgeError(result));
                        else if (result.GetProperty("matched").GetBoolean())
                            SetStatus(result.GetProperty("repaired").GetBoolean()
                                ? "VS Code: код исправлен и совпадает с исходным."
                                : "VS Code: код полностью совпадает с исходным.");
                        else
                            SetStatus("VS Code: код отличается от исходника. " +
                                "Выберите разрешённое исправление в настройках профиля.");
                    }
                    catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
                    {
                        DiagnosticLog.Error("vscode-complete", ex);
                        SetStatus("VS Code: не удалось завершить сверку. Проверьте редактор вручную.");
                    }
                }
                else
                {
                    try { await _ideBridge.RequestAsync("cancel", new { sessionId },
                        timeoutMilliseconds: 1200); }
                    catch { /* Extension may already have invalidated the session. */ }
                }
            }
            else if (_visualStudioSession is not null &&
                     _engine.LastOutcome == TypingOutcome.Completed)
            {
                var (matched, repaired, message) =
                    _visualStudioSession.Finish(expected, profile.CodeIntegrity);
                SetStatus(message);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Запуск отменён.");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ide-start", ex);
            SetStatus("Не удалось начать набор: " + ex.Message);
        }
        finally
        {
            _visualStudioMonitor.Stop();
            _vsCodeSessionId = null;
            _visualStudioSession = null;
            _ideBusy = false;
            _idePreflight.Dispose();
            _idePreflight = null;
            UpdateButtons();
        }
    }

    private static string BridgeError(JsonElement response) =>
        response.TryGetProperty("error", out var error)
            ? error.GetString() ?? "Неизвестная ошибка редактора."
            : "Неизвестная ошибка редактора.";

    private void StopTyping()
    {
        _countdownCts?.Cancel();
        _idePreflight?.Cancel();
        _engine.Stop();
        UpdateButtons();
    }

    private void PersistSettings()
    {
        try { SettingsStore.Save(_settings); }
        catch (Exception ex) { SetStatus("Не удалось сохранить настройки: " + ex.Message); }
    }

    private void UnregisterHotkeys()
    {
        _hotkeyCoordinator?.Dispose();
        _hotkeyCoordinator = null;
    }

    private bool RegisterHotkeys(bool showWarnings)
    {
        if (!IsHandleCreated) return false;
        _hotkeyCoordinator ??= new HotkeyRegistrationCoordinator(
            new Win32HotkeyRegistrationBackend(), Handle);
        var registered = _hotkeyCoordinator.TryApply(
            _settings.StartHotkey, _settings.PauseHotkey, _settings.StopHotkey);
        if (registered) return true;
        if (showWarnings)
            MessageBox.Show(this,
                "Не удалось зарегистрировать одну из глобальных комбинаций.\n" +
                "Проверьте, что они не используются другой программой, затем откройте настройки.\n" +
                (_hotkeyCoordinator.HasActiveSet
                    ? "Предыдущие работающие сочетания восстановлены."
                    : "Регистрация сочетаний не выполнена."),
                "KeyER — горячие клавиши", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case 1:
                    if (!_countingDown && !_ideBusy) _ = StartNowAsync(Native.GetForegroundWindow());
                    break;
                case 2:
                    _engine.TogglePause();
                    UpdateButtons();
                    break;
                case 3:
                    StopTyping();
                    break;
            }
            return;
        }
        base.WndProc(ref m);
    }
}
