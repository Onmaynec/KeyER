namespace KeyER;

public sealed partial class MainForm
{
    private bool CanReplaceSource()
    {
        if (!_modified || _editor.TextLength == 0) return true;
        return StudioDialog.Confirm(this, "Заменить исходник?",
            "В редакторе есть несохранённые изменения.\n\n" +
            "При открытии другого файла текущий текст будет заменён. " +
            "Нажмите «Отмена», чтобы сохранить его через Ctrl+Shift+S.");
    }

    private void ClearSource()
    {
        if (_editor.ReadOnly || _engine.IsRunning || _countingDown || _ideBusy) return;
        if (_editor.TextLength == 0) return;
        if (!StudioDialog.Confirm(this, "Очистить редактор?",
            "Будет удалён весь текущий текст из редактора.\n\n" +
            "Это действие нельзя автоматически отменить после очистки. " +
            "Вы можете сначала сохранить исходник в файл."))
            return;

        _editingBlank = false;
        _editor.Clear();
        RefreshEmptyState();
        ResetSourceProgress();
        _openedFilePath = null;
        _settings.SourceFileName = "";
        _modified = false;
        UpdateStats();
        SetStatus("Редактор очищен. Добавьте текст или откройте файл.");
    }

    private void ResetSourceProgress()
    {
        if (_progress is null || _progressPercent is null) return;
        _progress.Value = 0;
        _progressPercent.Text = "0%";
        // The previous post-run report remains accessible on demand.
    }

    private void SaveCurrentSource()
    {
        if (_editor.ReadOnly || _editor.TextLength == 0 ||
            _engine.IsRunning || _countingDown || _ideBusy) return;
        if (string.IsNullOrWhiteSpace(_openedFilePath))
        {
            SaveSourceAs();
            return;
        }

        try
        {
            AtomicDocumentWriter.WriteText(_openedFilePath, _editor.Text);
            _modified = false;
            UpdateDocumentChrome();
            SetStatus("Изменения сохранены в текущий файл.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException)
        {
            DiagnosticLog.Error("document-save", ex);
            SetStatus("Не удалось сохранить текущий файл. Проверьте папку и права доступа.");
        }
    }

    private void SaveSourceAs()
    {
        if (_editor.TextLength == 0 || _engine.IsRunning || _countingDown || _ideBusy) return;
        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить исходник как...",
            Filter = "Текстовый файл (*.txt)|*.txt|Исходный файл (*.cs;*.py;*.js;*.ts;*.cpp)|*.cs;*.py;*.js;*.ts;*.cpp|Все файлы (*.*)|*.*",
            InitialDirectory = _openedFilePath is null
                ? "" : Path.GetDirectoryName(_openedFilePath) ?? "",
            FileName = string.IsNullOrWhiteSpace(_settings.SourceFileName)
                ? (_settings.Mode == TypingMode.Code ? "snippet.cs" : "text.txt")
                : _settings.SourceFileName,
            AddExtension = true,
            DefaultExt = _settings.Mode == TypingMode.Code ? "cs" : "txt",
            OverwritePrompt = true,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            // Never autosave snippets. Only the user's explicit Save As writes code.
            AtomicDocumentWriter.WriteText(dialog.FileName, _editor.Text);
            _openedFilePath = dialog.FileName;
            _settings.SourceFileName = Path.GetFileName(dialog.FileName);
            _modified = false;
            UpdateStats();
            SetStatus("Файл сохранён. Исходник остаётся открытым в редакторе.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException)
        {
            DiagnosticLog.Error("document-save", ex);
            SetStatus("Не удалось сохранить исходник. Проверьте папку и права доступа.");
        }
    }

    private void OpenDocument(string path)
    {
        if (_engine.IsRunning || _countingDown || _ideBusy) return;
        if (!File.Exists(path)) { SetStatus("Указанный файл не найден."); return; }

        try
        {
            var bytes = new FileInfo(path).Length;
            if (bytes > 10_000_000)
            {
                SetStatus("Файл больше 10 МБ. Для безопасности откройте меньший фрагмент.");
                return;
            }
            if (!CanReplaceSource()) return;
            if (bytes > 2_000_000 &&
                !StudioDialog.Confirm(this, "Большой документ",
                    "Файл больше 2 МБ. Подсветка и набор могут работать медленно.\n\nОткрыть его?"))
                return;

            // Read before editing the current buffer so a failed read leaves it intact.
            var text = File.ReadAllText(path);
            if (text.Contains('\0'))
            {
                SetStatus("Это похоже на двоичный файл, а не на исходный текст.");
                return;
            }
            _importing = true;
            try { _editor.Text = text; }
            finally { _importing = false; }
            _editingBlank = text.Length > 0;
            RefreshEmptyState();

            _openedFilePath = path;
            _settings.SourceFileName = Path.GetFileName(path);
            _modified = false;
            ResetSourceProgress();
            UpdateStats();
            _editor.SelectionStart = 0;
            _editor.ScrollToCaret();
            SetStatus("Открыт файл: " + _settings.SourceFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException)
        {
            DiagnosticLog.Error("document-open", ex);
            SetStatus("Не удалось открыть текстовый файл. Проверьте кодировку и права доступа.");
        }
    }

    private void FileDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true &&
            !_engine.IsRunning && !_ideBusy && !_countingDown &&
            e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths &&
            File.Exists(paths[0]))
        {
            e.Effect = DragDropEffects.Copy;
            return;
        }
        e.Effect = DragDropEffects.None;
    }

    private void FileDropped(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths)
            OpenDocument(paths[0]);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Shift | Keys.S))
        {
            SaveSourceAs();
            return true;
        }
        if (keyData == (Keys.Control | Keys.S))
        {
            SaveCurrentSource();
            return true;
        }
        if (keyData == (Keys.Control | Keys.O))
        {
            OpenFile();
            return true;
        }
        if (keyData == Keys.F11)
        {
            ToggleFocusMode();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.Z))
        {
            ToggleWordWrap();
            return true;
        }
        if (keyData == (Keys.Control | Keys.F))
        {
            ToggleFind(true);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void UpdateDocumentChrome()
    {
        if (_sourceBadge is null || _profileDetails is null || _readyStep is null) return;

        var name = _editor.TextLength == 0 || string.IsNullOrWhiteSpace(_settings.SourceFileName)
            ? "НОВЫЙ ФРАГМЕНТ" : _settings.SourceFileName;
        _sourceBadge.Text = (_modified ? "●  " : "◉  ") + name;
        _sourceBadge.ForeColor = _modified ? Theme.Warning : Theme.Accent;
        _sourceBadge.Tag = _modified ? "warning" : "accent";
        _tips.SetToolTip(_sourceBadge, _modified
            ? "Исходник изменён. Ctrl+Shift+S — сохранить как файл."
            : "Редактор не изменяет файл на диске без явного сохранения.");

        var profile = _settings.GetProfile();
        _profileDetails.Text = $"{profile.CharactersPerMinute} сим/мин  ·  {profile.TypoPercent:0.#}% ошибок";

        var ready = _editor.TextLength > 0;
        _readyStep.Text = ready ? "✓  1. Исходный текст готов" : "○  1. Добавьте исходный текст";
        _readyStep.ForeColor = ready ? Theme.Success : Theme.Accent;
        _readyStep.Tag = ready ? "success" : "accent";

        if (_estimateLabel is not null)
        {
            var seconds = ready && profile.CharactersPerMinute > 0
                ? (int)Math.Ceiling(60.0 * _editor.TextLength / profile.CharactersPerMinute)
                : 0;
            _estimateLabel.Text = ready
                ? $"Базовое время: ≈ {FormatDuration(seconds)}"
                : "Добавьте исходник, чтобы начать";
            _tips.SetToolTip(_estimateLabel,
                "Приблизительное время без случайных пауз, задержек и исправлений.");
        }
    }

    private static string FormatDuration(int seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 1_000_000));
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours} ч {span.Minutes} мин"
             : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} мин {span.Seconds} с"
             : $"{span.Seconds} с";
    }
}
