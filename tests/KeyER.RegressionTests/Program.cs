using System.Text;
using System.Text.Json;
using System.IO.Pipes;
using KeyER;

internal static class Program
{
    private static int _passed;

    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--studio-preview"))
            return await GenerateStudioPreviewAsync();

        try
        {
            await Check("Unicode, line breaks and tabs", UnicodeAndLines);
            await Check("Text: random typo correction (100 seeds)", RandomTypos);
            await Check("Code: no unexpected changes in plain editor", CodeAndLines);
            await Check("Code 0.3: exact lexical plans across 15 languages", CodePlanning);
            await Check("Code 0.3: multi-line comments, strings and nesting", CodeLexicalContexts);
            await Check("Code 0.3: accuracy with deliberate identifier typos and reviews", CodeTypingAccuracy);
            await Check("Code 0.3: contextual pauses and manual language selection", CodeTimingAndOverride);
            await Check("Text v0.2: delayed multiword repairs and all correction strategies", HumanTextCorrections);
            await Check("Text v0.2: chat, tabs, lists, mixed Unicode", HumanTextFormats);
            await Check("Text v0.2: smooth speed and paragraph breaks", HumanTextTiming);
            await Check("Text v0.2: short and long words, digits and familiar pairs", HumanTextCadenceRules);
            await Check("Foreground loss prevents further keystrokes", ForegroundLoss);
            await Check("Reliability 0.8: compatible target diagnostic states", CompatibilityMatrix);
            await Check("Reliability 0.8: stop on unexpected modifiers", UnexpectedModifiers);
            await Check("Reliability 0.8: no retry after interrupted session", InterruptedSessionRestart);
            await Check("Reliability 0.8: privacy-safe completion and interruption reports", SessionReportValidation);
            await Check("Stop cancels future keystrokes", CancelDuringTyping);
            await Check("Pause/resume and stop during pause", PauseAndStop);
            await Check("Global hotkeys do not accidentally require Win", Hotkeys);
            await Check("Start waits for hotkey modifiers to be released", ModifierRelease);
            await Check("Start rejects unrelated window", RejectWrongWindow);
            await Check("Settings validation handles malformed values", SettingsValidation);
            await Check("Stable 1.0: settings primary/backup recovery", StableSettingsRecovery);
            await Check("Stable 1.0: safe migration without custom profile loss", StableMigration);
            await Check("Stable 1.0: interrupted save never replaces a valid backup", StableSettingsProtection);
            await Check("Stable 1.0: cancelled startup never contacts target", StableCancelledStartup);
            await Check("Stable 1.0: multi-profile deterministic soak across Text/Code", StableSoak);
            await Check("Stable 1.0: atomic global-hotkey reconfiguration and rollback", StableHotkeyRollback);
            await Check("Beta 0.9: single-instance startup guard", BetaSingleInstance);
            await Check("Beta 0.9: atomic document saving", BetaAtomicWrite);
            await Check("Beta 0.9: sanitized support report", BetaSupportPrivacy);
            await Check("Studio 0.7: palette and appearance validation", StudioAppearanceValidation);
            await Check("Studio 0.7: unicode-aware find, wrap and match counts", StudioSearch);
            await Check("Studio 0.7: main window/settings construction on STA", StudioUiSmoke);
            await Check("IDE 0.4: same-user pipe roundtrip and session invalidation", IdePipeRoundTrip);
            await Check("IDE 0.4: verified mode remains opt-in", IdeSettingsSafety);
            await Check("Human 0.5: style presets and settings migration", HumanStylePresets);
            await Check("Human 0.5: concentration/recovery deterministic behavior", HumanFocus);
            await Check("Human 0.5: reversible lookback in Text and Code (400 runs)", HumanLookbacks);
            await Check("Humanizer 1.1: deterministic motor timing and bounded ranges", HumanizerTiming);
            await Check("Humanizer 1.1: bilingual keyboard adjacency and weighted mistakes", HumanizerKeyboard);
            await Check("Humanizer 1.1: controlled pauses, errors and parameter boundaries", HumanizerContexts);
            await Check("Humanizer 1.1: schema v4 and natural profile without custom data loss", HumanizerMigration);
            await Check("Humanizer 1.1: precise seeded Text/Code output across profiles", HumanizerEndToEnd);
            Console.WriteLine($"PASS: {_passed} regression checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAILED: " + error);
            return 1;
        }
    }

    private static Task<int> GenerateStudioPreviewAsync()
    {
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var directory = Path.GetFullPath("artifacts/ui-preview");
                Directory.CreateDirectory(directory);

                Environment.SetEnvironmentVariable("KEYER_UI_PREVIEW", "1");
                using var main = new MainForm
                {
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(60, 60),
                    Size = new Size(1170, 760)
                };
                main.Show();
                Application.DoEvents();
                var editor = Descendants(main).OfType<RichTextBox>().Single();
                var codeButton = Descendants(main).OfType<StudioButton>()
                    .FirstOrDefault(b => b.Text.Contains("Редактор кода", StringComparison.Ordinal));
                codeButton?.PerformClick();
                // Capture the actual first-run experience before inserting a snippet.
                main.Refresh();
                Application.DoEvents();
                using (var empty = new Bitmap(main.Width, main.Height))
                {
                    main.DrawToBitmap(empty, new Rectangle(Point.Empty, empty.Size));
                    empty.Save(Path.Combine(directory, "KeyER-Studio-Empty.png"));
                }

                editor.Text =
                    "using System;\n\nnamespace KeyER.Preview;\n\n" +
                    "public static class HumanTyping\n{\n" +
                    "    public static void Hello(string name)\n    {\n" +
                    "        // Пример форматирования кода в редакторе\n" +
                    "        Console.WriteLine($\"Hello, {name}!\");\n" +
                    "    }\n}\n";
                Application.DoEvents();
                Thread.Sleep(350); // Let the syntax-highlighting timer elapse.
                Application.DoEvents();
                main.Refresh();
                Application.DoEvents();
                using (var bitmap = new Bitmap(main.Width, main.Height))
                {
                    main.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(directory, "KeyER-Studio-Main.png"));
                }
                var searchButton = Descendants(main).OfType<StudioButton>()
                    .FirstOrDefault(b => b.Text.Contains("Поиск", StringComparison.Ordinal));
                searchButton?.PerformClick();
                var findField = Descendants(main).OfType<TextBox>()
                    .FirstOrDefault(t => t.PlaceholderText.Contains("Поиск по исходнику"));
                if (findField is not null) findField.Text = "class";
                Application.DoEvents();
                using (var searching = new Bitmap(main.Width, main.Height))
                {
                    main.DrawToBitmap(searching, new Rectangle(Point.Empty, searching.Size));
                    searching.Save(Path.Combine(directory, "KeyER-Studio-Find.png"));
                }


                using var settings = new SettingsForm(new AppSettings())
                {
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(65, 65),
                    Size = new Size(1050, 790)
                };
                settings.Show(main);
                Application.DoEvents();
                settings.Refresh();
                Application.DoEvents();
                using (var bitmap = new Bitmap(settings.Width, settings.Height))
                {
                    settings.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(directory, "KeyER-Studio-Settings.png"));
                }

                var appearanceTab = Descendants(settings).OfType<StudioButton>()
                    .FirstOrDefault(b => b.Text.Contains("Оформление", StringComparison.Ordinal));
                appearanceTab?.PerformClick();
                Application.DoEvents();
                settings.Refresh();
                Application.DoEvents();
                using (var bitmap = new Bitmap(settings.Width, settings.Height))
                {
                    settings.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(directory, "KeyER-Studio-Appearance.png"));
                }

                var diagnosticsTab = Descendants(settings).OfType<StudioButton>()
                    .FirstOrDefault(b => b.Text.Contains("Диагностика", StringComparison.Ordinal));
                diagnosticsTab?.PerformClick();
                Application.DoEvents();
                settings.Refresh();
                Application.DoEvents();
                using (var bitmap = new Bitmap(settings.Width, settings.Height))
                {
                    settings.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(directory, "KeyER-Studio-Beta-Diagnostics.png"));
                }

                using (var checkDialog = new TargetDiagnosticsForm(
                    TargetCompatibility.Evaluate(true, false, true, true,
                        false, false, false, false, "notepad")))
                {
                    checkDialog.Show(main);
                    Application.DoEvents();
                    using var bitmap = new Bitmap(checkDialog.Width, checkDialog.Height);
                    checkDialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(directory, "KeyER-Studio-Compatibility.png"));
                    checkDialog.Close();
                }

                settings.Close();
                main.Close();
                result.TrySetResult(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("UI preview unavailable: " + ex);
                result.TrySetResult(1);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return result.Task;
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

    private static Task CodePlanning()
    {
        var samples = new (string filename, string source, CodeAnalyzer.Language expected)[]
        {
            ("hello.cs", "using System;\r\nclass A { string S = \"}\"; }\r\n", CodeAnalyzer.Language.CSharp),
            ("main.cpp", "#include <iostream>\nint main() { return 0; }\n", CodeAnalyzer.Language.Cpp),
            ("hello.py", "def f(x):\n    return x + 1\n", CodeAnalyzer.Language.Python),
            ("app.js", "function main() { return 1; }\n", CodeAnalyzer.Language.JavaScript),
            ("app.ts", "export const add = (a: number) => a + 1;\n", CodeAnalyzer.Language.TypeScript),
            ("main.go", "package main\nfunc main() {}\n", CodeAnalyzer.Language.Go),
            ("lib.rs", "pub fn greet() { println!(\"hi\"); }\n", CodeAnalyzer.Language.Rust),
            ("main.java", "class App { public static void main() {} }\n", CodeAnalyzer.Language.Java),
            ("index.html", "<html>\n<!-- note -->\n<div>Hello</div>\n</html>", CodeAnalyzer.Language.Html),
            ("style.css", "/* comment */\n.a { color: red; }\n", CodeAnalyzer.Language.Css),
            ("app.json", "{\"text\": \"} not brace\", \"count\": 2}", CodeAnalyzer.Language.Json),
            ("schema.sql", "SELECT id FROM users WHERE id = 7;\n", CodeAnalyzer.Language.Sql),
            ("build.sh", "#!/bin/bash\nprintf 'hello'\n", CodeAnalyzer.Language.Shell),
            ("app.ps1", "# powershell\nWrite-Host \"hello\"\n", CodeAnalyzer.Language.PowerShell),
            ("main.swift", "func main() { print(\"hi\") }\n", CodeAnalyzer.Language.Swift)
        };
        foreach (var (file, source, lang) in samples)
        {
            var plan = CodeAnalyzer.Analyze(source, file);
            Assert(plan.Language == lang, $"Language for {file}");
            Equal(source.Replace("\r\n", "\n"), plan.Reassemble(), $"reassemble {file}");
            foreach (var line in plan.Lines)
            {
                Equal(line.Text, string.Concat(line.Tokens.Select(x => x.Text)), $"tokens {file}:{line.Number}");
                Assert(line.Tokens.Select((x, i) => x.Offset == line.Tokens.Take(i).Sum(t => t.Text.Length)).All(x => x),
                    $"token offsets {file}:{line.Number}");
            }
        }
        Assert(CodeAnalyzer.Detect("note.py", "function foo() {}") == CodeAnalyzer.Language.Python,
            "File extension must win over heuristics");
        Assert(CodeAnalyzer.Detect(null, "def example():\n    pass\n") == CodeAnalyzer.Language.Python,
            "Python snippet detection");
        Assert(CodeAnalyzer.Analyze("anything", "a.py", CodeAnalyzer.Language.CSharp).Language ==
            CodeAnalyzer.Language.CSharp, "Manual language override must win");
        return Task.CompletedTask;
    }

    private static Task CodeLexicalContexts()
    {
        var csharp = CodeAnalyzer.Analyze(
            "class Outer {\n" +
            "  string txt = \"} // not a comment\";\n" +
            "  /* class Fake\n" +
            "     } hidden { */\n" +
            "  void Go() {\n" +
            "    // def hidden()\n" +
            "    return;\n" +
            "  }\n" +
            "}\n", "test.cs");
        Assert(csharp.Lines[1].Depth == 1, "String closing brace shouldn't change depth");
        Assert(csharp.Lines[2].Tokens.Any(t => t.Kind == CodeAnalyzer.TokenKind.Comment),
            "C# block comment opening");
        Assert(csharp.Lines[3].Tokens.Any(t => t.Kind == CodeAnalyzer.TokenKind.Comment),
            "C# continued block comment");
        Assert(csharp.Lines[4].Kind == CodeAnalyzer.LineKind.Function,
            "C# method line classification");
        Assert(csharp.Lines[6].Kind == CodeAnalyzer.LineKind.Return, "Return line classification");
        Assert(csharp.Lines[8].Depth == 0, "C# outer closing brace depth");

        var python = CodeAnalyzer.Analyze(
            "def example():\n" +
            "    text = \"\"\"class No\n" +
            "    def not_function():\n" +
            "    \"\"\"\n" +
            "    # class not_a_class\n" +
            "    return text\n", "example.py");
        Assert(python.Lines[0].Kind == CodeAnalyzer.LineKind.Function, "Python function");
        Assert(python.Lines[2].Tokens.All(t => t.Kind == CodeAnalyzer.TokenKind.String),
            "Python triple-quoted block");
        Assert(python.Lines[4].Kind == CodeAnalyzer.LineKind.Comment, "Python hash comment");
        Assert(python.Lines[5].Depth == 1, "Python indentation depth");

        var html = CodeAnalyzer.Analyze("<div>\n<!-- <b>\n  comment -->\n</div>", "index.html");
        Assert(html.Lines[1].Tokens.Single().Kind == CodeAnalyzer.TokenKind.Comment, "HTML comment");
        Assert(html.Lines[2].Tokens.Any(t => t.Kind == CodeAnalyzer.TokenKind.Comment), "HTML comment continuation");
        return Task.CompletedTask;
    }

    private static async Task CodeTypingAccuracy()
    {
        var examples = new (string name, string code)[]
        {
            ("Example.cs", "using System;\nclass Test {\n    static int Sum(int count) {\n        return count + 42;\n    }\n}\n"),
            ("script.py", "def greet(name):\n    message = 'Hello ' + name\n    if len(message) > 5:\n        print(message)\n"),
            ("site.html", "<!DOCTYPE html>\n<html><body><h1>Hi!</h1></body></html>\n"),
            ("data.json", "{\n  \"name\": \"KeyER\",\n  \"count\": 42,\n  \"active\": true\n}\n"),
            ("query.sql", "SELECT id, name FROM people WHERE active = 1;\n"),
            ("main.cpp", "#include <string>\nint main() { int answer = 42; return answer; }\n"),
            ("script.js", "function greet(name) {\n  return 'Hi ' + name;\n}\n")
        };
        foreach (var (file, code) in examples)
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var profile = Profile(30m);
                profile.CodeReviewPercent = 25;
                profile.CodeLinePauseMilliseconds = 0;
                profile.CodeFunctionPauseMilliseconds = 0;
                profile.CodeBlockPauseMilliseconds = 0;
                profile.CodeSyntaxPauseMilliseconds = 0;
                var device = new BufferDevice();
                await Engine(device, seed).StartAsync(code, TypingMode.Code, profile,
                    Window, file);
                Equal(code, device.Text, $"Code typed {file}, seed={seed}");
                Assert(device.Cursor == device.Text.Length, $"Code caret {file}:{seed}");
            }
        }
    }

    private static async Task CodeTimingAndOverride()
    {
        var delays = new List<int>();
        var profile = Profile(0m);
        profile.CodeFunctionPauseMilliseconds = 500;
        profile.CodeBlockPauseMilliseconds = 300;
        profile.CodeLinePauseMilliseconds = 100;
        profile.CodeSyntaxPauseMilliseconds = 200;
        var device = new BufferDevice();
        var engine = new TypingEngine(device, (ms, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            delays.Add(ms);
            return Task.CompletedTask;
        }, () => new Random(2));
        const string source = "def example(name):\n    return 'Hello ' + name\n";
        await engine.StartAsync(source, TypingMode.Code, profile, Window,
            null, "Python");
        Equal(source, device.Text, "Manual Python typing");
        Assert(delays.Any(d => d >= 500), "Function planning pause expected");
        Assert(delays.Contains(100), "Line pause expected");
        Assert(delays.Any(d => d >= 200), "Syntax pause expected");
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

    private static Task CompatibilityMatrix()
    {
        static TargetHealthReport Check(TargetHealthCode expected,
            bool hasWindow = true, bool own = false, bool available = true,
            bool foreground = true, bool minimized = false, bool hung = false,
            bool? targetElevation = false, bool? currentElevation = false)
        {
            var report = TargetCompatibility.Evaluate(hasWindow, own, available,
                foreground, minimized, hung, targetElevation, currentElevation, "notepad");
            Assert(report.Code == expected,
                $"Expected compatibility={expected}, returned {report.Code}.");
            Assert(report.CanType == (expected == TargetHealthCode.Ready),
                "CanType must be restricted to Ready.");
            Assert(!string.IsNullOrWhiteSpace(report.Guidance), "Guidance should be actionable.");
            return report;
        }

        Check(TargetHealthCode.NoWindow, hasWindow: false);
        Check(TargetHealthCode.OwnWindow, own: true);
        Check(TargetHealthCode.WindowUnavailable, available: false);
        Assert(TargetCompatibility.Evaluate(true, false, true, true, false, false,
                null, null, "notepad", processAvailable: false).Code ==
                TargetHealthCode.ProcessUnavailable,
            "Unknown target PID must prevent typing.");
        Check(TargetHealthCode.NotForeground, foreground: false);
        Check(TargetHealthCode.Minimized, minimized: true);
        Check(TargetHealthCode.NotResponding, hung: true);
        Check(TargetHealthCode.ElevatedTarget, targetElevation: true, currentElevation: false);
        Check(TargetHealthCode.Ready, targetElevation: true, currentElevation: true);
        Check(TargetHealthCode.Ready, targetElevation: null, currentElevation: false);
        Check(TargetHealthCode.Ready, targetElevation: false, currentElevation: null);

        var name = TargetCompatibility.Evaluate(true, false, true, true,
            false, false, false, false, "notepad<>:/secret/title");
        Assert(name.ProcessLabel == "notepadsecrettitle",
            "Process labels should strip path/title punctuation.");
        Assert(!name.Summary.Contains("secret", StringComparison.OrdinalIgnoreCase),
            "No target title data must leak into the summary.");

        var noName = TargetCompatibility.Evaluate(true, false, true, true,
            false, false, null, null, null);
        Assert(noName.ProcessLabel.Contains("Неизвестное"), "Unknown process state.");
        return Task.CompletedTask;
    }

    private static Task SessionReportValidation()
    {
        var interrupted = TypingSessionReport.Create(TypingOutcome.Interrupted,
            processed: 25, total: 100, TimeSpan.FromSeconds(9.25), "Code<>/Sensitive.cs");
        Assert(interrupted.Processed == 25 && interrupted.Total == 100,
            "Partial progress should remain visible.");
        Assert(interrupted.Title.Contains("прерван"), "Interruption report title.");
        Assert(interrupted.Guidance.Contains("Отпустите"), "Interruption recovery action.");
        Assert(interrupted.Application == "CodeSensitive.cs",
            "Process label sanitized; never expose filenames or title fields.");
        Assert(!interrupted.ClipboardText.Contains("Sensitive.cs") ||
               interrupted.Application == "CodeSensitive.cs",
               "No user document content should be appended to the report.");

        var complete = TypingSessionReport.Create(TypingOutcome.Completed, 110,
            80, TimeSpan.FromSeconds(18), "notepad");
        Assert(complete.Processed == 80, "Progress clamped to total.");
        Assert(complete.Guidance.Contains("сравните"), "Do not claim destination acceptance.");
        Assert(complete.ClipboardText.Contains("80 из 80"), "Copyable summary has progress.");
        Assert(complete.Outcome == TypingOutcome.Completed, "Completed classification.");

        var stopped = TypingSessionReport.Create(TypingOutcome.Cancelled,
            -50, -1, TimeSpan.FromSeconds(-1), null);
        Assert(stopped.Processed == 0 && stopped.Total == 0,
            "Negative counts clamped.");
        Assert(stopped.Duration == TimeSpan.Zero, "Negative duration clamped.");
        Assert(stopped.Guidance.Contains("часть текста"),
            "Stopped session must warn about partial content.");
        return Task.CompletedTask;
    }

    private static async Task UnexpectedModifiers()
    {
        foreach (var mode in new[] { TypingMode.Text, TypingMode.Code })
        {
            var device = new BufferDevice();
            var engine = Engine(device, 102);
            device.OnEvent = () =>
            {
                if (device.Events == 12) device.ModifiersHeld = true;
            };
            string? diagnostic = null;
            engine.StatusChanged += message => diagnostic = message;
            await engine.StartAsync(new string('a', 80), mode, Profile(0m),
                Window, mode == TypingMode.Code ? "test.cs" : null);
            Assert(device.Events == 12,
                $"Unintended modifiers must interrupt before the next event: {mode}.");
            Assert(engine.LastOutcome == TypingOutcome.Interrupted,
                $"Interrupted outcome for {mode}.");
            Assert(diagnostic is not null && diagnostic.Contains("зажаты"),
                "User should see modifier interruption reason.");
            Assert(!engine.IsRunning && !engine.IsPaused,
                "Interrupted session must release engine state.");
        }
    }

    private static async Task InterruptedSessionRestart()
    {
        var device = new BufferDevice();
        var engine = Engine(device, 5);
        device.OnEvent = () =>
        {
            if (device.Events == 8) device.ModifiersHeld = true;
        };
        await engine.StartAsync(new string('a', 35), TypingMode.Text,
            Profile(0m), Window);
        Assert(engine.LastOutcome == TypingOutcome.Interrupted,
            "First interrupted attempt.");

        device.ModifiersHeld = false;
        device.OnEvent = null;
        var firstLength = device.Text.Length;
        await engine.StartAsync("new session", TypingMode.Text,
            Profile(0m), Window);
        Assert(engine.LastOutcome == TypingOutcome.Completed,
            "Fresh session should complete after interference ends.");
        Assert(device.Text[firstLength..] == "new session",
            "Restart should only type the new content, not auto-repeat partial text.");
    }

    private static async Task ForegroundLoss()
    {
        var device = new BufferDevice { ChangeFocusAfter = 10 };
        var engine = Engine(device);
        string? status = null;
        engine.StatusChanged += s => status = s;
        await engine.StartAsync(new string('a', 60), TypingMode.Text, Profile(), Window);
        Assert(device.Events == 10, $"Expected 10 events, got {device.Events}.");
        Assert(engine.LastOutcome == TypingOutcome.FocusLost, "Foreground loss outcome");
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
        Assert(engine.LastOutcome == TypingOutcome.Cancelled, "Stopped outcome");
        Assert(engine.LastProcessedCharacters >= 0 &&
               engine.LastProcessedCharacters <= engine.LastTotalCharacters,
            "Cancelled session must retain safe progress bounds.");
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

    private static Task HumanizerTiming()
    {
        var p = Profile(0m);
        p.HumanizerStrength = 100;
        p.KeyTravelInfluence = 100;
        p.RhythmMemoryPercent = 90;
        p.MicroHesitationPercent = 0;
        p.SpeedVariationPercent = 80;
        p.BurstPercent = 40;
        p.Validate();

        var one = new HumanMotorModel(p, new Random(8842));
        var two = new HumanMotorModel(p, new Random(8842));
        var previous = '\0';
        const string passage = "The quick brown fox jumps over the lazy dog. Привет мир! 123";
        var output = new List<int>();
        foreach (var c in passage)
        {
            var a = one.NextDelay(c, previous, 8);
            var b = two.NextDelay(c, previous, 8);
            Assert(a == b, "Seeded motor model must be reproducible.");
            Assert(a is >= 12 and <= 3500, "Every interval must be bounded.");
            output.Add(a);
            previous = c;
        }
        Assert(output.Distinct().Count() >= 14,
            "Realistic motor model must not produce constant intervals.");
        one.OnRest(1200);
        two.OnRest(1200);
        Assert(one.NextDelay('x', 'e', 4) == two.NextDelay('x', 'e', 4),
            "Recovery from rest must be deterministic.");

        var leftRight = HumanMotorModel.TransitionFactor('f', 'j', 100);
        var neutral = HumanMotorModel.TransitionFactor('f', 'j', 0);
        Assert(neutral == 1.0, "Zero travel sensitivity means neutral motor factor.");
        Assert(Math.Abs(leftRight - neutral) > 0.01,
            "Hand alternation and key distance should influence timing.");
        Assert(HumanMotorModel.TransitionFactor('a', 'a', 100) >
               HumanMotorModel.TransitionFactor('a', 's', 100),
            "Repeated keys need distinct finger-reset timing.");
        Assert(HumanMotorModel.TransitionFactor('a', 'a', 0) == 1,
            "Physical distance effect is optional.");
        return Task.CompletedTask;
    }

    private static Task HumanizerKeyboard()
    {
        foreach (var layout in new[] { "qwertyuiopasdfghjklzxcvbnm",
                     "йцукенгшщзхъфывапролджэячсмитьбюё" })
        {
            for (var i = 0; i < 500; i++)
            {
                var c = layout[i % layout.Length];
                var typo = HumanMotorModel.NearbyLetter(c, new Random(i * 171 + 91));
                Assert(c != typo, "Wrong letter must differ from intended key.");
                Assert(layout.Contains(typo),
                    "Nearby typo must stay in the same writing system.");
                Assert(HumanMotorModel.TryLocate(c, out var original),
                    "Source letter must have a keyboard location.");
                Assert(HumanMotorModel.TryLocate(typo, out var neighbor),
                    "Nearby typo must have a keyboard location.");
                Assert(original.Layout == neighbor.Layout, "Physical keyboard mapping.");
                var dx = original.X - neighbor.X;
                var dy = original.Y - neighbor.Y;
                Assert(dx * dx + dy * dy <= 2.9,
                    "Generated typo must be a nearby physical key.");
            }
        }
        Assert(char.IsUpper(HumanMotorModel.NearbyLetter('Щ', new Random(44))),
            "Shifted characters must preserve case.");
        Assert(HumanMotorModel.NearbyLetter('f', new Random(3)) ==
               HumanMotorModel.NearbyLetter('f', new Random(3)),
            "Typo choices must be reproducible.");
        return Task.CompletedTask;
    }

    private static Task HumanizerContexts()
    {
        var p = Profile(0m);
        p.HumanizerStrength = 100;
        p.MicroHesitationPercent = 35;
        p.KeyTravelInfluence = 100;
        p.RhythmMemoryPercent = 96;
        p.Validate();
        var motor = new HumanMotorModel(p, new Random(22));
        Assert(motor.NextHesitation('b', 'a', false) == 0,
            "No cognitive hesitation for ordinary familiar passage.");
        var pauses = new List<int>();
        for (var i = 0; i < 1000; i++)
        {
            var ms = motor.NextHesitation('{', 'x', true);
            Assert(ms is >= 0 and <= 850,
                "Context microhesitations must never block for unbounded time.");
            if (ms > 0) pauses.Add(ms);
        }
        Assert(pauses.Count >= 40 && pauses.Count <= 500,
            "Code punctuation should have infrequent, not uniform, thought pauses.");
        Assert(pauses.Distinct().Count() > 15,
            "Cognitive pauses must vary rather than use one fixed length.");

        p.MicroHesitationPercent = 0;
        var disabled = new HumanMotorModel(p, new Random(91));
        for (var i = 0; i < 100; i++)
            Assert(disabled.NextHesitation('(', 'a', true) == 0,
                "Zero microhesitation setting must strictly disable the feature.");

        var errors = new HumanMotorModel(p, new Random(25));
        Assert(Enumerable.Range(0, 1000).All(_ =>
            !errors.ShouldMistype('a', 'f', 8, 0m)),
            "Zero typo percentage must always disable deliberate mistakes.");

        var types = new int[4];
        for (var i = 0; i < 2000; i++) types[errors.NextErrorKind()]++;
        Assert(types[0] > types[1] && types[1] > types[3],
            "Substitutions should dominate rare transpositions.");
        Assert(types.All(x => x > 0), "All supported corrections still occur.");

        p.HumanizerStrength = -1;
        p.KeyTravelInfluence = 999;
        p.RhythmMemoryPercent = 100;
        p.MicroHesitationPercent = 999;
        p.Validate();
        Assert(p.HumanizerStrength == 0 && p.KeyTravelInfluence == 100 &&
               p.RhythmMemoryPercent == 96 && p.MicroHesitationPercent == 35,
               "Every new parameter must be clamped safely.");
        return Task.CompletedTask;
    }

    private static Task HumanizerMigration()
    {
        const string legacyJson = """
            {"SettingsSchemaVersion":3,"SelectedProfile":"Legacy human",
             "Profiles":[{"Name":"Legacy human","Style":0,"CharactersPerMinute":317,
                          "TypoPercent":3.5,"LookbackPercent":9,"CodeReviewPercent":5}]}
            """;
        var config = JsonSerializer.Deserialize<AppSettings>(legacyJson)!;
        config.Validate();
        Assert(config.SettingsSchemaVersion == 4,
            "Schema v3 profiles should migrate once to v4.");
        Assert(config.SelectedProfile == "Legacy human" &&
               config.GetProfile().CharactersPerMinute == 317 &&
               config.GetProfile().LookbackPercent == 9,
            "Migration cannot reset customized typing behavior.");
        Assert(config.GetProfile().HumanizerStrength == 80,
            "Legacy profiles use safe new motor defaults.");
        Assert(config.Profiles.Any(x => x.Name == "Естественный ритм"),
            "A new natural profile must be selectable after migration.");
        var count = config.Profiles.Count;
        config.Validate();
        Assert(count == config.Profiles.Count, "Repeated validation must be idempotent.");
        var natural = AppSettings.NaturalPreset();
        Assert(natural.HumanizerStrength >= 90 &&
               natural.MicroHesitationPercent > 0 &&
               natural.KeyTravelInfluence >= 80,
            "Natural preset must prioritize humanlike rhythm.");
        return Task.CompletedTask;
    }

    private static async Task HumanizerEndToEnd()
    {
        const string text = "Привет, world! This is an interesting sentence: 42.\n" +
            "LongLongLongWords and короткиеслова, accents йцукен ёЁ 🙂.\n" +
            "Next paragraph for authentic pacing and corrective gestures.";
        const string code = "using System;\nclass Humanizer {\n" +
            "    static string Name(int number) => \"value \" + number;\n" +
            "    // A comment with a bilingual word: проверка\n}\n";

        for (var style = 1; style <= 4; style++)
        for (var seed = 0; seed < 30; seed++)
        {
            var p = AppSettings.MakeStylePreset("Humanizer seed", (HumanStyle)style);
            p.HumanizerStrength = 100;
            p.MicroHesitationPercent = 30;
            p.KeyTravelInfluence = 100;
            p.TypoPercent = 19m;
            p.AttentionBreakPercent = 70;
            p.FocusMinCharacters = 6;
            p.FocusMaxCharacters = 15;
            p.LookbackPercent = 12;
            p.CodeReviewPercent = 12;
            p.Validate();

            var a = new BufferDevice();
            var b = new BufferDevice();
            var textEngine = Engine(a, seed * 11 + style);
            var codeEngine = Engine(b, seed * 19 + style * 31);
            await textEngine.StartAsync(text, TypingMode.Text, p, Window);
            await codeEngine.StartAsync(code, TypingMode.Code, p, Window, "Humanizer.cs");
            Equal(text, a.Text, $"Humanizer text {style}/{seed}");
            Equal(code, b.Text, $"Humanizer code {style}/{seed}");
            Assert(a.Cursor == a.Text.Length && b.Cursor == b.Text.Length,
                "Humanizer review must return caret to exact end.");
            Assert(textEngine.LastOutcome == TypingOutcome.Completed &&
                   codeEngine.LastOutcome == TypingOutcome.Completed,
                "Humanizer must not silently fail a seeded session.");
        }
    }

    private static Task HumanStylePresets()
    {
        var cps = new List<int>();
        foreach (var style in new[]
        {
            HumanStyle.Beginner, HumanStyle.Casual, HumanStyle.Developer,
            HumanStyle.Professional
        })
        {
            var p = AppSettings.MakeStylePreset("Test", style);
            Assert(p.Style == style, "Style identity should persist.");
            Assert(p.FocusMinCharacters <= p.FocusMaxCharacters, "Window bounds.");
            Assert(p.LookbackPercent > 0, "Style should enable reversible reviews.");
            cps.Add(p.CharactersPerMinute);
        }
        Assert(cps.SequenceEqual(cps.OrderBy(x => x)), "CPM progresses with experience.");

        var original = new TypingProfile
        {
            CharactersPerMinute = 777, TypoPercent = 0m,
            LookbackPercent = 19, Style = HumanStyle.Custom
        };
        original.ApplyStyle(HumanStyle.Custom);
        Assert(original.CharactersPerMinute == 777 && original.LookbackPercent == 19,
            "Custom must not wipe manual settings.");

        var settings = new AppSettings
        {
            SettingsSchemaVersion = 2,
            SelectedProfile = "My custom",
            Profiles = [new TypingProfile { Name = "My custom",
                CharactersPerMinute = 777, Style = HumanStyle.Custom }]
        };
        settings.Validate();
        Assert(settings.SettingsSchemaVersion == 4, "Schema version");
        Assert(settings.SelectedProfile == "My custom" &&
               settings.GetProfile().CharactersPerMinute == 777, "Custom profile retained");
        Assert(Enum.GetNames<HumanStyle>().Skip(1).All(n =>
            settings.Profiles.Any(x => x.Name == n)), "Preset profiles created");
        var nProfiles = settings.Profiles.Count;
        settings.Validate();
        Assert(settings.Profiles.Count == nProfiles, "Migration must not duplicate.");
        return Task.CompletedTask;
    }

    private static Task HumanFocus()
    {
        var profile = Profile(0);
        profile.FocusMinCharacters = 5;
        profile.FocusMaxCharacters = 5;
        profile.AttentionBreakPercent = 100;
        profile.AttentionBreakMinMilliseconds = 600;
        profile.AttentionBreakMaxMilliseconds = 600;
        profile.FocusRecoveryPercent = 50;
        profile.ThinkingPausePercent = 100;
        profile.ThinkingPauseMinMilliseconds = 400;
        profile.ThinkingPauseMaxMilliseconds = 400;
        profile.LookbackPercent = 100;
        profile.LookbackMaxCharacters = 7;
        var b = new HumanBehaviorModel(profile, new Random(42));
        var c = new HumanBehaviorModel(profile, new Random(42));
        for (var i = 0; i < 70; i++)
        {
            Assert(b.NextSpeedFactor() == c.NextSpeedFactor(), "Deterministic focus");
            Assert(b.NextAttentionBreakMilliseconds() ==
                   c.NextAttentionBreakMilliseconds(), "Deterministic break");
            Assert(b.NextThinkingPauseMilliseconds() == 400, "Thinking pause duration.");
            Assert(b.ShouldReview(), "Forced review.");
            Assert(b.NextLookbackLength(3) is >= 1 and <= 3, "Bounded caret review.");
            c.NextThinkingPauseMilliseconds();
            c.ShouldReview();
            c.NextLookbackLength(3);
        }
        return Task.CompletedTask;
    }

    private static async Task HumanLookbacks()
    {
        const string text =
            "Hello wonderful software developers! Привет замечательные программисты. " +
            "Test short words, longLongWordsForCarefulInspection and more.\n" +
            "Another sentence with 🙂 emoji and big word.\n\n" +
            "Several paragraphs.\n";
        const string code =
            "using System;\nclass Sample {\n    static string TestValue(int value) {\n" +
            "        // comment with braces { }\n        return \"example\" + value;\n    }\n}\n";
        for (var style = 0; style < 4; style++)
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var profile = AppSettings.MakeStylePreset("Test",
                    (HumanStyle)(style + 1));
                profile.CharactersPerMinute = 1200;
                profile.TypoPercent = 18m;
                profile.FocusMinCharacters = 5;
                profile.FocusMaxCharacters = 11;
                profile.AttentionBreakPercent = 100;
                profile.AttentionBreakMinMilliseconds = 5;
                profile.AttentionBreakMaxMilliseconds = 10;
                profile.LookbackPercent = 30;
                profile.LookbackMaxCharacters = 7;
                profile.LongPausePercent = 0m;
                profile.CodeReviewPercent = 20;
                var a = new BufferDevice();
                var b = new BufferDevice();
                await Engine(a, seed).StartAsync(text, TypingMode.Text, profile, Window);
                await Engine(b, seed).StartAsync(code, TypingMode.Code, profile,
                    Window, "sample.cs");
                Equal(text, a.Text, $"Text style={style} seed={seed}");
                Equal(code, b.Text, $"Code style={style} seed={seed}");
                Assert(a.Cursor == a.Text.Length && b.Cursor == b.Text.Length,
                    $"Caret style={style} seed={seed}");
            }
        }
    }

    private static async Task IdePipeRoundTrip()
    {
        using var bridge = new IdeBridgeServer();
        var invalidation = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.SessionInvalidated += session => invalidation.TrySetResult(session);
        bridge.Start();

        using var client = new NamedPipeClientStream(".",
            IdeBridgeServer.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(3500);
        using var reader = new StreamReader(client, new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
        using var writer = new StreamWriter(client, new UTF8Encoding(false), 8192,
            leaveOpen: true) { AutoFlush = true };

        var simulatedExtension = Task.Run(async () =>
        {
            var request = await reader.ReadLineAsync();
            Assert(request is not null, "Named pipe request missing");
            using var parsed = JsonDocument.Parse(request!);
            Assert(parsed.RootElement.GetProperty("type").GetString() == "ping",
                "Request command should match");
            var id = parsed.RootElement.GetProperty("id").GetInt64();
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                replyTo = id, ok = true, extension = "VS Code", version = 1
            }));
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                type = "invalidated", sessionId = "test-session"
            }));
        });

        for (var i = 0; i < 100 && !bridge.IsConnected; i++)
            await Task.Delay(20);
        Assert(bridge.IsConnected, "Bridge should accept same-user client");

        var response = await bridge.RequestAsync("ping", new { check = true },
            timeoutMilliseconds: 3000);
        Assert(response.GetProperty("ok").GetBoolean(), "Successful round-trip");
        Assert(response.GetProperty("extension").GetString() == "VS Code", "Extension identity");
        await simulatedExtension.WaitAsync(TimeSpan.FromSeconds(3));
        Assert(await invalidation.Task.WaitAsync(TimeSpan.FromSeconds(3)) == "test-session",
            "Session invalidation must reach native host");

        var isAvailable = bridge.IsConnected;
        Assert(isAvailable, "Bridge should stay connected while client is open");
    }

    private static Task IdeSettingsSafety()
    {
        var profile = new TypingProfile();
        Assert(profile.CodeIntegrity == CodeIntegrityMode.Off,
            "IDE verification must be opt-in for existing users");
        profile.CodeIntegrity = (CodeIntegrityMode)77;
        profile.Validate();
        Assert(profile.CodeIntegrity == CodeIntegrityMode.Off,
            "Invalid mode must fall back to Off");
        Assert(!VisualStudioDteSession.TryPrepare(IntPtr.Zero,
            out _, out var message), "Invalid target window must be rejected");
        Assert(message.Length > 0, "DTE rejection must be explained");
        return Task.CompletedTask;
    }

    private sealed class MockHotkeyBackend : IHotkeyRegistrationBackend
    {
        public readonly Dictionary<int, Keys> Active = [];
        public readonly HashSet<Keys> Blocked = [];
        public int RegisterCalls { get; private set; }
        public int UnregisterCalls { get; private set; }
        public bool Register(IntPtr window, int id, Keys keys)
        {
            RegisterCalls++;
            if (Blocked.Contains(keys) || Active.ContainsKey(id)) return false;
            Active[id] = keys;
            return true;
        }
        public void Unregister(IntPtr window, int id)
        {
            UnregisterCalls++;
            Active.Remove(id);
        }
    }

    private static Task StableHotkeyRollback()
    {
        var backend = new MockHotkeyBackend();
        var start = Keys.Control | Keys.Alt | Keys.F5;
        var pause = Keys.Control | Keys.Alt | Keys.F6;
        var stop = Keys.Control | Keys.Alt | Keys.F7;
        using (var service = new HotkeyRegistrationCoordinator(backend, new IntPtr(123)))
        {
            Assert(service.TryApply(start, pause, stop) && service.HasActiveSet,
                "First shortcut registration.");
            Assert(backend.Active.Count == 3, "All three shortcuts must be active.");
            var calls = backend.RegisterCalls;
            var removes = backend.UnregisterCalls;
            Assert(service.TryApply(start, pause, stop), "Unchanged settings remain valid.");
            Assert(backend.RegisterCalls == calls && backend.UnregisterCalls == removes,
                "Visual-settings change must not unregister active shortcuts.");

            var occupied = Keys.Control | Keys.Alt | Keys.F8;
            backend.Blocked.Add(occupied);
            Assert(!service.TryApply(start, pause, occupied),
                "Occupied shortcut should reject whole change.");
            Assert(backend.Active.Count == 3 && backend.Active[3] == stop,
                "Previous shortcut set should be restored transactionally.");

            backend.Blocked.Remove(occupied);
            Assert(service.TryApply(start, pause, occupied),
                "Retry after release should work.");
            Assert(backend.Active[3] == occupied,
                "New stop key must become active on successful update.");
            Assert(!service.TryApply(start, start, occupied),
                "Duplicate keys cannot be registered.");
            Assert(backend.Active[1] == start && backend.Active[3] == occupied,
                "Rejected duplicate must not affect existing hotkeys.");
        }
        Assert(backend.Active.Count == 0,
            "Shutdown must unregister exactly the active set.");
        return Task.CompletedTask;
    }

    private static Task StableCancelledStartup()
    {
        Exception? issue = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var main = new MainForm();
                var method = typeof(MainForm).GetMethod("StartNowAsync",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert(method is not null, "StartNowAsync must remain cancellable.");
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                var task = (Task?)method!.Invoke(main,
                    [new IntPtr(12345), cancelled.Token]);
                Assert(task is not null, "Cancellable start should return a task.");
                var rejected = false;
                try { task!.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { rejected = true; }
                Assert(rejected,
                    "A cancelled countdown must abort before target inspection or key injection.");
                var engineField = typeof(MainForm).GetField("_engine",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                var engine = (TypingEngine?)engineField?.GetValue(main);
                Assert(engine is not null && !engine.IsRunning,
                    "Cancelled startup must not create a running typing session.");
            }
            catch (Exception ex) { issue = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15)))
            throw new Exception("Cancelled startup STA smoke timed out.");
        if (issue is not null) throw new Exception("Cancelled startup failed.", issue);
        return Task.CompletedTask;
    }

    private static async Task StableSoak()
    {
        const string text =
            "Hello, developer. Привет, программист! " +
            "Разнообразныеслова ипроверки. Unicode 😀 € — ёЁ.\n" +
            "This is the second paragraph: 42 tests, commas, dots and values. " +
            "VeryLongIdentifiersAndCompoundWordsAreUseful.\n";
        const string code =
            "using System;\nnamespace Test {\n" +
            "    // Stable typing across comments and strings\n" +
            "    static class Code {\n" +
            "        static string Name(int count) => \"value-\" + count;\n" +
            "    }\n}\n";

        var seeds = 0;
        foreach (var style in new[] { HumanStyle.Casual, HumanStyle.Developer,
                     HumanStyle.Professional })
        {
            for (var seed = 0; seed < 45; seed++)
            {
                var profile = AppSettings.MakeStylePreset("Stable soak", style);
                profile.CharactersPerMinute = 1000;
                profile.TypoPercent = 9m;
                profile.LongPausePercent = 0;
                profile.AttentionBreakPercent = 9;
                profile.LookbackPercent = 10;
                profile.CodeReviewPercent = 8;
                profile.CodeLinePauseMilliseconds = 0;
                profile.CodeFunctionPauseMilliseconds = 0;
                profile.CodeBlockPauseMilliseconds = 0;
                profile.CodeSyntaxPauseMilliseconds = 0;

                var textDevice = new BufferDevice();
                var textEngine = Engine(textDevice, 1000 + seed * 7 + (int)style * 101);
                await textEngine.StartAsync(text, TypingMode.Text, profile, Window);
                Assert(textEngine.LastOutcome == TypingOutcome.Completed,
                    $"Text soak outcome: {style}, {seed}");
                Equal(text, textDevice.Text, $"Text soak output: {style}, {seed}");
                Assert(textEngine.LastProcessedCharacters == text.Length,
                    "Completed Text must report all source units.");

                var codeDevice = new BufferDevice();
                var codeEngine = Engine(codeDevice, 2000 + seed * 11 + (int)style * 101);
                await codeEngine.StartAsync(code, TypingMode.Code, profile, Window,
                    "stable.cs");
                Assert(codeEngine.LastOutcome == TypingOutcome.Completed,
                    $"Code soak outcome: {style}, {seed}");
                Equal(code, codeDevice.Text, $"Code soak output: {style}, {seed}");
                Assert(codeEngine.LastProcessedCharacters == code.Length,
                    "Completed Code must report all source units.");
                seeds++;
            }
        }
        Assert(seeds == 135, "Stable soak must run across expected random seeds.");
    }

    private static Task StableSettingsRecovery()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "KeyER-stable-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "settings.json");
        try
        {
            var fresh = SettingsStore.LoadFromPath(path);
            Assert(fresh.Origin == SettingsLoadOrigin.Defaults &&
                   !fresh.HasUnreadableFiles, "Fresh install must be a clean default.");
            var initial = fresh.Settings;
            initial.SelectedProfile = "Сбалансированный";
            SettingsStore.SaveToPath(path, initial);
            Assert(SettingsStore.LoadFromPath(path).Origin == SettingsLoadOrigin.Primary,
                "Saved settings should load from primary.");
            Assert(!File.Exists(path + ".bak"),
                "First save has no preceding version to back up.");

            var next = initial.Clone();
            next.SelectedProfile = "Переписка";
            next.EditorFontSize = 16;
            SettingsStore.SaveToPath(path, next);
            Assert(File.Exists(path + ".bak"), "Second save must retain good backup.");
            Assert(SettingsStore.LoadFromPath(path).Settings.SelectedProfile == "Переписка",
                "Latest primary should be read.");
            var stableBackup = File.ReadAllBytes(path + ".bak");

            File.WriteAllText(path, "{ not valid JSON :");
            var restored = SettingsStore.LoadFromPath(path);
            Assert(restored.Origin == SettingsLoadOrigin.Backup &&
                   restored.HasUnreadableFiles,
                   "Corrupt primary should load preserved valid fallback.");
            Assert(restored.Settings.SelectedProfile == "Сбалансированный",
                "Recovered profile should match last valid backup.");
            SettingsStore.SaveToPath(path, restored.Settings);
            Assert(SettingsStore.LoadFromPath(path).Origin == SettingsLoadOrigin.Primary,
                "A clean primary should be restored after save.");
            Assert(File.ReadAllBytes(path + ".bak").SequenceEqual(stableBackup),
                "Saving recovery must not overwrite a good .bak with corrupt JSON.");
            Assert(Directory.EnumerateFiles(folder, "settings.json.corrupt-*").Count() == 1,
                "Corrupt primary must be preserved for manual recovery.");
            Assert(!Directory.EnumerateFiles(folder, "*.tmp").Any(),
                "No residual temp after successful recovery.");
            return Task.CompletedTask;
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static Task StableSettingsProtection()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "KeyER-stable-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "settings.json");
        try
        {
            File.WriteAllText(path, "{\"SettingsSchemaVersion\":?");
            File.WriteAllText(path + ".bak", "also invalid");
            var state = SettingsStore.LoadFromPath(path);
            Assert(state.Origin == SettingsLoadOrigin.Defaults &&
                   state.HasUnreadableFiles, "Both corrupt files should not crash startup.");
            SettingsStore.SaveToPath(path, state.Settings);
            Assert(SettingsStore.LoadFromPath(path).Origin == SettingsLoadOrigin.Primary,
                "Save from defaults should produce valid primary.");
            Assert(File.ReadAllText(path + ".bak") == "also invalid",
                "Keep original backup data until a valid next generation exists.");
            Assert(Directory.EnumerateFiles(folder, "settings.json.corrupt-*").Any(),
                "Preserve broken primary for debugging and user recovery.");

            var second = state.Settings.Clone();
            second.EditorFontSize = 15;
            SettingsStore.SaveToPath(path, second);
            var backed = SettingsStore.LoadFromPath(path + ".bak");
            Assert(backed.Origin == SettingsLoadOrigin.Primary &&
                   backed.Settings.EditorFontSize == state.Settings.EditorFontSize,
                "A valid previous version should replace invalid backup on next save.");
            return Task.CompletedTask;
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static Task StableMigration()
    {
        var old = new AppSettings
        {
            SettingsSchemaVersion = 1,
            SelectedProfile = "My custom style",
            Profiles =
            [
                new TypingProfile
                {
                    Name = "My custom style",
                    CharactersPerMinute = 327,
                    TypoPercent = 4.3m,
                    TextEnter = TextEnterMode.ShiftEnter
                }
            ]
        };
        old.Validate();
        Assert(old.SettingsSchemaVersion == 4,
            "All older settings schemas must be migrated to current format.");
        Assert(old.SelectedProfile == "My custom style",
            "Selected custom profile must be preserved.");
        Assert(old.GetProfile().CharactersPerMinute == 327 &&
               old.GetProfile().TextEnter == TextEnterMode.ShiftEnter,
            "Custom speed and Enter mode must survive migration.");
        Assert(old.Profiles.Any(p => p.Name == "Переписка") &&
               old.Profiles.Any(p => p.Name == "Developer"),
            "Missing default Text and Human profiles must be added once.");
        var count = old.Profiles.Count;
        old.Validate();
        Assert(old.Profiles.Count == count,
            "Running validation again must not duplicate migrated profiles.");
        return Task.CompletedTask;
    }

    private static Task BetaSingleInstance()
    {
        var name = @"Local\KeyER.Beta.Regression." + Guid.NewGuid().ToString("N");
        var first = SingleInstanceGuard.TryAcquire(name);
        Assert(first is not null, "First instance should acquire the mutex.");
        try
        {
            using var second = SingleInstanceGuard.TryAcquire(name);
            Assert(second is null, "Second instance must not acquire the mutex.");
        }
        finally { first!.Dispose(); }

        using var reopened = SingleInstanceGuard.TryAcquire(name);
        Assert(reopened is not null,
            "The same singleton slot must be available after normal shutdown.");
        return Task.CompletedTask;
    }

    private static Task BetaAtomicWrite()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "KeyER-beta-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "unicode.txt");
            AtomicDocumentWriter.WriteText(path, "Привет\nHello 🙂");
            Assert(File.ReadAllText(path) == "Привет\nHello 🙂",
                "Initial UTF-8 document must roundtrip.");
            Assert(!File.ReadAllBytes(path).Take(3)
                    .SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }),
                "No UTF-8 BOM should be inserted.");

            AtomicDocumentWriter.WriteText(path, "New content \u20ac\n");
            Assert(File.ReadAllText(path) == "New content \u20ac\n",
                "Existing file must be atomically overwritten.");
            Assert(!Directory.GetFiles(directory, "*.tmp").Any(),
                "Temporary files should be cleaned after successful writes.");

            var failed = false;
            try { AtomicDocumentWriter.WriteText(directory, "must not replace directory"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { failed = true; }
            Assert(failed, "Writing to an existing directory must fail.");
            Assert(File.ReadAllText(path) == "New content \u20ac\n",
                "A failed write must preserve previously saved content.");
            Assert(!Directory.GetFiles(directory, "*.tmp").Any(),
                "Failed write should not leave temp files behind.");
        }
        finally { Directory.Delete(directory, recursive: true); }
        return Task.CompletedTask;
    }

    private static Task BetaSupportPrivacy()
    {
        string[] entries =
        [
            "2026-11-22T23:17:11.2288737+00:00 ERROR typing IOException HRESULT=0x80070057",
            "2026-11-22T23:17:12.0000000+00:00 ERROR settings-save JsonException HRESULT=0x80131500",
            "2026-11-22T23:17:13.0000000+00:00 ERROR typing /Users/secret/password=123",
            "user sensitive source code",
            "2026-11-22T23:17:14.0000000+00:00 ERROR component SomeException HRESULT=0x80004005\nSECRET"
        ];
        var filtered = BetaSupportReport.FilterLogEntries(entries);
        Assert(filtered.Count == 2, "Only strictly formatted log entries can be exported.");
        Assert(filtered.All(x => !x.Contains("secret", StringComparison.OrdinalIgnoreCase)),
            "Sanitized log entries must exclude fake private content.");
        Assert(BetaSupportReport.FilterLogEntries(entries, 1).Count == 1,
            "Log export should honor its bounded size.");
        var settings = new AppSettings();
        settings.Validate();
        var report = BetaSupportReport.Build(settings, entries);
        Assert(report.Contains("support report"), "Report has support identity.");
        Assert(report.Contains("IOException") && report.Contains("JsonException"),
            "Report preserves safe failure categories.");
        Assert(!report.Contains("password") && !report.Contains("sensitive source"),
            "No user content can be copied into a support report.");
        Assert(!report.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase),
            "Do not export the Windows account name.");
        return Task.CompletedTask;
    }

    private static Task StudioAppearanceValidation()
    {
        Theme.Configure(AppearanceMode.Dark, AccentTheme.Iris);
        var darkBackground = Theme.Background;
        var darkAccent = Theme.Accent;
        Theme.Configure(AppearanceMode.Light, AccentTheme.Iris);
        Assert(Theme.Background != darkBackground, "Dark and light palettes must differ.");
        Assert(Theme.Accent != darkAccent, "Accent should be contrast-adjusted in light mode.");
        Theme.Configure(AppearanceMode.Dark, AccentTheme.Mint);
        Assert(Theme.Accent != darkAccent, "Changing accent should update the palette.");

        var invalid = new AppSettings
        {
            Appearance = (AppearanceMode)99,
            Accent = (AccentTheme)99,
            EditorFontSize = 100,
            WindowWidth = 100,
            WindowHeight = 100
        };
        invalid.Validate();
        Assert(invalid.Appearance == AppearanceMode.Dark, "Invalid appearance reset.");
        Assert(invalid.Accent == AccentTheme.Iris, "Invalid accent reset.");
        Assert(invalid.EditorFontSize == 18, "Font clamped.");
        Assert(invalid.WindowWidth >= 760 && invalid.WindowHeight >= 520, "Valid window geometry.");
        return Task.CompletedTask;
    }

    private static Task StudioSearch()
    {
        var finder = new SnippetSearch();
        const string sample = "Alpha alpha ALPHA Привет привет 🙂🙂";
        finder.Update(sample, "alpha", matchCase: false);
        Assert(finder.Offsets.Count == 3, "Case-insensitive count.");
        Assert(finder.Next(0, true, includeCurrent: true) == 0, "Include current match.");
        Assert(finder.Next(0, true) == 6, "Next match.");
        Assert(finder.Next(20, true) == 0, "Wrap forward.");
        Assert(finder.Next(0, false) == 12, "Wrap backward.");
        Assert(finder.OrdinalAt(6) == 2, "Current match ordinal.");

        finder.Update(sample, "ALPHA", matchCase: true);
        Assert(finder.Offsets.Count == 1 && finder.Offsets[0] == 12,
            "Case-sensitive exact match.");

        finder.Update(sample, "ПРИВЕТ", matchCase: false);
        Assert(finder.Offsets.Count == 2, "Cyrillic case fold.");

        finder.Update(sample, "🙂", matchCase: true);
        Assert(finder.Offsets.Count == 2 && finder.Length == 2,
            "Unicode surrogate-pair offsets.");

        finder.Update(sample, "", matchCase: false);
        Assert(finder.Offsets.Count == 0 && finder.Next(0, true) == -1,
            "Empty query never selects text.");

        finder.Update(new string('x', 22000), "x", matchCase: true);
        Assert(finder.Offsets.Count == 10000 && finder.Truncated,
            "Search must cap matches on huge documents.");
        return Task.CompletedTask;
    }

    private static Task StudioUiSmoke()
    {
        Exception? issue = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var main = new MainForm();
                Assert(main.Width >= 785 && main.Height >= 560, "Studio window dimensions.");

                var editor = Descendants(main).OfType<RichTextBox>().Single();
                Assert(editor.AcceptsTab && !editor.WordWrap, "Editor usability.");
                Assert(Descendants(main).OfType<EditorGutter>().Count() == 1,
                    "Dedicated line-number gutter.");
                Assert(Descendants(main).OfType<StudioButton>().Count() >= 10,
                    "Owner-painted primary and quick actions.");
                Assert(Descendants(main).OfType<StudioProgress>().Count() == 1,
                    "Custom progress bar.");
                Assert(Descendants(main).OfType<StudioButton>().Any(b =>
                    b.Text.Contains("окно", StringComparison.OrdinalIgnoreCase)),
                    "Target compatibility check must be visible.");
                Assert(Descendants(main).OfType<StudioButton>().Any(b =>
                    b.Text.Contains("Отчёт", StringComparison.Ordinal)),
                    "In-memory session report must be discoverable.");
                Assert(Descendants(main).OfType<ComboBox>().Any(),
                    "Profile and language selectors.");
                Assert(Descendants(main).OfType<StudioButton>().Any(b =>
                    b.Text.Contains("Сохранить", StringComparison.Ordinal)),
                    "Source Save As action exists.");
                Assert(Descendants(main).OfType<Label>().Any(l =>
                    l.Text.Contains("ПЕРЕД ЗАПУСКОМ", StringComparison.Ordinal)),
                    "Readiness guidance visible in sidebar.");
                Assert(Descendants(main).OfType<TextBox>().Any(t =>
                    t.PlaceholderText.Contains("Поиск", StringComparison.Ordinal)),
                    "Improved Find bar present.");
                var onboarding = Descendants(main).OfType<Panel>().FirstOrDefault(x =>
                    x.AccessibleName == "Подготовка исходного текста");
                Assert(onboarding is not null, "First-run onboarding should cover the empty editor.");
                Assert(Descendants(onboarding!).OfType<StudioButton>().Any(b =>
                    b.Text.Contains("Вставить", StringComparison.Ordinal)), "Onboarding paste action.");
                Assert(Descendants(onboarding!).OfType<StudioButton>().Any(b =>
                    b.Text.Contains("Открыть", StringComparison.Ordinal)), "Onboarding file action.");

                using (var reportWindow = new SessionReportForm(
                    TypingSessionReport.Create(TypingOutcome.FocusLost, 10, 60,
                        TimeSpan.FromSeconds(2), "notepad")))
                {
                    Assert(Descendants(reportWindow).OfType<StudioButton>().Count() >= 2,
                        "Themed report has copy and close actions.");
                }

                using var settings = new SettingsForm(new AppSettings());
                Assert(Descendants(settings).OfType<StudioButton>().Count() >= 6,
                    "Settings side navigation and save controls.");
                Assert(Descendants(settings).OfType<NumericUpDown>().Any(),
                    "Editable profile parameters must remain available.");
                Assert(Descendants(settings).OfType<Label>().Any(l =>
                    l.Text.Contains("Humanizer", StringComparison.Ordinal)),
                    "Humanizer 3.0 settings must be discoverable.");
                Assert(Descendants(settings).OfType<StudioThemePreview>().Count() == 1,
                    "Live theme preview must exist.");
                Assert(Descendants(settings).OfType<StudioButton>().Any(b =>
                    b.Text.Contains("Диагностика", StringComparison.Ordinal)),
                    "Beta support center must be discoverable.");
                Assert(Descendants(settings).OfType<TextBox>().Any(t =>
                    t.PlaceholderText.Contains("Поиск параметра", StringComparison.Ordinal)),
                    "Settings search must be discoverable.");

                using (var theme = new StudioThemePreview
                {
                    Size = new Size(400, 196), PreviewAppearance = AppearanceMode.Dark
                })
                using (var first = new Bitmap(400, 196))
                using (var second = new Bitmap(400, 196))
                {
                    theme.DrawToBitmap(first, new Rectangle(0, 0, 400, 196));
                    theme.PreviewAppearance = AppearanceMode.Light;
                    theme.DrawToBitmap(second, new Rectangle(0, 0, 400, 196));
                    Assert(first.GetPixel(40, 45) != second.GetPixel(40, 45),
                        "Preview must change colors without saving settings.");
                }

                editor.Text = "using System;\nclass Example { int Count = 7; }";
                Assert(editor.Text.Contains("Example"), "Rich editor should accept input.");
                using var painter = new SyntaxPainter(editor)
                {
                    IsCode = true, Enabled = true, FileName = "test.cs"
                };
                _ = editor.Handle; // Ensure the native RichEdit control exists on STA.
                painter.Render();
                editor.Select(0, 5);
                Assert(editor.SelectionColor != Theme.Foreground,
                    "C# 'using' should be syntax highlighted.");
            }
            catch (Exception e) { issue = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(18)))
            throw new Exception("Studio UI smoke test timed out.");
        if (issue is not null) throw new Exception("Studio UI smoke failure.", issue);
        return Task.CompletedTask;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
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
