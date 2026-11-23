namespace KeyER;

internal interface IHotkeyRegistrationBackend
{
    bool Register(IntPtr window, int id, Keys keys);
    void Unregister(IntPtr window, int id);
}

internal sealed class Win32HotkeyRegistrationBackend : IHotkeyRegistrationBackend
{
    public bool Register(IntPtr window, int id, Keys keys) =>
        Native.TryRegisterHotkey(window, id, keys);

    public void Unregister(IntPtr window, int id) =>
        _ = Native.UnregisterHotKey(window, id);
}

/// <summary>
/// Applies start, pause and stop hotkeys as one logical transaction.
/// On a failed change, tries to restore the previously registered set.
/// A preference-only settings change does not touch the existing hotkeys.
/// </summary>
internal sealed class HotkeyRegistrationCoordinator : IDisposable
{
    private readonly IHotkeyRegistrationBackend _backend;
    private readonly IntPtr _window;
    private Keys[] _active = [];
    private bool _disposed;

    public bool HasActiveSet => _active.Length == 3;

    public HotkeyRegistrationCoordinator(IHotkeyRegistrationBackend backend, IntPtr window)
    {
        _backend = backend;
        _window = window;
    }

    public bool TryApply(Keys start, Keys pause, Keys stop)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var requested = new[] { start, pause, stop };
        if (requested[0] == requested[1] || requested[0] == requested[2] ||
            requested[1] == requested[2])
            return false;
        if (_active.SequenceEqual(requested))
            return true;

        var previous = _active.ToArray();
        Clear();

        if (RegisterAll(requested))
        {
            _active = requested;
            return true;
        }

        // Roll back if another application already owns the proposed hotkey.
        if (previous.Length == 3 && RegisterAll(previous))
            _active = previous;
        return false;
    }

    private bool RegisterAll(Keys[] keys)
    {
        var count = 0;
        foreach (var key in keys)
        {
            if (!_backend.Register(_window, count + 1, key))
            {
                for (var i = 1; i <= count; i++)
                    _backend.Unregister(_window, i);
                return false;
            }
            count++;
        }
        return true;
    }

    public void Clear()
    {
        if (_active.Length == 3)
            for (var i = 1; i <= 3; i++)
                _backend.Unregister(_window, i);
        _active = [];
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
    }
}
