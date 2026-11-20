using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyER;

public enum TypingMode { Text, Code }
public enum EditorType { Generic, VisualStudio, VSCode }
public enum TextEnterMode { Enter, ShiftEnter }
public enum TextTabMode { Tab, Spaces }

public sealed class TypingProfile
{
    public string Name { get; set; } = "Свой";
    public int CharactersPerMinute { get; set; } = 240;
    public int SpeedVariationPercent { get; set; } = 30;
    public decimal TypoPercent { get; set; } = 2.5m;
    public int LateCorrectionPercent { get; set; } = 40;
    public int CorrectionLagCharacters { get; set; } = 4;
    public decimal LongPausePercent { get; set; } = 1.5m;
    public int PauseMinMilliseconds { get; set; } = 350;
    public int PauseMaxMilliseconds { get; set; } = 1400;
    public int FatiguePercent { get; set; } = 10;
    public int BurstPercent { get; set; } = 30;
    public int CursorReviewPercent { get; set; } = 3;
    public EditorType Editor { get; set; } = EditorType.Generic;
    public bool ClearAutoIndent { get; set; } = false;
    public int StartCountdownSeconds { get; set; } = 3;

    // Human Typing Engine 2.0 — applies to Text mode; Code stays independent.
    public int ShortWordSpeedPercent { get; set; } = 110;
    public int LongWordSpeedPercent { get; set; } = 90;
    public int DigitSlowdownPercent { get; set; } = 135;
    public int PunctuationSlowdownPercent { get; set; } = 150;
    public int SpecialSlowdownPercent { get; set; } = 125;
    public int FamiliarPairBoostPercent { get; set; } = 18;
    public int SpeedSmoothingPercent { get; set; } = 28;
    public int SentencePauseMinMilliseconds { get; set; } = 180;
    public int SentencePauseMaxMilliseconds { get; set; } = 430;
    public int ParagraphPauseMinMilliseconds { get; set; } = 180;
    public int ParagraphPauseMaxMilliseconds { get; set; } = 650;
    public int BlockPauseMinMilliseconds { get; set; } = 850;
    public int BlockPauseMaxMilliseconds { get; set; } = 1800;
    public int LateCorrectionMaxWords { get; set; } = 3;
    public int LateCorrectionMaxDistance { get; set; } = 100;
    public int DeleteCorrectionPercent { get; set; } = 25;
    public int SelectionCorrectionPercent { get; set; } = 25;
    public TextEnterMode TextEnter { get; set; } = TextEnterMode.Enter;
    public TextTabMode TextTab { get; set; } = TextTabMode.Tab;
    public int TabSpaces { get; set; } = 4;

    public TypingProfile Clone() =>
        JsonSerializer.Deserialize<TypingProfile>(JsonSerializer.Serialize(this))!;

    public void Validate()
    {
        CharactersPerMinute = Math.Clamp(CharactersPerMinute, 20, 1200);
        SpeedVariationPercent = Math.Clamp(SpeedVariationPercent, 0, 95);
        TypoPercent = Math.Clamp(TypoPercent, 0m, 30m);
        LateCorrectionPercent = Math.Clamp(LateCorrectionPercent, 0, 100);
        CorrectionLagCharacters = Math.Clamp(CorrectionLagCharacters, 0, 12);
        LongPausePercent = Math.Clamp(LongPausePercent, 0m, 20m);
        PauseMinMilliseconds = Math.Clamp(PauseMinMilliseconds, 0, 10000);
        PauseMaxMilliseconds = Math.Clamp(PauseMaxMilliseconds, PauseMinMilliseconds, 20000);
        FatiguePercent = Math.Clamp(FatiguePercent, 0, 100);
        BurstPercent = Math.Clamp(BurstPercent, 0, 80);
        CursorReviewPercent = Math.Clamp(CursorReviewPercent, 0, 20);
        StartCountdownSeconds = Math.Clamp(StartCountdownSeconds, 1, 10);
        ShortWordSpeedPercent = Math.Clamp(ShortWordSpeedPercent, 40, 250);
        LongWordSpeedPercent = Math.Clamp(LongWordSpeedPercent, 40, 250);
        DigitSlowdownPercent = Math.Clamp(DigitSlowdownPercent, 100, 350);
        PunctuationSlowdownPercent = Math.Clamp(PunctuationSlowdownPercent, 100, 350);
        SpecialSlowdownPercent = Math.Clamp(SpecialSlowdownPercent, 100, 350);
        FamiliarPairBoostPercent = Math.Clamp(FamiliarPairBoostPercent, 0, 65);
        SpeedSmoothingPercent = Math.Clamp(SpeedSmoothingPercent, 1, 100);
        SentencePauseMinMilliseconds = Math.Clamp(SentencePauseMinMilliseconds, 0, 10000);
        SentencePauseMaxMilliseconds = Math.Clamp(SentencePauseMaxMilliseconds, SentencePauseMinMilliseconds, 15000);
        ParagraphPauseMinMilliseconds = Math.Clamp(ParagraphPauseMinMilliseconds, 0, 15000);
        ParagraphPauseMaxMilliseconds = Math.Clamp(ParagraphPauseMaxMilliseconds, ParagraphPauseMinMilliseconds, 20000);
        BlockPauseMinMilliseconds = Math.Clamp(BlockPauseMinMilliseconds, 0, 30000);
        BlockPauseMaxMilliseconds = Math.Clamp(BlockPauseMaxMilliseconds, BlockPauseMinMilliseconds, 60000);
        LateCorrectionMaxWords = Math.Clamp(LateCorrectionMaxWords, 1, 6);
        LateCorrectionMaxDistance = Math.Clamp(LateCorrectionMaxDistance, 20, 250);
        DeleteCorrectionPercent = Math.Clamp(DeleteCorrectionPercent, 0, 100);
        SelectionCorrectionPercent = Math.Clamp(SelectionCorrectionPercent, 0, 100 - DeleteCorrectionPercent);
        if (!Enum.IsDefined(TextEnter)) TextEnter = TextEnterMode.Enter;
        if (!Enum.IsDefined(TextTab)) TextTab = TextTabMode.Tab;
        TabSpaces = Math.Clamp(TabSpaces, 1, 8);
        Name = (Name ?? "").Trim();
        if (Name.Length == 0) Name = "Профиль";
    }
}

public sealed class AppSettings
{
    public int SettingsSchemaVersion { get; set; } = 1;
    public string SelectedProfile { get; set; } = "Сбалансированный";
    public TypingMode Mode { get; set; } = TypingMode.Text;
    public string SourceFileName { get; set; } = "";
    public Keys StartHotkey { get; set; } = Keys.Control | Keys.Alt | Keys.F5;
    public Keys PauseHotkey { get; set; } = Keys.Control | Keys.Alt | Keys.F6;
    public Keys StopHotkey { get; set; } = Keys.Control | Keys.Alt | Keys.F7;
    public List<TypingProfile> Profiles { get; set; } = Defaults();

    public static List<TypingProfile> Defaults() =>
    [
        new() { Name = "Спокойный", CharactersPerMinute = 145, SpeedVariationPercent = 32, TypoPercent = 1.0m, LongPausePercent = 2.0m, FatiguePercent = 12 },
        new() { Name = "Сбалансированный", CharactersPerMinute = 240, TypoPercent = 2.5m },
        new() { Name = "Быстрый", CharactersPerMinute = 410, SpeedVariationPercent = 25, TypoPercent = 3.5m, BurstPercent = 40 },
        new() { Name = "Аккуратный код", CharactersPerMinute = 190, TypoPercent = 0.8m, LongPausePercent = 2.0m, Editor = EditorType.VisualStudio },
        new() { Name = "Обычный текст", CharactersPerMinute = 260, TypoPercent = 1.8m,
            ShortWordSpeedPercent = 118, LongWordSpeedPercent = 88, LateCorrectionPercent = 40 },
        new() { Name = "Переписка", CharactersPerMinute = 320, TypoPercent = 2.6m,
            ShortWordSpeedPercent = 125, LongWordSpeedPercent = 95, TextEnter = TextEnterMode.ShiftEnter,
            SentencePauseMinMilliseconds = 90, SentencePauseMaxMilliseconds = 320,
            BlockPauseMinMilliseconds = 550, BlockPauseMaxMilliseconds = 1100 }
    ];

    public TypingProfile GetProfile() =>
        Profiles.FirstOrDefault(x => x.Name == SelectedProfile) ?? Profiles[0];

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;

    public void Validate()
    {
        Profiles ??= Defaults();
        Profiles.RemoveAll(p => p is null);
        if (Profiles.Count == 0) Profiles = Defaults();
        if (SettingsSchemaVersion < 2)
        {
            // Existing 0.1.x installations keep their profiles and get new Text
            // presets exactly once; customized profiles are never overwritten.
            foreach (var template in Defaults().Where(p =>
                p.Name is "Обычный текст" or "Переписка"))
            {
                if (!Profiles.Any(p => string.Equals(p.Name, template.Name,
                    StringComparison.OrdinalIgnoreCase)))
                    Profiles.Add(template);
            }
            SettingsSchemaVersion = 2;
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in Profiles)
        {
            profile.Validate();
            var baseName = profile.Name;
            var suffix = 2;
            while (!seen.Add(profile.Name))
                profile.Name = $"{baseName} ({suffix++})";
        }
        SelectedProfile ??= "";
        SourceFileName ??= "";
        if (Profiles.All(p => p.Name != SelectedProfile)) SelectedProfile = Profiles[0].Name;
        if (!Enum.IsDefined(Mode)) Mode = TypingMode.Text;
        if (!ValidHotkey(StartHotkey)) StartHotkey = Keys.Control | Keys.Alt | Keys.F5;
        if (!ValidHotkey(PauseHotkey)) PauseHotkey = Keys.Control | Keys.Alt | Keys.F6;
        if (!ValidHotkey(StopHotkey)) StopHotkey = Keys.Control | Keys.Alt | Keys.F7;
        if (StartHotkey == PauseHotkey || StartHotkey == StopHotkey || PauseHotkey == StopHotkey)
        {
            StartHotkey = Keys.Control | Keys.Alt | Keys.F5;
            PauseHotkey = Keys.Control | Keys.Alt | Keys.F6;
            StopHotkey = Keys.Control | Keys.Alt | Keys.F7;
        }
    }

    private static bool ValidHotkey(Keys combo)
    {
        var key = combo & Keys.KeyCode;
        var modifiers = combo & Keys.Modifiers;
        if (key is Keys.None or Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
            return false;
        return modifiers != Keys.None || key is >= Keys.F1 and <= Keys.F24;
    }
}

public static class SettingsStore
{
    public static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeyER", "settings.json");

    private static string BackupPath => SettingsPath + ".bak";

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath) && !File.Exists(BackupPath))
            return new AppSettings();

        var original = ReadValid(SettingsPath);
        if (original is not null) return original;

        var backup = ReadValid(BackupPath);
        if (backup is not null)
        {
            MessageBox.Show(
                "Основной файл настроек повреждён или недоступен.\n" +
                "Загружена последняя резервная копия. Сохраните настройки для восстановления.",
                "KeyER — восстановление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return backup;
        }

        MessageBox.Show(
            "Не удалось прочитать настройки KeyER. Загружены стандартные значения.\n" +
            "Повреждённый файл не удалён.",
            "KeyER — настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return new AppSettings();
    }

    private static AppSettings? ReadValid(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            if (settings is null) return null;
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                   or ArgumentException or NotSupportedException)
        {
            DiagnosticLog.Error("settings-load", ex);
            return null;
        }
    }

    public static void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        var temporaryPath = SettingsPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true }));
            // File.Replace is atomic on NTFS and retains the previous settings as a backup.
            if (File.Exists(SettingsPath))
                File.Replace(temporaryPath, SettingsPath, BackupPath, ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, SettingsPath);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("settings-save", ex);
            throw;
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { /* Best-effort cleanup. */ }
        }
    }
}

public static class LanguageDetector
{
    public static string Detect(string filename, string source)
    {
        var ext = Path.GetExtension(filename ?? "").ToLowerInvariant();
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "C#", [".py"] = "Python", [".cpp"] = "C++", [".cc"] = "C++",
            [".c"] = "C", [".h"] = "C/C++", [".hpp"] = "C++", [".js"] = "JavaScript",
            [".jsx"] = "JavaScript", [".ts"] = "TypeScript", [".tsx"] = "TypeScript",
            [".java"] = "Java", [".go"] = "Go", [".rs"] = "Rust",
            [".html"] = "HTML", [".htm"] = "HTML", [".css"] = "CSS",
            [".json"] = "JSON", [".xml"] = "XML", [".sql"] = "SQL",
            [".ps1"] = "PowerShell", [".sh"] = "Shell", [".rb"] = "Ruby",
            [".php"] = "PHP", [".kt"] = "Kotlin", [".swift"] = "Swift"
        };
        if (known.TryGetValue(ext, out var language)) return language;
        var sample = source.Length > 12000 ? source[..12000] : source;
        if (sample.Contains("#include", StringComparison.Ordinal)) return "C/C++";
        if (sample.Contains("using System;", StringComparison.Ordinal) ||
            sample.Contains("namespace ", StringComparison.Ordinal) && sample.Contains("class ", StringComparison.Ordinal)) return "C#";
        if (sample.Contains("def ", StringComparison.Ordinal) && sample.Contains(":", StringComparison.Ordinal)) return "Python";
        if (sample.Contains("console.log(", StringComparison.Ordinal) ||
            sample.Contains("function ", StringComparison.Ordinal)) return "JavaScript";
        if (sample.Contains("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
            sample.Contains("<html", StringComparison.OrdinalIgnoreCase)) return "HTML";
        if (sample.Contains("SELECT ", StringComparison.OrdinalIgnoreCase) &&
            sample.Contains(" FROM ", StringComparison.OrdinalIgnoreCase)) return "SQL";
        return "Авто";
    }
}
