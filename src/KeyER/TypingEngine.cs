namespace KeyER;

public sealed class TargetWindowChangedException : Exception
{
    public TargetWindowChangedException() : base("Активное окно изменилось. Набор остановлен во избежание ввода не туда.") { }
}

/// <summary>
/// Simulates typing in the chosen foreground window. It never reads or modifies
/// the target application's document through an API or the clipboard.
/// </summary>
public sealed class TypingEngine
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _paused;

    public event Action<int, int>? ProgressChanged;
    public event Action<string>? StatusChanged;
    public bool IsRunning { get { lock (_gate) return _cts is not null; } }
    public bool IsPaused { get { lock (_gate) return _paused; } }

    public async Task StartAsync(string content, TypingMode mode, TypingProfile sourceProfile, IntPtr target)
    {
        if (string.IsNullOrEmpty(content)) throw new ArgumentException("Нет текста для ввода.");
        if (target == IntPtr.Zero || !Native.IsWindow(target) || Native.IsOwnWindow(target))
            throw new InvalidOperationException("Выберите окно другого приложения и установите курсор.");

        var profile = sourceProfile.Clone();
        profile.Validate();
        CancellationTokenSource local;
        lock (_gate)
        {
            if (_cts is not null) throw new InvalidOperationException("Набор уже запущен.");
            local = new CancellationTokenSource();
            _cts = local;
            _paused = false;
        }

        try
        {
            StatusChanged?.Invoke("Набор текста...");
            await Task.Run(() => RunAsync(content, mode, profile, target, local.Token));
            StatusChanged?.Invoke("Готово. Ввод завершён.");
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke("Остановлено пользователем.");
        }
        catch (TargetWindowChangedException ex)
        {
            StatusChanged?.Invoke(ex.Message);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Ошибка ввода: " + ex.Message);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cts, local))
                {
                    _cts = null;
                    _paused = false;
                }
            }
            local.Dispose();
        }
    }

    public void TogglePause()
    {
        lock (_gate)
        {
            if (_cts is null) return;
            _paused = !_paused;
            StatusChanged?.Invoke(_paused ? "Пауза. Вернитесь в целевое окно для продолжения." : "Продолжение...");
        }
    }

    public void Stop()
    {
        lock (_gate) _cts?.Cancel();
    }

    private async Task RunAsync(string source, TypingMode mode, TypingProfile profile,
        IntPtr target, CancellationToken token)
    {
        source = source.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = source.Split('\n');
        var total = source.Length;
        var done = 0;
        var random = new Random();
        var inBurst = false;
        var burstRemaining = 0;

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            await EnsureActiveAsync(target, token);
            if (lineIndex > 0)
            {
                Native.PressKey(Keys.Enter);
                done++;
                ProgressChanged?.Invoke(done, total);
                await DelayAsync('\n', done, profile, mode, random, false, token);

                if (mode == TypingMode.Code && profile.ClearAutoIndent && lines[lineIndex].Length > 0)
                {
                    // Select auto-inserted whitespace or brace on the NEW line.
                    // The next typed character replaces the selection. Do not press
                    // Backspace: that would merge lines when the line is empty.
                    Native.PressKey(Keys.Home);
                    Native.PressKey(Keys.Home); // handles "smart Home" navigation
                    Native.PressChord(Keys.ShiftKey, Keys.End);
                }
            }

            var line = lines[lineIndex];
            for (int i = 0; i < line.Length;)
            {
                await EnsureActiveAsync(target, token);
                var c = line[i];

                if (burstRemaining-- <= 0)
                {
                    inBurst = random.Next(100) < profile.BurstPercent;
                    burstRemaining = random.Next(6, 22);
                }

                if (CanMistype(c) && random.NextDouble() * 100 < (double)profile.TypoPercent)
                {
                    var used = await MistypeAndCorrectAsync(line, i, done, profile, mode, target,
                        random, inBurst, token);
                    i += used;
                    done += used;
                }
                else
                {
                    SendCharacter(c);
                    await DelayAsync(c, done, profile, mode, random, inBurst, token);
                    i++;
                    done++;
                }

                // No unbounded UI event stream for very large documents.
                if (done % 4 == 0 || i >= line.Length)
                    ProgressChanged?.Invoke(done, total);
            }

            // A harmless line-review gesture only on ordinary, unpaired text.
            // Code editors may have inserted closing brackets, so avoid this for
            // lines with syntactic delimiters.
            if (line.Length > 0 &&
                random.Next(100) < profile.CursorReviewPercent &&
                (mode == TypingMode.Text || line.IndexOfAny("()[]{}\"'".ToCharArray()) < 0))
            {
                await EnsureActiveAsync(target, token);
                Native.PressKey(Keys.Home);
                await Task.Delay(random.Next(80, 220), token);
                await EnsureActiveAsync(target, token);
                Native.PressKey(Keys.End);
            }
        }

        ProgressChanged?.Invoke(total, total);
    }

    private async Task<int> MistypeAndCorrectAsync(string line, int index, int done,
        TypingProfile profile, TypingMode mode, IntPtr target, Random random, bool burst,
        CancellationToken token)
    {
        // Only plain letters are considered. Delayed corrections are constrained
        // to a single contiguous word, so cursor moves never cross a line break,
        // indentation, brace or other IDE-sensitive punctuation.
        var available = 0;
        var limit = Math.Min(profile.CorrectionLagCharacters, line.Length - index - 1);
        while (available < limit && CanMistype(line[index + 1 + available])) available++;
        var lag = available > 0 && random.Next(100) < profile.LateCorrectionPercent
            ? random.Next(1, available + 1) : 0;

        var kind = random.Next(4);
        if (kind == 3 && (index + 1 >= line.Length || !CanMistype(line[index + 1])))
            kind = 0;

        var consumed = 1;
        switch (kind)
        {
            case 0: // substitution
                SendCharacter(WrongLetter(line[index], random));
                break;
            case 1: // omission: don't type the character at all
                break;
            case 2: // duplicate
                SendCharacter(line[index]);
                await DelayAsync(line[index], done, profile, mode, random, burst, token);
                SendCharacter(line[index]);
                break;
            default: // adjacent transposition
                SendCharacter(line[index + 1]);
                await DelayAsync(line[index + 1], done, profile, mode, random, burst, token);
                SendCharacter(line[index]);
                consumed = 2;
                // Don't retype the second letter among the delayed chars.
                lag = Math.Max(0, lag - 1);
                break;
        }

        await DelayAsync(line[index], done, profile, mode, random, burst, token);

        for (int j = 0; j < lag; j++)
        {
            await EnsureActiveAsync(target, token);
            var c = line[index + consumed + j];
            SendCharacter(c);
            await DelayAsync(c, done + consumed + j, profile, mode, random, burst, token);
        }

        await EnsureActiveAsync(target, token);
        for (int j = 0; j < lag; j++)
        {
            Native.PressKey(Keys.Left);
            await Task.Delay(random.Next(35, 100), token);
        }

        switch (kind)
        {
            case 0: // replace bad character
                Native.PressKey(Keys.Back);
                SendCharacter(line[index]);
                break;
            case 1: // insert previously omitted character
                SendCharacter(line[index]);
                break;
            case 2: // remove duplicate
                Native.PressKey(Keys.Back);
                break;
            default: // remove both swapped characters; retype correctly
                Native.PressKey(Keys.Back);
                Native.PressKey(Keys.Back);
                SendCharacter(line[index]);
                SendCharacter(line[index + 1]);
                break;
        }

        await Task.Delay(random.Next(60, 160), token);
        for (int j = 0; j < lag; j++)
        {
            await EnsureActiveAsync(target, token);
            Native.PressKey(Keys.Right);
            await Task.Delay(random.Next(35, 100), token);
        }

        return consumed + lag;
    }

    private static bool CanMistype(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= 'а' and <= 'я' or >= 'А' and <= 'Я';

    private static char WrongLetter(char source, Random random)
    {
        var rows = new[] { "qwertyuiop", "asdfghjkl", "zxcvbnm", "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю" };
        var lower = char.ToLowerInvariant(source);
        foreach (var row in rows)
        {
            var idx = row.IndexOf(lower);
            if (idx < 0) continue;
            var next = idx == 0 ? 1 : idx == row.Length - 1 ? idx - 1 :
                (random.Next(2) == 0 ? idx - 1 : idx + 1);
            var replacement = row[next];
            return char.IsUpper(source) ? char.ToUpperInvariant(replacement) : replacement;
        }
        return char.IsUpper(source) ? 'X' : 'x';
    }

    private static void SendCharacter(char c)
    {
        if (c == '\t') Native.PressKey(Keys.Tab);
        else if (c != '\0') Native.TypeUnicode(c);
    }

    private async Task EnsureActiveAsync(IntPtr target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        while (true)
        {
            bool paused;
            lock (_gate) paused = _paused;
            if (!paused) break;
            await Task.Delay(65, token);
        }

        token.ThrowIfCancellationRequested();
        if (!Native.IsWindow(target) || Native.GetForegroundWindow() != target)
            throw new TargetWindowChangedException();
    }

    private static async Task DelayAsync(char c, int index, TypingProfile profile,
        TypingMode mode, Random random, bool burst, CancellationToken token)
    {
        var baseMs = 60000.0 / profile.CharactersPerMinute;
        var jitter = 1.0 + (random.NextDouble() * 2 - 1) * profile.SpeedVariationPercent / 100.0;
        var fatigue = 1.0 + Math.Min(1.0, index / 3000.0) * profile.FatiguePercent / 100.0;
        var speed = burst ? 0.65 : 1.0;
        var syntaxPause = mode == TypingMode.Code && c is ';' or '{' or '}' or ':'
            ? 1.7 : 1.0;
        var delay = Math.Clamp((int)(baseMs * jitter * fatigue * speed * syntaxPause), 12, 3000);
        await Task.Delay(delay, token);
        if (random.NextDouble() * 100 < (double)profile.LongPausePercent)
        {
            var ms = random.Next(profile.PauseMinMilliseconds,
                profile.PauseMaxMilliseconds + 1);
            if (ms > 0) await Task.Delay(ms, token);
        }
    }
}
