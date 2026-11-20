namespace KeyER;

public sealed class TargetWindowChangedException : Exception
{
    public TargetWindowChangedException()
        : base("Активное окно изменилось. Набор остановлен, чтобы не вводить текст не туда.") { }
}

internal interface ITypingDevice
{
    bool IsTargetActive(IntPtr target);
    bool AreModifiersHeld { get; }
    void TypeUnicode(char character);
    void PressKey(Keys key);
    void PressChord(Keys modifier, Keys key);
}

internal sealed class WindowsTypingDevice : ITypingDevice
{
    public bool IsTargetActive(IntPtr target) =>
        target != IntPtr.Zero && Native.IsWindow(target) &&
        !Native.IsOwnWindow(target) && Native.GetForegroundWindow() == target;

    public bool AreModifiersHeld =>
        Native.IsPressed(Keys.ControlKey) || Native.IsPressed(Keys.Menu) ||
        Native.IsPressed(Keys.ShiftKey) || Native.IsPressed(Keys.LWin) ||
        Native.IsPressed(Keys.RWin);

    public void TypeUnicode(char character) => Native.TypeUnicode(character);
    public void PressKey(Keys key) => Native.PressKey(key);
    public void PressChord(Keys modifier, Keys key) => Native.PressChord(modifier, key);
}

/// <summary>
/// Keyboard input simulation. The target is revalidated before every key event.
/// An injectable device enables deterministic regression tests without emitting real input.
/// </summary>
public sealed partial class TypingEngine
{
    private readonly object _gate = new();
    private readonly ITypingDevice _device;
    private readonly Func<int, CancellationToken, Task> _wait;
    private readonly Func<Random> _randomFactory;
    private CancellationTokenSource? _cts;
    private bool _paused;

    public event Action<int, int>? ProgressChanged;
    public event Action<string>? StatusChanged;

    public TypingEngine()
        : this(new WindowsTypingDevice(), Task.Delay, () => new Random()) { }

    internal TypingEngine(ITypingDevice device, Func<int, CancellationToken, Task> wait,
        Func<Random> randomFactory)
    {
        _device = device;
        _wait = wait;
        _randomFactory = randomFactory;
    }

    public bool IsRunning { get { lock (_gate) return _cts is not null; } }
    public bool IsPaused { get { lock (_gate) return _paused; } }

    public async Task StartAsync(string content, TypingMode mode, TypingProfile sourceProfile, IntPtr target)
    {
        if (string.IsNullOrEmpty(content)) throw new ArgumentException("Нет текста для ввода.");
        if (!_device.IsTargetActive(target))
            throw new InvalidOperationException("Выберите активное окно другого приложения и установите курсор.");

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
            DiagnosticLog.Error("typing", ex);
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
        string? status = null;
        lock (_gate)
        {
            if (_cts is null || _cts.IsCancellationRequested) return;
            _paused = !_paused;
            status = _paused ? "Пауза. Для продолжения вернитесь в целевое окно." : "Продолжение...";
        }
        StatusChanged?.Invoke(status);
    }

    public void Stop()
    {
        lock (_gate) _cts?.Cancel();
    }

    private async Task RunAsync(string source, TypingMode mode, TypingProfile profile,
        IntPtr target, CancellationToken token)
    {
        source = source.Replace("\r\n", "\n").Replace('\r', '\n');
        if (mode == TypingMode.Text)
        {
            await RunHumanTextAsync(source, profile, target, token);
            return;
        }
        var lines = source.Split('\n');
        var total = source.Length;
        var done = 0;
        var random = _randomFactory();
        var burstRemaining = 0;
        var inBurst = false;

        await WaitForHotkeyReleaseAsync(target, token);

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            await EnsureActiveAsync(target, token);

            if (lineIndex > 0)
            {
                await KeyAsync(Keys.Enter, target, token);
                done++;
                ProgressChanged?.Invoke(done, total);
                await DelayAsync('\n', done, profile, mode, random, false, token);

                if (mode == TypingMode.Code && profile.ClearAutoIndent && lines[lineIndex].Length > 0)
                {
                    // Experimental and opt-in; editor-specific behavior is not guaranteed.
                    await KeyAsync(Keys.Home, target, token);
                    await KeyAsync(Keys.Home, target, token);
                    await ChordAsync(Keys.ShiftKey, Keys.End, target, token);
                }
            }

            var line = lines[lineIndex];
            for (var i = 0; i < line.Length;)
            {
                await EnsureActiveAsync(target, token);
                var c = line[i];
                if (--burstRemaining <= 0)
                {
                    inBurst = random.Next(100) < profile.BurstPercent;
                    burstRemaining = random.Next(6, 22);
                }

                if (CanMistype(c) && random.NextDouble() * 100 < (double)profile.TypoPercent)
                {
                    var consumed = await MistypeAndCorrectAsync(line, i, done, profile,
                        mode, target, random, inBurst, token);
                    i += consumed;
                    done += consumed;
                }
                else
                {
                    await CharacterAsync(c, target, token);
                    await DelayAsync(c, done, profile, mode, random, inBurst, token);
                    i++;
                    done++;
                }

                if (done % 4 == 0 || i >= line.Length)
                    ProgressChanged?.Invoke(done, total);
            }

            // Use a reversible one-step caret review, never Home/End.
            // Home/End is editor-dependent and may leave the caret at the wrong location.
            if (mode == TypingMode.Text && line.Length > 0 &&
                random.Next(100) < profile.CursorReviewPercent)
            {
                await KeyAsync(Keys.Left, target, token);
                await PauseAsync(random.Next(80, 220), token);
                await KeyAsync(Keys.Right, target, token);
            }
        }

        ProgressChanged?.Invoke(total, total);
    }

    private async Task<int> MistypeAndCorrectAsync(string line, int index, int done,
        TypingProfile profile, TypingMode mode, IntPtr target, Random random, bool burst,
        CancellationToken token)
    {
        // All error types stay inside one uninterrupted word.
        var kind = random.Next(4); // 0 replace, 1 omit, 2 double, 3 transpose
        if (kind == 3 && (index + 1 >= line.Length || !CanMistype(line[index + 1])))
            kind = 0;

        var consumed = kind == 3 ? 2 : 1;
        var maxLag = Math.Min(profile.CorrectionLagCharacters, line.Length - index - consumed);
        var available = 0;
        while (available < maxLag && CanMistype(line[index + consumed + available]))
            available++;
        var lag = available > 0 && random.Next(100) < profile.LateCorrectionPercent
            ? random.Next(1, available + 1) : 0;

        switch (kind)
        {
            case 0:
                await CharacterAsync(WrongLetter(line[index], random), target, token);
                break;
            case 1:
                break; // omitted original character
            case 2:
                await CharacterAsync(line[index], target, token);
                await DelayAsync(line[index], done, profile, mode, random, burst, token);
                await CharacterAsync(line[index], target, token);
                break;
            case 3:
                await CharacterAsync(line[index + 1], target, token);
                await DelayAsync(line[index + 1], done, profile, mode, random, burst, token);
                await CharacterAsync(line[index], target, token);
                break;
        }

        await DelayAsync(line[index], done, profile, mode, random, burst, token);

        for (var j = 0; j < lag; j++)
        {
            var c = line[index + consumed + j];
            await CharacterAsync(c, target, token);
            await DelayAsync(c, done + consumed + j, profile, mode, random, burst, token);
        }

        // Move from end of typed lag to the damaged segment, repair, and return.
        for (var j = 0; j < lag; j++)
        {
            await KeyAsync(Keys.Left, target, token);
            await PauseAsync(random.Next(35, 100), token);
        }

        switch (kind)
        {
            case 0:
                await KeyAsync(Keys.Back, target, token);
                await CharacterAsync(line[index], target, token);
                break;
            case 1:
                await CharacterAsync(line[index], target, token);
                break;
            case 2:
                await KeyAsync(Keys.Back, target, token);
                break;
            case 3:
                await KeyAsync(Keys.Back, target, token);
                await KeyAsync(Keys.Back, target, token);
                await CharacterAsync(line[index], target, token);
                await CharacterAsync(line[index + 1], target, token);
                break;
        }

        await PauseAsync(random.Next(60, 160), token);
        for (var j = 0; j < lag; j++)
        {
            await KeyAsync(Keys.Right, target, token);
            await PauseAsync(random.Next(35, 100), token);
        }

        return consumed + lag;
    }

    private static bool CanMistype(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= 'а' and <= 'я' or >= 'А' and <= 'Я';

    private static char WrongLetter(char source, Random random)
    {
        string[] rows = ["qwertyuiop", "asdfghjkl", "zxcvbnm", "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю"];
        var lower = char.ToLowerInvariant(source);
        if (lower == 'ё') return char.IsUpper(source) ? 'Й' : 'й';
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

    private async Task CharacterAsync(char character, IntPtr target, CancellationToken token)
    {
        await EnsureActiveAsync(target, token);
        if (character == '\t') _device.PressKey(Keys.Tab);
        else _device.TypeUnicode(character);
    }

    private async Task KeyAsync(Keys key, IntPtr target, CancellationToken token)
    {
        await EnsureActiveAsync(target, token);
        _device.PressKey(key);
    }

    private async Task ChordAsync(Keys modifier, Keys key, IntPtr target, CancellationToken token)
    {
        await EnsureActiveAsync(target, token);
        _device.PressChord(modifier, key);
    }

    private async Task EnsureActiveAsync(IntPtr target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        while (true)
        {
            bool paused;
            lock (_gate) paused = _paused;
            if (!paused) break;
            await Task.Delay(50, token);
        }
        token.ThrowIfCancellationRequested();
        if (!_device.IsTargetActive(target)) throw new TargetWindowChangedException();
    }

    private async Task WaitForHotkeyReleaseAsync(IntPtr target, CancellationToken token)
    {
        var start = Environment.TickCount64;
        while (_device.AreModifiersHeld)
        {
            await EnsureActiveAsync(target, token);
            if (Environment.TickCount64 - start > 4000)
                throw new InvalidOperationException(
                    "Отпустите Ctrl, Alt, Shift и Win перед началом ввода.");
            await Task.Delay(20, token);
        }
    }

    private Task PauseAsync(int milliseconds, CancellationToken token) =>
        _wait(milliseconds, token);

    private async Task DelayAsync(char c, int index, TypingProfile profile,
        TypingMode mode, Random random, bool burst, CancellationToken token)
    {
        var baseMs = 60000.0 / profile.CharactersPerMinute;
        var jitter = 1.0 + (random.NextDouble() * 2 - 1) * profile.SpeedVariationPercent / 100.0;
        var fatigue = 1.0 + Math.Min(1.0, index / 3000.0) * profile.FatiguePercent / 100.0;
        var speed = burst ? 0.65 : 1.0;
        var syntaxPause = mode == TypingMode.Code && c is ';' or '{' or '}' or ':'
            ? 1.7 : 1.0;
        var delay = Math.Clamp((int)(baseMs * jitter * fatigue * speed * syntaxPause), 12, 3000);
        await PauseAsync(delay, token);

        if (random.NextDouble() * 100 < (double)profile.LongPausePercent)
        {
            var ms = random.Next(profile.PauseMinMilliseconds, profile.PauseMaxMilliseconds + 1);
            if (ms > 0) await PauseAsync(ms, token);
        }
    }
}
