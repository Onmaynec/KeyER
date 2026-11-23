using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyER;

public enum TypingMode { Text, Code }
public enum HumanStyle { Custom, Beginner, Casual, Developer, Professional }
public enum EditorType { Generic, VisualStudio, VSCode }
public enum CodeIntegrityMode { Off, VerifyOnly, VerifyAndRepair }
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
    // Opt-in: verified typing only works in blank Visual Studio / VS Code editors.
    public CodeIntegrityMode CodeIntegrity { get; set; } = CodeIntegrityMode.Off;
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

    // v0.5.0: controlled attention/fatigue behaviors; Custom preserves old profiles.
    public HumanStyle Style { get; set; } = HumanStyle.Custom;
    public int FocusMinCharacters { get; set; } = 35;
    public int FocusMaxCharacters { get; set; } = 130;
    public int FocusRecoveryPercent { get; set; } = 22;
    public int AttentionBreakPercent { get; set; } = 65;
    public int AttentionBreakMinMilliseconds { get; set; } = 300;
    public int AttentionBreakMaxMilliseconds { get; set; } = 1050;
    public int ThinkingPausePercent { get; set; } = 9;
    public int ThinkingPauseMinMilliseconds { get; set; } = 200;
    public int ThinkingPauseMaxMilliseconds { get; set; } = 700;
    public int LookbackPercent { get; set; } = 2;
    public int LookbackMaxCharacters { get; set; } = 5;

    // Humanizer 3.0: motor rhythm shared between Text and Code, with
    // correlated timing and bounded context-dependent hesitation.
    public int HumanizerStrength { get; set; } = 80;
    public int KeyTravelInfluence { get; set; } = 72;
    public int RhythmMemoryPercent { get; set; } = 72;
    public int MicroHesitationPercent { get; set; } = 10;

    // Code Intelligence v0.3: separate syntax-aware timing and review.
    public int CodeLinePauseMilliseconds { get; set; } = 90;
    public int CodeFunctionPauseMilliseconds { get; set; } = 520;
    public int CodeBlockPauseMilliseconds { get; set; } = 250;
    public int CodeSyntaxPauseMilliseconds { get; set; } = 140;
    public int CodeIdentifierSpeedPercent { get; set; } = 115;
    public int CodeStringSpeedPercent { get; set; } = 85;
    public int CodeCommentSpeedPercent { get; set; } = 130;
    public int CodeReviewPercent { get; set; } = 0;

    public void ApplyStyle(HumanStyle style)
    {
        Style = style;
        switch (style)
        {
            case HumanStyle.Beginner:
                CharactersPerMinute = 130; SpeedVariationPercent = 42;
                TypoPercent = 6m; LateCorrectionPercent = 28;
                BurstPercent = 8; FatiguePercent = 28;
                FocusMinCharacters = 15; FocusMaxCharacters = 50;
                FocusRecoveryPercent = 36; AttentionBreakPercent = 90;
                ThinkingPausePercent = 24; LookbackPercent = 12;
                CodeReviewPercent = 12;
                HumanizerStrength = 95; KeyTravelInfluence = 90;
                RhythmMemoryPercent = 50; MicroHesitationPercent = 20;
                break;
            case HumanStyle.Casual:
                CharactersPerMinute = 255; SpeedVariationPercent = 30;
                TypoPercent = 2.8m; LateCorrectionPercent = 45;
                BurstPercent = 25; FatiguePercent = 15;
                FocusMinCharacters = 35; FocusMaxCharacters = 100;
                FocusRecoveryPercent = 22; AttentionBreakPercent = 65;
                ThinkingPausePercent = 10; LookbackPercent = 4;
                CodeReviewPercent = 3;
                HumanizerStrength = 85; KeyTravelInfluence = 73;
                RhythmMemoryPercent = 72; MicroHesitationPercent = 10;
                break;
            case HumanStyle.Developer:
                CharactersPerMinute = 335; SpeedVariationPercent = 22;
                TypoPercent = 1.5m; LateCorrectionPercent = 55;
                BurstPercent = 36; FatiguePercent = 11;
                FocusMinCharacters = 60; FocusMaxCharacters = 150;
                FocusRecoveryPercent = 25; AttentionBreakPercent = 55;
                ThinkingPausePercent = 14; LookbackPercent = 5;
                CodeReviewPercent = 10;
                HumanizerStrength = 82; KeyTravelInfluence = 70;
                RhythmMemoryPercent = 79; MicroHesitationPercent = 13;
                break;
            case HumanStyle.Professional:
                CharactersPerMinute = 465; SpeedVariationPercent = 12;
                TypoPercent = 0.6m; LateCorrectionPercent = 65;
                BurstPercent = 40; FatiguePercent = 6;
                FocusMinCharacters = 95; FocusMaxCharacters = 220;
                FocusRecoveryPercent = 16; AttentionBreakPercent = 30;
                ThinkingPausePercent = 4; LookbackPercent = 1;
                CodeReviewPercent = 2;
                HumanizerStrength = 70; KeyTravelInfluence = 64;
                RhythmMemoryPercent = 88; MicroHesitationPercent = 5;
                break;
            case HumanStyle.Custom: break; // retaining all existing parameters
        }
        Validate();
    }

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
        if (!Enum.IsDefined(Style)) Style = HumanStyle.Custom;
        FocusMinCharacters = Math.Clamp(FocusMinCharacters, 5, 1000);
        FocusMaxCharacters = Math.Clamp(FocusMaxCharacters, FocusMinCharacters, 2000);
        FocusRecoveryPercent = Math.Clamp(FocusRecoveryPercent, 0, 100);
        AttentionBreakPercent = Math.Clamp(AttentionBreakPercent, 0, 100);
        AttentionBreakMinMilliseconds = Math.Clamp(AttentionBreakMinMilliseconds, 0, 15000);
        AttentionBreakMaxMilliseconds = Math.Clamp(AttentionBreakMaxMilliseconds, AttentionBreakMinMilliseconds, 30000);
        ThinkingPausePercent = Math.Clamp(ThinkingPausePercent, 0, 100);
        ThinkingPauseMinMilliseconds = Math.Clamp(ThinkingPauseMinMilliseconds, 0, 15000);
        ThinkingPauseMaxMilliseconds = Math.Clamp(ThinkingPauseMaxMilliseconds, ThinkingPauseMinMilliseconds, 30000);
        LookbackPercent = Math.Clamp(LookbackPercent, 0, 30);
        LookbackMaxCharacters = Math.Clamp(LookbackMaxCharacters, 1, 12);
        HumanizerStrength = Math.Clamp(HumanizerStrength, 0, 100);
        KeyTravelInfluence = Math.Clamp(KeyTravelInfluence, 0, 100);
        RhythmMemoryPercent = Math.Clamp(RhythmMemoryPercent, 0, 96);
        MicroHesitationPercent = Math.Clamp(MicroHesitationPercent, 0, 35);
        CodeLinePauseMilliseconds = Math.Clamp(CodeLinePauseMilliseconds, 0, 5000);
        CodeFunctionPauseMilliseconds = Math.Clamp(CodeFunctionPauseMilliseconds, 0, 10000);
        CodeBlockPauseMilliseconds = Math.Clamp(CodeBlockPauseMilliseconds, 0, 10000);
        CodeSyntaxPauseMilliseconds = Math.Clamp(CodeSyntaxPauseMilliseconds, 0, 5000);
        CodeIdentifierSpeedPercent = Math.Clamp(CodeIdentifierSpeedPercent, 40, 250);
        CodeStringSpeedPercent = Math.Clamp(CodeStringSpeedPercent, 40, 250);
        CodeCommentSpeedPercent = Math.Clamp(CodeCommentSpeedPercent, 40, 250);
        CodeReviewPercent = Math.Clamp(CodeReviewPercent, 0, 30);
        if (!Enum.IsDefined(CodeIntegrity)) CodeIntegrity = CodeIntegrityMode.Off;
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
    public string CodeLanguage { get; set; } = "Auto";

    // UI v0.6.0 preferences are independent of typing profiles.
    public AppearanceMode Appearance { get; set; } = AppearanceMode.Dark;
    public AccentTheme Accent { get; set; } = AccentTheme.Iris;
    public bool CompactMode { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = false;
    public bool ShowLineNumbers { get; set; } = true;
    public bool SyntaxHighlighting { get; set; } = true;
    public int EditorFontSize { get; set; } = 11;
    public int WindowWidth { get; set; } = 1080;
    public int WindowHeight { get; set; } = 720;
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
            BlockPauseMinMilliseconds = 550, BlockPauseMaxMilliseconds = 1100 },
        MakeStylePreset("Beginner", HumanStyle.Beginner),
        MakeStylePreset("Casual", HumanStyle.Casual),
        MakeStylePreset("Developer", HumanStyle.Developer),
        MakeStylePreset("Professional", HumanStyle.Professional),
        NaturalPreset()
    ];

    public static TypingProfile NaturalPreset()
    {
        var profile = MakeStylePreset("Естественный ритм", HumanStyle.Casual);
        profile.HumanizerStrength = 93;
        profile.KeyTravelInfluence = 85;
        profile.RhythmMemoryPercent = 83;
        profile.MicroHesitationPercent = 15;
        profile.TypoPercent = 2.2m;
        profile.LookbackPercent = 4;
        profile.AttentionBreakPercent = 58;
        profile.Validate();
        return profile;
    }

    public static TypingProfile MakeStylePreset(string name, HumanStyle style)
    {
        var profile = new TypingProfile { Name = name };
        profile.ApplyStyle(style);
        return profile;
    }

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
        if (SettingsSchemaVersion < 3)
        {
            // Add style presets once, without overwriting custom profiles or selections.
            foreach (var name in new[] { "Beginner", "Casual", "Developer", "Professional" })
            {
                if (Profiles.All(p => !string.Equals(p.Name, name,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    var style = Enum.Parse<HumanStyle>(name);
                    Profiles.Add(MakeStylePreset(name, style));
                }
            }
            SettingsSchemaVersion = 3;
        }
        if (SettingsSchemaVersion < 4)
        {
            // New attributes get their class initializer defaults when absent
            // from old JSON. Never reset user-modified values or style choices.
            if (Profiles.All(p => !string.Equals(p.Name, "Естественный ритм",
                StringComparison.OrdinalIgnoreCase)))
                Profiles.Add(NaturalPreset());
            SettingsSchemaVersion = 4;
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
        if (!Enum.IsDefined(Appearance)) Appearance = AppearanceMode.Dark;
        if (!Enum.IsDefined(Accent)) Accent = AccentTheme.Iris;
        EditorFontSize = Math.Clamp(EditorFontSize, 9, 18);
        WindowWidth = Math.Clamp(WindowWidth, 760, 1920);
        WindowHeight = Math.Clamp(WindowHeight, 520, 1200);
        if (!Enum.TryParse<CodeAnalyzer.Language>(CodeLanguage, ignoreCase: true, out var preferred) ||
            !Enum.IsDefined(preferred))
            CodeLanguage = "Auto";
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

internal enum SettingsLoadOrigin { Defaults, Primary, Backup }

internal sealed record SettingsLoadResult(AppSettings Settings, SettingsLoadOrigin Origin,
    bool HasUnreadableFiles);

public static class SettingsStore
{
    public static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeyER", "settings.json");

    private const long MaximumSettingsBytes = 4_194_304;

    public static AppSettings Load()
    {
        var result = LoadFromPath(SettingsPath);
        if (result.Origin == SettingsLoadOrigin.Backup)
        {
            MessageBox.Show(
                "Основной файл настроек повреждён или недоступен.\n" +
                "Загружена последняя резервная копия. При сохранении старая копия " +
                "не будет заменена повреждёнными данными.",
                "KeyER — восстановление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else if (result.Origin == SettingsLoadOrigin.Defaults && result.HasUnreadableFiles)
        {
            MessageBox.Show(
                "Не удалось прочитать настройки KeyER. Загружены стандартные значения.\n" +
                "При сохранении повреждённый файл будет скопирован для восстановления.",
                "KeyER — настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        return result.Settings;
    }

    internal static SettingsLoadResult LoadFromPath(string path)
    {
        var original = ReadValid(path);
        if (original is not null)
            return new SettingsLoadResult(original, SettingsLoadOrigin.Primary, false);

        var backup = ReadValid(path + ".bak");
        if (backup is not null)
            return new SettingsLoadResult(backup, SettingsLoadOrigin.Backup, File.Exists(path));

        var unreadable = File.Exists(path) || File.Exists(path + ".bak");
        return new SettingsLoadResult(new AppSettings(), SettingsLoadOrigin.Defaults, unreadable);
    }

    private static AppSettings? ReadValid(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaximumSettingsBytes)
                return null;

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            if (settings is null) return null;
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                   or ArgumentException or NotSupportedException
                                   or System.Security.SecurityException)
        {
            DiagnosticLog.Error("settings-load", ex);
            return null;
        }
    }

    public static void Save(AppSettings settings) => SaveToPath(SettingsPath, settings);

    /// <summary>
    /// Atomic settings commit with a good last-known backup. A corrupt primary
    /// is first archived, never allowed to overwrite a healthy .bak.
    /// Tests use a temporary directory without writing into the user's profile.
    /// </summary>
    internal static void SaveToPath(string path, AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(JsonSerializer.Serialize(settings,
                    new JsonSerializerOptions { WriteIndented = true }));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }

            if (ReadValid(path) is not null)
            {
                // Only a valid primary may become the next fallback backup.
                File.Replace(temporary, path, path + ".bak", ignoreMetadataErrors: true);
                return;
            }

            // Preserve the original unreadable file for user recovery without
            // poisoning a previously valid backup. Do not log or export it.
            var archive = path + ".corrupt-" +
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
                Guid.NewGuid().ToString("N")[..8];
            File.Copy(path, archive, overwrite: false);
            File.Replace(temporary, path, destinationBackupFileName: null,
                ignoreMetadataErrors: true);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("settings-save", ex);
            throw;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { /* Preserve the original error. */ }
            catch (UnauthorizedAccessException) { /* Best effort cleanup. */ }
        }
    }
}
