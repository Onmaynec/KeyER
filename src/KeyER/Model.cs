using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyER;

public enum TypingMode { Text, Code }
public enum EditorType { Generic, VisualStudio, VSCode }

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
        Name = (Name ?? "").Trim();
        if (Name.Length == 0) Name = "Профиль";
    }
}

public sealed class AppSettings
{
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
        new() { Name = "Аккуратный код", CharactersPerMinute = 190, TypoPercent = 0.8m, LongPausePercent = 2.0m, Editor = EditorType.VisualStudio }
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
        foreach (var profile in Profiles) profile.Validate();
        if (Profiles.All(p => p.Name != SelectedProfile)) SelectedProfile = Profiles[0].Name;
        if (!Enum.IsDefined(Mode)) Mode = TypingMode.Text;
    }
}

public static class SettingsStore
{
    public static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeyER", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            if (settings is null) return new AppSettings();
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            MessageBox.Show($"Настройки не удалось загрузить: {ex.Message}\nБудут использованы значения по умолчанию.",
                "KeyER", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, SettingsPath, overwrite: true);
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
