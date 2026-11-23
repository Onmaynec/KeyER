namespace KeyER;

public enum TypingOutcome { Idle, Running, Completed, Cancelled, FocusLost, Failed, Interrupted }

public sealed class TargetWindowChangedException : Exception
{
    public TargetWindowChangedException()
        : base("Активное окно изменилось. Набор остановлен, чтобы не вводить текст не туда.") { }
}

public sealed class UnexpectedModifierException : Exception
{
    public UnexpectedModifierException()
        : base("Во время набора зажаты Ctrl, Alt, Shift или Win. Ввод остановлен, чтобы избежать случайных команд.") { }
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
    private int _boundProcessId;

    internal bool BindTarget(IntPtr target)
    {
        var id = Native.GetWindowProcessId(target);
        if (id == 0) return false;
        Volatile.Write(ref _boundProcessId, checked((int)id));
        return true;
    }

    internal void UnbindTarget() => Volatile.Write(ref _boundProcessId, 0);

    public bool IsTargetActive(IntPtr target)
    {
        if (target == IntPtr.Zero || !Native.IsWindow(target) ||
            Native.IsOwnWindow(target) || Native.GetForegroundWindow() != target)
            return false;
        var bound = Volatile.Read(ref _boundProcessId);
        return bound == 0 || Native.GetWindowProcessId(target) == (uint)bound;
    }

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
    private bool _modifiersReleased;
    private volatile TypingOutcome _outcome = TypingOutcome.Idle;
    private int _processedCharacters;
    private int _totalCharacters;

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
    public TypingOutcome LastOutcome => _outcome;
    public int LastProcessedCharacters => Volatile.Read(ref _processedCharacters);
    public int LastTotalCharacters => Volatile.Read(ref _totalCharacters);

    private void ReportProgress(int completed, int total)
    {
        Volatile.Write(ref _processedCharacters, Math.Clamp(completed, 0, Math.Max(0, total)));
        Volatile.Write(ref _totalCharacters, Math.Max(0, total));
        ProgressChanged?.Invoke(completed, total);
    }

    public async Task StartAsync(string content, TypingMode mode, TypingProfile sourceProfile,
        IntPtr target, string? sourceFileName = null, string? selectedLanguage = null)
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
            _modifiersReleased = false;
            _outcome = TypingOutcome.Running;
            Volatile.Write(ref _processedCharacters, 0);
            Volatile.Write(ref _totalCharacters, content.Replace("\r\n", "\n").Replace('\r', '\n').Length);
        }

        try
        {
            if (_device is WindowsTypingDevice windowsDevice &&
                !windowsDevice.BindTarget(target))
                throw new TargetWindowChangedException();
            StatusChanged?.Invoke("Набор текста...");
            await Task.Run(() => RunAsync(content, mode, profile, target, sourceFileName,
                selectedLanguage, local.Token));
            _outcome = TypingOutcome.Completed;
            StatusChanged?.Invoke("Готово. Ввод завершён.");
        }
        catch (OperationCanceledException)
        {
            _outcome = TypingOutcome.Cancelled;
            StatusChanged?.Invoke("Остановлено пользователем.");
        }
        catch (TargetWindowChangedException ex)
        {
            _outcome = TypingOutcome.FocusLost;
            StatusChanged?.Invoke(ex.Message);
        }
        catch (UnexpectedModifierException ex)
        {
            _outcome = TypingOutcome.Interrupted;
            StatusChanged?.Invoke(ex.Message);
        }
        catch (Exception ex)
        {
            _outcome = TypingOutcome.Failed;
            DiagnosticLog.Error("typing", ex);
            StatusChanged?.Invoke("Ошибка ввода: " + ex.Message);
        }
        finally
        {
            if (_device is WindowsTypingDevice windowsDevice) windowsDevice.UnbindTarget();
            lock (_gate)
            {
                if (ReferenceEquals(_cts, local))
                {
                    _cts = null;
                    _paused = false;
                    _modifiersReleased = false;
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
        IntPtr target, string? fileName, string? selectedLanguage, CancellationToken token)
    {
        source = source.Replace("\r\n", "\n").Replace('\r', '\n');
        if (mode == TypingMode.Text)
        {
            await RunHumanTextAsync(source, profile, target, token);
            return;
        }

        var language = CodeAnalyzer.Language.Auto;
        if (!string.IsNullOrWhiteSpace(selectedLanguage))
            Enum.TryParse(selectedLanguage, ignoreCase: true, out language);
        await RunCodeAsync(source, profile, target, fileName, language, token);
    }

    private async Task<int> MistypeAndCorrectAsync(string line, int index, int done,
        TypingProfile profile, TypingMode mode, IntPtr target, Random random, bool burst,
        CancellationToken token, HumanMotorModel motor)
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
                await DelayAsync(line[index], done, profile, mode, random, burst, token, motor);
                await CharacterAsync(line[index], target, token);
                break;
            case 3:
                await CharacterAsync(line[index + 1], target, token);
                await DelayAsync(line[index + 1], done, profile, mode, random, burst, token, motor);
                await CharacterAsync(line[index], target, token);
                break;
        }

        await DelayAsync(line[index], done, profile, mode, random, burst, token, motor);

        for (var j = 0; j < lag; j++)
        {
            var c = line[index + consumed + j];
            await CharacterAsync(c, target, token);
            await DelayAsync(c, done + consumed + j, profile, mode, random, burst, token, motor);
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

    private static char WrongLetter(char source, Random random) =>
        HumanMotorModel.NearbyLetter(source, random);

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
        if (_modifiersReleased && _device.AreModifiersHeld)
            throw new UnexpectedModifierException();
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
        // The initial start hotkey has now been released. Later physical
        // modifiers could transform navigation/repair into unintended shortcuts.
        _modifiersReleased = true;
    }

    private Task PauseAsync(int milliseconds, CancellationToken token) =>
        _wait(milliseconds, token);

    private async Task DelayAsync(char c, int index, TypingProfile profile,
        TypingMode mode, Random random, bool burst, CancellationToken token,
        HumanMotorModel motor)
    {
        // Corrections are short, deliberate bursts with the same session
        // motor characteristics as ordinary source characters.
        var delay = motor.NextDelay(c, '\0', wordLength: 0,
            contextFactor: burst ? 0.82 : 1.12);
        await PauseAsync(delay, token);

        if (random.NextDouble() * 100 < (double)profile.LongPausePercent)
        {
            var ms = random.Next(profile.PauseMinMilliseconds,
                profile.PauseMaxMilliseconds + 1);
            if (ms > 0)
            {
                await PauseAsync(ms, token);
                motor.OnRest(ms);
            }
        }
    }

}
