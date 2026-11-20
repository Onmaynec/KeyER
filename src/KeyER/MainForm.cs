using System.Drawing;
using System.Text.Json;

namespace KeyER;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(18, 22, 31);
    public static readonly Color Surface = Color.FromArgb(27, 33, 46);
    public static readonly Color Input = Color.FromArgb(21, 26, 37);
    public static readonly Color Foreground = Color.FromArgb(231, 237, 246);
    public static readonly Color Muted = Color.FromArgb(151, 165, 187);
    public static readonly Color Accent = Color.FromArgb(108, 154, 255);
    public static readonly Color Danger = Color.FromArgb(232, 112, 125);

    public static void StyleButton(Button b, bool primary = false, bool danger = false)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = danger ? Danger : primary ? Accent : Surface;
        b.ForeColor = primary || danger ? Color.FromArgb(14, 19, 28) : Foreground;
        b.Font = new Font("Segoe UI", 9F, primary ? FontStyle.Bold : FontStyle.Regular);
        b.Cursor = Cursors.Hand;
        b.Margin = new Padding(4);
    }

    public static void StyleCombo(ComboBox b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.DropDownStyle = ComboBoxStyle.DropDownList;
        b.BackColor = Surface;
        b.ForeColor = Foreground;
        b.Font = new Font("Segoe UI", 9.5F);
        b.Margin = new Padding(4, 7, 4, 4);
    }

    public static Label Label(string text, float size = 9, bool bold = false)
    {
        return new Label
        {
            Text = text, ForeColor = Foreground, BackColor = Color.Transparent,
            AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular)
        };
    }
}

public sealed class MainForm : Form
{
    private AppSettings _settings;
    private readonly TypingEngine _engine = new();
    private readonly IdeBridgeServer _ideBridge = new();
    private readonly System.Windows.Forms.Timer _visualStudioMonitor = new() { Interval = 300 };
    private VisualStudioDteSession? _visualStudioSession;
    private string? _vsCodeSessionId;
    private bool _ideBusy;
    private CancellationTokenSource? _idePreflight;
    private readonly TextBox _editor;
    private readonly ComboBox _mode;
    private readonly ComboBox _profiles;
    private readonly ComboBox _languagePicker;
    private readonly Label _language;
    private readonly Label _stats;
    private readonly Label _status;
    private readonly Label _hotkeys;
    private readonly ProgressBar _progress;
    private readonly Button _start;
    private readonly Button _pause;
    private readonly Button _stop;
    private bool _registering;
    private bool _countingDown;
    private CancellationTokenSource? _countdownCts;

    public MainForm()
    {
        _settings = SettingsStore.Load();
        Text = "KeyER · Keyboard Emulator";
        MinimumSize = new Size(480, 400);
        Size = new Size(680, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        Font = new Font("Segoe UI", 9.5F);

        var header = new Panel { Dock = DockStyle.Top, Height = 77, BackColor = Theme.Background, Padding = new Padding(16, 12, 12, 4) };
        var brand = Theme.Label("KeyER", 19F, true);
        brand.ForeColor = Theme.Accent;
        brand.Location = new Point(16, 12);
        var tagline = Theme.Label("Human-like typing  /  Text & Code", 8F);
        tagline.ForeColor = Theme.Muted;
        tagline.Location = new Point(18, 48);
        header.Controls.AddRange([brand, tagline]);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 55, BackColor = Theme.Background,
            Padding = new Padding(10, 3, 8, 1), WrapContents = false, AutoScroll = true
        };
        _mode = new ComboBox { Width = 92, Height = 33 };
        Theme.StyleCombo(_mode);
        _mode.Items.AddRange(["Text", "Code"]);
        _mode.SelectedIndex = (int)_settings.Mode;
        _mode.SelectedIndexChanged += (_, _) =>
        {
            if (_registering) return;
            _settings.Mode = (TypingMode)_mode.SelectedIndex;
            UpdateStats();
            PersistSettings();
        };

        _profiles = new ComboBox { Width = 164, Height = 33 };
        Theme.StyleCombo(_profiles);
        _profiles.SelectedIndexChanged += (_, _) =>
        {
            if (_registering || _profiles.SelectedItem is not string value) return;
            _settings.SelectedProfile = value;
            UpdateStats();
            PersistSettings();
        };

        var paste = new Button { Text = "Вставить", Width = 91, Height = 34 };
        var open = new Button { Text = "Файл", Width = 77, Height = 34 };
        var settingsButton = new Button { Text = "Настройки", Width = 112, Height = 34 };
        foreach (var b in new[] { paste, open, settingsButton }) Theme.StyleButton(b);
        paste.Click += (_, _) =>
        {
            try
            {
                if (Clipboard.ContainsText() && _editor is not null) _editor.SelectedText = Clipboard.GetText();
                else SetStatus("В буфере обмена нет текста.");
            }
            catch (Exception ex) { SetStatus("Не удалось вставить: " + ex.Message); }
        };
        open.Click += (_, _) => OpenFile();
        settingsButton.Click += (_, _) => OpenSettings();
        toolbar.Controls.AddRange([_mode, _profiles, paste, open, settingsButton]);

        var strip = new Panel { Dock = DockStyle.Top, Height = 39, BackColor = Theme.Background };
        _language = Theme.Label("Code", 8.5F);
        _language.ForeColor = Theme.Muted;
        _language.Location = new Point(17, 10);
        strip.Controls.Add(_language);

        _languagePicker = new ComboBox { Width = 128, Height = 28, Left = 195, Top = 3 };
        Theme.StyleCombo(_languagePicker);
        _languagePicker.Margin = Padding.Empty;
        _languagePicker.Items.AddRange(Enum.GetNames<CodeAnalyzer.Language>());
        _languagePicker.SelectedItem = _settings.CodeLanguage;
        if (_languagePicker.SelectedIndex < 0) _languagePicker.SelectedIndex = 0;
        _languagePicker.SelectedIndexChanged += (_, _) =>
        {
            if (_registering || _languagePicker.SelectedItem is not string value) return;
            _settings.CodeLanguage = value;
            UpdateStats();
            PersistSettings();
        };
        strip.Controls.Add(_languagePicker);
        _stats = Theme.Label("0 символов", 8.5F);
        _stats.ForeColor = Theme.Muted;
        _stats.AutoSize = false;
        _stats.TextAlign = ContentAlignment.MiddleRight;
        _stats.Dock = DockStyle.Right;
        _stats.Width = 170;
        strip.Controls.Add(_stats);

        var editorFrame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(2), Margin = new Padding(12) };
        var editorHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(13), BackColor = Theme.Input };
        _editor = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, AcceptsTab = true,
            ScrollBars = ScrollBars.Both, WordWrap = false, BorderStyle = BorderStyle.None,
            Font = new Font("Cascadia Code", 11F, FontStyle.Regular),
            BackColor = Theme.Input, ForeColor = Theme.Foreground, ShortcutsEnabled = true
        };
        _editor.TextChanged += (_, _) =>
        {
            _settings.SourceFileName = ""; // pasted/edited snippet may no longer match imported file
            UpdateStats();
        };
        editorHost.Controls.Add(_editor);
        editorFrame.Controls.Add(editorHost);

        var editorPadding = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 0, 14, 4), BackColor = Theme.Background };
        editorPadding.Controls.Add(editorFrame);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 121, BackColor = Theme.Background, Padding = new Padding(14, 7, 14, 9) };
        _progress = new ProgressBar { Dock = DockStyle.Top, Height = 5, Style = ProgressBarStyle.Continuous, Maximum = 1000 };
        _status = Theme.Label("Готово к работе", 8.5F);
        _status.ForeColor = Theme.Muted;
        _status.AutoSize = false;
        _status.Dock = DockStyle.Top;
        _status.Height = 27;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 43, WrapContents = false, AutoScroll = true,
            BackColor = Theme.Background, Padding = new Padding(0, 2, 0, 0)
        };
        _start = new Button { Text = "▶  Начать", Width = 122, Height = 34 };
        _pause = new Button { Text = "Ⅱ  Пауза", Width = 105, Height = 34 };
        _stop = new Button { Text = "■  Стоп", Width = 94, Height = 34 };
        Theme.StyleButton(_start, primary: true);
        Theme.StyleButton(_pause);
        Theme.StyleButton(_stop, danger: true);
        _start.Click += async (_, _) => await StartWithCountdownAsync();
        _pause.Click += (_, _) => { _engine.TogglePause(); UpdateButtons(); };
        _stop.Click += (_, _) => StopTyping();
        actionRow.Controls.AddRange([_start, _pause, _stop]);

        _hotkeys = Theme.Label("", 8F);
        _hotkeys.ForeColor = Theme.Muted;
        _hotkeys.AutoSize = false;
        _hotkeys.Dock = DockStyle.Bottom;
        _hotkeys.Height = 20;
        footer.Controls.AddRange([_hotkeys, actionRow, _status, _progress]);
        // Reverse dock-order is not relied upon for the action controls:
        footer.Controls.SetChildIndex(_progress, 0);
        footer.Controls.SetChildIndex(_status, 1);
        footer.Controls.SetChildIndex(actionRow, 2);

        Controls.AddRange([editorPadding, footer, strip, toolbar, header]);
        RefreshProfiles();
        UpdateStats();
        UpdateButtons();
        _engine.StatusChanged += message => Ui(() => SetStatus(message));
        _engine.ProgressChanged += (completed, total) => Ui(() =>
            _progress.Value = Math.Clamp(total == 0 ? 0 : (int)((long)completed * 1000 / total), 0, 1000));

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
            RegisterHotkeys(showWarnings: true);
            _ideBridge.Start();
        };
        FormClosing += (_, _) =>
        {
            _idePreflight?.Cancel();
            _countdownCts?.Cancel();
            _engine.Stop();
            _visualStudioMonitor.Stop();
            _visualStudioMonitor.Dispose();
            _ideBridge.Dispose();
            UnregisterHotkeys();
        };
    }

    private void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { /* Window is closing. */ }
    }

    private void SetStatus(string value) => _status.Text = value;

    private void UpdateStats()
    {
        _stats.Text = $"{_editor.TextLength:N0} символов · {_editor.Lines.Length:N0} строк";
        var profile = _settings.GetProfile();
        _languagePicker.Visible = _settings.Mode == TypingMode.Code;
        _language.Text = _settings.Mode == TypingMode.Text
            ? "Text · " + (profile.TextEnter == TextEnterMode.ShiftEnter ? "Shift+Enter" : "Enter")
                + " · " + (profile.TextTab == TextTabMode.Spaces ? $"Tab = {profile.TabSpaces} пробелов" : "Tab")
            : "Code · " + CodeAnalyzer.Name(CodeAnalyzer.Detect(_settings.SourceFileName,
                _editor.Text, Enum.TryParse<CodeAnalyzer.Language>(_settings.CodeLanguage,
                    out var selected) ? selected : CodeAnalyzer.Language.Auto));
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
        _start.Enabled = !running && !_countingDown && !_ideBusy;
        _pause.Enabled = running;
        _stop.Enabled = running || _countingDown || _ideBusy;
        _pause.Text = _engine.IsPaused ? "▶  Продолжить" : "Ⅱ  Пауза";
        _mode.Enabled = !running && !_ideBusy;
        _profiles.Enabled = !running && !_ideBusy;
        _languagePicker.Enabled = !running && !_ideBusy;
        _editor.ReadOnly = running || _ideBusy;
    }

    private void OpenFile()
    {
        if (_engine.IsRunning) { SetStatus("Остановите набор перед импортом."); return; }
        using var dialog = new OpenFileDialog
        {
            Title = "Открыть текст или код",
            Filter = "Текст и код|*.txt;*.cs;*.py;*.cpp;*.h;*.hpp;*.js;*.ts;*.java;*.go;*.rs;*.html;*.css;*.json;*.xml;*.sql;*.md;*.sh;*.ps1|Все файлы|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var size = new FileInfo(dialog.FileName).Length;
            if (size > 2_000_000 &&
                MessageBox.Show(this, "Файл больше 2 МБ. Открыть? Набор может быть очень долгим.",
                    "KeyER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _editor.Text = File.ReadAllText(dialog.FileName);
            _settings.SourceFileName = Path.GetFileName(dialog.FileName);
            UpdateStats();
            SetStatus($"Загружен файл: {_settings.SourceFileName}");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ошибка чтения", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
            RegisterHotkeys(showWarnings: false);
            MessageBox.Show(this, "Не удалось зарегистрировать одну из комбинаций. Возможно, она уже используется другой программой. Настройки не изменены.",
                "KeyER — горячие клавиши", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            PersistSettings();
        }
        RefreshProfiles();
        UpdateStats();
    }

    private async Task StartWithCountdownAsync()
    {
        if (_engine.IsRunning || _countingDown || _ideBusy) return;
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
            await StartNowAsync(Native.GetForegroundWindow());
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

    private async Task StartNowAsync(IntPtr target)
    {
        if (_engine.IsRunning || _ideBusy) return;
        if (string.IsNullOrEmpty(_editor.Text))
        {
            SetStatus("Сначала вставьте или откройте текст.");
            return;
        }
        if (target == IntPtr.Zero || Native.IsOwnWindow(target))
        {
            SetStatus("Курсор должен быть в окне другого приложения. Набор не запущен.");
            return;
        }

        _ideBusy = true;
        _idePreflight = new CancellationTokenSource();
        _progress.Value = 0;
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

            var activeTask = _engine.StartAsync(snapshot, _settings.Mode, profile, target,
                _settings.SourceFileName, _settings.CodeLanguage);
            UpdateButtons();
            await activeTask;

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
        if (!IsHandleCreated) return;
        for (var id = 1; id <= 3; id++) Native.UnregisterHotKey(Handle, id);
    }

    private bool RegisterHotkeys(bool showWarnings)
    {
        UnregisterHotkeys();
        var ok = Native.TryRegisterHotkey(Handle, 1, _settings.StartHotkey) &&
                 Native.TryRegisterHotkey(Handle, 2, _settings.PauseHotkey) &&
                 Native.TryRegisterHotkey(Handle, 3, _settings.StopHotkey);
        if (ok) return true;
        UnregisterHotkeys();
        if (showWarnings)
            MessageBox.Show(this, "Не удалось зарегистрировать глобальные клавиши. Измените их в настройках.",
                "KeyER", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
