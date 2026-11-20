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
            await Check("Foreground loss prevents further keystrokes", ForegroundLoss);
            await Check("Stop cancels future keystrokes", CancelDuringTyping);
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

    public void TypeUnicode(char character)
    {
        _buffer.Insert(Cursor++, character);
        Touched();
    }

    public void PressKey(Keys key)
    {
        switch (key)
        {
            case Keys.Enter:
                _buffer.Insert(Cursor++, '\n');
                break;
            case Keys.Tab:
                _buffer.Insert(Cursor++, '\t');
                break;
            case Keys.Back:
                if (Cursor > 0) _buffer.Remove(--Cursor, 1);
                break;
            case Keys.Delete:
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

    public void PressChord(Keys modifier, Keys key) =>
        throw new NotSupportedException($"Unexpected chord: {modifier}+{key}");
}
