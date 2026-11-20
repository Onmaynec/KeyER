using System.Text;
using KeyER;

internal static class Program
{
    private static int _passed;

    private static async Task<int> Main()
    {
        try
        {
            await Check("Unicode, line breaks and tabs", UnicodeAndLines);
            await Check("Text: random typo correction (100 seeds)", RandomTypos);
            await Check("Code: no unexpected changes in plain editor", CodeAndLines);
            await Check("Text v0.2: delayed multiword repairs and all correction strategies", HumanTextCorrections);
            await Check("Text v0.2: chat, tabs, lists, mixed Unicode", HumanTextFormats);
            await Check("Text v0.2: smooth speed and paragraph breaks", HumanTextTiming);
            await Check("Text v0.2: short and long words, digits and familiar pairs", HumanTextCadenceRules);
            await Check("Foreground loss prevents further keystrokes", ForegroundLoss);
            await Check("Stop cancels future keystrokes", CancelDuringTyping);
            await Check("Pause/resume and stop during pause", PauseAndStop);
            await Check("Global hotkeys do not accidentally require Win", Hotkeys);
            await Check("Start waits for hotkey modifiers to be released", ModifierRelease);
            await Check("Start rejects unrelated window", RejectWrongWindow);
            await Check("Settings validation handles malformed values", SettingsValidation);
            Console.WriteLine($"PASS: {_passed} regression checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAILED: " + error);
            return 1;
        }
    }

    private static async Task Check(string name, Func<Task> test)
    {
        await test();
        _passed++;
        Console.WriteLine("[PASS] " + name);
    }

    private static readonly IntPtr Window = new(4242);

    private static TypingProfile Profile(decimal typo = 0m) => new()
    {
        Name = "Regression",
        CharactersPerMinute = 1200,
        SpeedVariationPercent = 0,
        TypoPercent = typo,
        LongPausePercent = 0,
        BurstPercent = 0,
        CursorReviewPercent = 0,
        LateCorrectionPercent = 100,
        CorrectionLagCharacters = 12
    };

    private static TypingEngine Engine(BufferDevice device, int seed = 42) =>
        new(device, (_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, () => new Random(seed));

    private static async Task UnicodeAndLines()
    {
        const string source = "Привет, world 😀!\r\n\tстрока №2\r\n\r\nКонец.";
        var device = new BufferDevice();
        var engine = Engine(device);
        await engine.StartAsync(source, TypingMode.Text, Profile(), Window);
        Equal(source.Replace("\r\n", "\n"), device.Text, "Unicode output");
        Assert(device.Cursor == device.Text.Length, "Cursor should remain at the end.");
    }

    private static async Task RandomTypos()
    {
        const string source = "qwerty asdfgh zxcvbn hello world Длинноесловоиопечатки ЙЦУКЕНГШЩЗХЪ " +
                              "abcdefghijklmnopqrstuvwxyz имитацияПрограммиста ПриветМосква\r\n" +
                              "Another line with a few verylongwordsincludingseveralerrors and letters.\r\n" +
                              "Last line ending.";
        for (var seed = 0; seed < 100; seed++)
        {
            var device = new BufferDevice();
            var engine = Engine(device, seed);
            await engine.StartAsync(source, TypingMode.Text, Profile(30m), Window);
            Equal(source.Replace("\r\n", "\n"), device.Text, $"seed={seed}");
            Assert(device.Cursor == device.Text.Length, $"Cursor position seed={seed}");
        }
    }

    private static async Task HumanTextCorrections()
    {
        const string sample =
            "This extensive and interesting demonstration includes words and more words to trigger late repairs. " +
            "Программа печатает естественный русский текст при работе с несколькими словами подряд. " +
            "LongerOrdinaryWordsForCorrectionPositions allow repeated checks of the engine. " +
            "ёЁ Аа and a few more letters then emoji 😀 end.\n" +
            "Second paragraph with many individual words to ensure various errors are repaired correctly.";

        // Independently force Backspace, Delete, selection and mixed strategies.
        for (var strategy = 0; strategy < 4; strategy++)
        {
            var totalRepairs = 0;
            var totalDelete = 0;
            var totalSelection = 0;
            var totalBackspace = 0;

            for (var seed = 0; seed < 100; seed++)
            {
                var device = new BufferDevice();
                var profile = Profile(30m);
                profile.LateCorrectionPercent = 100;
                profile.LateCorrectionMaxWords = 3;
                profile.LateCorrectionMaxDistance = 180;
                profile.DeleteCorrectionPercent = strategy == 1 ? 100 : strategy == 3 ? 30 : 0;
                profile.SelectionCorrectionPercent = strategy == 2 ? 100 : strategy == 3 ? 30 : 0;
                var engine = Engine(device, seed);
                await engine.StartAsync(sample, TypingMode.Text, profile, Window);
                Equal(sample, device.Text, $"HumanText strategy={strategy}, seed={seed}");
                Assert(device.Cursor == device.Text.Length, $"Cursor strategy={strategy}, seed={seed}");
                totalRepairs += device.DeleteEvents + device.SelectionEvents + device.BackspaceEvents;
                totalDelete += device.DeleteEvents;
                totalSelection += device.SelectionEvents;
                totalBackspace += device.BackspaceEvents;
            }

            Assert(totalRepairs > 0, "Expected actual corrections to be simulated.");
            if (strategy == 0) Assert(totalBackspace > 0, "Backspace must be exercised.");
            if (strategy == 1) Assert(totalDelete > 0, "Delete must be exercised.");
            if (strategy == 2) Assert(totalSelection > 0, "Shift+Left selection must be exercised.");
            if (strategy == 3) Assert(totalDelete > 0 && totalSelection > 0 && totalBackspace > 0,
                "All repair strategies should be exercised in mixed mode.");
        }
    }

    private static async Task HumanTextFormats()
    {
        const string source = "- Купить молоко\n- Купить хлеб\n\nПривет, мир! 😀 café 👨‍👩‍👧‍👦\n\t* пункт два\nПоследняя строка.";
        var expected = source.Replace("\t", "    ");
        var device = new BufferDevice();
        var profile = Profile(0);
        profile.TextTab = TextTabMode.Spaces;
        profile.TabSpaces = 4;
        profile.TextEnter = TextEnterMode.ShiftEnter;
        await Engine(device).StartAsync(source, TypingMode.Text, profile, Window);
        Equal(expected, device.Text, "chat formatted text");
        Assert(device.ShiftEnterEvents == 5, $"Chat newline shortcut count: {device.ShiftEnterEvents}");
        Assert(device.Cursor == device.Text.Length, "Chat caret at end");

        var noErrors = new BufferDevice();
        await Engine(noErrors).StartAsync(source, TypingMode.Text, Profile(0), Window);
        Equal(source, noErrors.Text, "default literal tabs and newlines");
        Assert(noErrors.Events == source.Length, "0% errors should not introduce extra keyboard events");
    }

    private static async Task HumanTextTiming()
    {
        var intervals = new List<int>();
        var device = new BufferDevice();
        var profile = Profile(0);
        profile.SpeedSmoothingPercent = 18;
        profile.LongPausePercent = 0;
        profile.ParagraphPauseMinMilliseconds = 220;
        profile.ParagraphPauseMaxMilliseconds = 220;
        profile.BlockPauseMinMilliseconds = 700;
        profile.BlockPauseMaxMilliseconds = 700;
        profile.SentencePauseMinMilliseconds = 310;
        profile.SentencePauseMaxMilliseconds = 310;

        var engine = new TypingEngine(device,
            (ms, token) =>
            {
                token.ThrowIfCancellationRequested();
                intervals.Add(ms);
                return Task.CompletedTask;
            }, () => new Random(11));
        const string source = "Hi.\n\nThe lengthywordishere and 12345!\nEnd.";
        await engine.StartAsync(source, TypingMode.Text, profile, Window);
        Equal(source, device.Text, "timing example output");
        Assert(intervals.Contains(310), "Expected sentence pause");
        Assert(intervals.Contains(700), "Expected block pause");
        Assert(intervals.Any(x => x != intervals[0]), "Expected non-constant typing interval");

        // Deterministic input produces the same pause series.
        var otherIntervals = new List<int>();
        var another = new TypingEngine(new BufferDevice(),
            (ms, token) =>
            {
                token.ThrowIfCancellationRequested();
                otherIntervals.Add(ms);
                return Task.CompletedTask;
            }, () => new Random(11));
        await another.StartAsync(source, TypingMode.Text, profile, Window);
        Assert(intervals.SequenceEqual(otherIntervals), "Typing intervals should reproduce with the same seed.");
    }

    private static async Task HumanTextCadenceRules()
    {
        var profile = Profile(0);
        profile.SpeedVariationPercent = 0;
        profile.BurstPercent = 0;
        profile.FatiguePercent = 0;
        profile.ShortWordSpeedPercent = 220;
        profile.LongWordSpeedPercent = 50;
        profile.SpeedSmoothingPercent = 100;
        profile.DigitSlowdownPercent = 300;
        profile.PunctuationSlowdownPercent = 300;
        profile.FamiliarPairBoostPercent = 0;

        async Task<List<int>> Durations(string sample)
        {
            var measures = new List<int>();
            var engine = new TypingEngine(new BufferDevice(), (milliseconds, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                measures.Add(milliseconds);
                return Task.CompletedTask;
            }, () => new Random(37));
            await engine.StartAsync(sample, TypingMode.Text, profile, Window);
            return measures;
        }

        var shortWord = await Durations("cat");
        var longWord = await Durations("abcdefghijklmnop");
        var regular = await Durations("abcde");
        var digits = await Durations("12345");
        var punctuation = await Durations("abcde;");
        Assert(shortWord.Average() < longWord.Average(), "Short words must be faster than long words with these profile settings.");
        Assert(digits.Average() > regular.Average(), "Digits should have an independent slowdown.");
        Assert(punctuation[^1] > punctuation[0], "Punctuation should slow the current symbol.");

        profile.FamiliarPairBoostPercent = 55;
        var familiar = await Durations("th");
        Assert(familiar.Count == 2 && familiar[1] < familiar[0], "Familiar 'th' pair should accelerate.");
    }

    private static async Task CodeAndLines()
    {
        const string source = "using System;\r\n\r\nclass Demo {\r\n    static void Main() {\r\n        Console.WriteLine(\"OK\");\r\n    }\r\n}\r\n";
        var device = new BufferDevice();
        var engine = Engine(device, 17);
        await engine.StartAsync(source, TypingMode.Code, Profile(30m), Window);
        Equal(source.Replace("\r\n", "\n"), device.Text, "code output");
    }

    private static async Task ForegroundLoss()
    {
        var device = new BufferDevice { ChangeFocusAfter = 10 };
        var engine = Engine(device);
        string? status = null;
        engine.StatusChanged += s => status = s;
        await engine.StartAsync(new string('a', 60), TypingMode.Text, Profile(), Window);
        Assert(device.Events == 10, $"Expected 10 events, got {device.Events}.");
        Assert(status is not null && status.Contains("окно изменилось", StringComparison.OrdinalIgnoreCase),
            "Target loss must be reported.");
    }

    private static async Task CancelDuringTyping()
    {
        var device = new BufferDevice();
        var engine = Engine(device);
        device.OnEvent = () =>
        {
            if (device.Events == 10) engine.Stop();
        };
        string? status = null;
        engine.StatusChanged += s => status = s;
        await engine.StartAsync(new string('a', 70), TypingMode.Text, Profile(), Window);
        Assert(device.Events == 10, $"Unexpected events after stop: {device.Events}");
        Assert(status == "Остановлено пользователем.", "Stop status");
        Assert(!engine.IsRunning && !engine.IsPaused, "Should reset after stop.");
    }

    private static async Task PauseAndStop()
    {
        var device = new BufferDevice();
        var engine = Engine(device, 2);
        device.OnEvent = () =>
        {
            if (device.Events == 8) engine.TogglePause();
        };
        var task = engine.StartAsync("paused test completed", TypingMode.Text, Profile(), Window);
        await Task.Delay(120);
        Assert(engine.IsPaused, "Expected paused engine.");
        Assert(device.Events == 8, "Typing continued while paused.");
        engine.TogglePause();
        await task.WaitAsync(TimeSpan.FromSeconds(3));
        Equal("paused test completed", device.Text, "resume must preserve output");

        var secondDevice = new BufferDevice();
        var secondEngine = Engine(secondDevice);
        secondDevice.OnEvent = () =>
        {
            if (secondDevice.Events == 8) secondEngine.TogglePause();
        };
        var secondTask = secondEngine.StartAsync("this never finishes", TypingMode.Text, Profile(), Window);
        await Task.Delay(120);
        secondEngine.Stop();
        await secondTask.WaitAsync(TimeSpan.FromSeconds(3));
        Assert(secondDevice.Events == 8, "Stopping during pause should inject no more keys.");
        Assert(!secondEngine.IsRunning, "Engine should reset state after stop while paused.");
    }

    private static Task Hotkeys()
    {
        var start = Keys.Control | Keys.Alt | Keys.F5;
        var stop = Keys.Control | Keys.Alt | Keys.F7;
        Assert(Native.HotkeyModifiers(start) == 0x4003, "Ctrl+Alt+F5 should not include MOD_WIN.");
        Assert(Native.HotkeyText(start) == "Ctrl+Alt+F5", "Hotkey display");
        Assert(Native.HotkeyModifiers(stop) == 0x4003, "Stop should not require Win.");
        Assert(Native.HotkeyModifiers(Keys.Shift | Keys.F8) == 0x4004, "Shift F8 mapping.");
        return Task.CompletedTask;
    }

    private static async Task ModifierRelease()
    {
        var device = new BufferDevice { ModifiersHeld = true };
        var engine = Engine(device);
        var task = engine.StartAsync("release", TypingMode.Text, Profile(), Window);
        await Task.Delay(80);
        Assert(device.Events == 0, "Typing started while hotkey modifiers were held.");
        device.ModifiersHeld = false;
        await task.WaitAsync(TimeSpan.FromSeconds(3));
        Equal("release", device.Text, "typing after release");
    }

    private static async Task RejectWrongWindow()
    {
        var device = new BufferDevice { Active = false };
        var engine = Engine(device);
        var threw = false;
        try { await engine.StartAsync("test", TypingMode.Text, Profile(), Window); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "Expected failure when target is not foreground.");
        Assert(device.Events == 0, "No keys may be sent to wrong foreground target.");
    }

    private static Task SettingsValidation()
    {
        var settings = new AppSettings
        {
            Profiles = [new() { Name = "", CharactersPerMinute = -10, TypoPercent = 90m, PauseMinMilliseconds = 12000, PauseMaxMilliseconds = -5 }],
            SelectedProfile = "invalid",
            Mode = (TypingMode)900
        };
        settings.Validate();
        Assert(settings.GetProfile().CharactersPerMinute == 20, "Minimum speed");
        Assert(settings.GetProfile().TypoPercent == 30m, "Maximum typo");
        Assert(settings.GetProfile().PauseMaxMilliseconds >= settings.GetProfile().PauseMinMilliseconds, "Valid pause range");
        Assert(settings.Mode == TypingMode.Text, "Mode fallback");
        Assert(settings.Profiles.Any(x => x.Name == "Обычный текст") &&
               settings.Profiles.Any(x => x.Name == "Переписка"),
            "Old profiles should be migrated to include the Text presets.");
        Assert(settings.Profiles.Any(x => x.Name == "Профиль"),
            "Custom/invalid profile should be kept after validation.");
        var previousCount = settings.Profiles.Count;
        settings.Validate();
        Assert(settings.Profiles.Count == previousCount, "Migration must be idempotent.");
        return Task.CompletedTask;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Equal(string expected, string actual, string context)
    {
        if (expected == actual) return;
        var diff = 0;
        while (diff < expected.Length && diff < actual.Length && expected[diff] == actual[diff]) diff++;
        throw new Exception($"{context}: text differs at position {diff}, expected length={expected.Length}, actual length={actual.Length}.");
    }
}

internal sealed class BufferDevice : ITypingDevice
{
    private readonly StringBuilder _buffer = new();
    public bool Active { get; set; } = true;
    public bool ModifiersHeld { get; set; }
    public int ChangeFocusAfter { get; set; } = -1;
    public Action? OnEvent { get; set; }
    public int Cursor { get; private set; }
    public int Events { get; private set; }
    public string Text => _buffer.ToString();

    public bool IsTargetActive(IntPtr target) => Active && target == new IntPtr(4242);
    public bool AreModifiersHeld => ModifiersHeld;

    private void Touched()
    {
        Events++;
        if (Events == ChangeFocusAfter) Active = false;
        OnEvent?.Invoke();
    }

    private int? _selectionAnchor;
    public int DeleteEvents { get; private set; }
    public int BackspaceEvents { get; private set; }
    public int SelectionEvents { get; private set; }
    public int ShiftEnterEvents { get; private set; }

    private void RemoveSelection()
    {
        if (_selectionAnchor is null) return;
        var start = Math.Min(Cursor, _selectionAnchor.Value);
        var end = Math.Max(Cursor, _selectionAnchor.Value);
        _buffer.Remove(start, end - start);
        Cursor = start;
        _selectionAnchor = null;
    }

    public void TypeUnicode(char character)
    {
        RemoveSelection();
        _buffer.Insert(Cursor++, character);
        Touched();
    }

    public void PressKey(Keys key)
    {
        if (key is Keys.Enter or Keys.Tab or Keys.Back or Keys.Delete)
            RemoveSelection();
        else
            _selectionAnchor = null;
        switch (key)
        {
            case Keys.Enter:
                _buffer.Insert(Cursor++, '\n');
                break;
            case Keys.Tab:
                _buffer.Insert(Cursor++, '\t');
                break;
            case Keys.Back:
                BackspaceEvents++;
                if (Cursor > 0) _buffer.Remove(--Cursor, 1);
                break;
            case Keys.Delete:
                DeleteEvents++;
                if (Cursor < _buffer.Length) _buffer.Remove(Cursor, 1);
                break;
            case Keys.Left:
                if (Cursor > 0) Cursor--;
                break;
            case Keys.Right:
                if (Cursor < _buffer.Length) Cursor++;
                break;
            case Keys.Home:
                while (Cursor > 0 && _buffer[Cursor - 1] != '\n') Cursor--;
                break;
            case Keys.End:
                while (Cursor < _buffer.Length && _buffer[Cursor] != '\n') Cursor++;
                break;
            default:
                throw new NotSupportedException("Unexpected test key: " + key);
        }
        Touched();
    }

    public void PressChord(Keys modifier, Keys key)
    {
        if (modifier != Keys.ShiftKey)
            throw new NotSupportedException($"Unexpected chord: {modifier}+{key}");

        if (key == Keys.Enter)
        {
            ShiftEnterEvents++;
            RemoveSelection();
            _buffer.Insert(Cursor++, '\n');
        }
        else if (key == Keys.Left)
        {
            SelectionEvents++;
            _selectionAnchor ??= Cursor;
            if (Cursor > 0) Cursor--;
        }
        else throw new NotSupportedException($"Unexpected chord: {modifier}+{key}");
        Touched();
    }
}
