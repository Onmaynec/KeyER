namespace KeyER;

/// <summary>
/// Prevents accidental duplicate UI processes from competing for global hotkeys
/// and for the same user settings file. Never holds a system-wide/global lock.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private SingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
        _ownsMutex = true;
    }

    internal static SingleInstanceGuard? TryAcquire(
        string name = @"Local\KeyER.Studio.SingleInstance")
    {
        var mutex = new Mutex(true, name, out var createdNew);
        if (createdNew) return new SingleInstanceGuard(mutex);

        // A newly opened handle without initial ownership must still be closed.
        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (!_ownsMutex) return;
        _ownsMutex = false;
        try { _mutex.ReleaseMutex(); }
        finally { _mutex.Dispose(); }
    }
}
