using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace KeyER;

/// <summary>
/// Same-user Windows named pipe connecting the native keyboard sender to the
/// opt-in VS Code companion extension. Never binds a network socket.
/// </summary>
internal sealed class IdeBridgeServer : IDisposable
{
    internal const string PipeName = "KeyER.IdeBridge.v1";
    private const int MaxMessageChars = 2_000_000;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly object _gate = new();
    private readonly Dictionary<long, TaskCompletionSource<JsonElement>> _pending = [];
    private NamedPipeServerStream? _pipe;
    private StreamWriter? _writer;
    private long _nextId;
    private int _connected;
    private Task? _server;
    private bool _disposed;

    public bool IsConnected => Volatile.Read(ref _connected) != 0;
    public event Action<string>? SessionInvalidated;
    public event Action<bool>? ConnectionChanged;

    public void Start()
    {
        if (_server is not null) return;
        _server = Task.Run(() => ServeAsync(_shutdown.Token));
    }

    public async Task<JsonElement> RequestAsync(string type, object? payload,
        CancellationToken cancellationToken = default, int timeoutMilliseconds = 3500)
    {
        if (!IsConnected) throw new InvalidOperationException("Расширение VS Code не подключено.");

        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) _pending.Add(id, completion);

        try
        {
            var line = JsonSerializer.Serialize(new { id, type, payload });
            if (line.Length > MaxMessageChars)
                throw new InvalidOperationException("Объём передаваемого кода превышает лимит интеграции.");

            await _sendGate.WaitAsync(cancellationToken);
            try
            {
                StreamWriter? writer;
                lock (_gate) writer = _writer;
                if (writer is null) throw new IOException("Соединение с VS Code потеряно.");
                await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
            }
            finally { _sendGate.Release(); }

            return await completion.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMilliseconds),
                cancellationToken);
        }
        finally { lock (_gate) _pending.Remove(id); }
    }

    private async Task ServeAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 8192, 8192);
                lock (_gate) _pipe = pipe;
                await pipe.WaitForConnectionAsync(token);
                using var reader = new StreamReader(pipe, new UTF8Encoding(false),
                    detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 8192,
                    leaveOpen: true) { AutoFlush = true };
                lock (_gate) _writer = writer;
                Volatile.Write(ref _connected, 1);
                ConnectionChanged?.Invoke(true);

                while (pipe.IsConnected && !token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(token);
                    if (line is null || line.Length > MaxMessageChars) break;
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("replyTo", out var replyId) &&
                        replyId.TryGetInt64(out var id))
                    {
                        TaskCompletionSource<JsonElement>? waiter;
                        lock (_gate) _pending.TryGetValue(id, out waiter);
                        waiter?.TrySetResult(root.Clone());
                    }
                    else if (root.TryGetProperty("type", out var evt) &&
                             evt.GetString() == "invalidated" &&
                             root.TryGetProperty("sessionId", out var sid))
                    {
                        var sessionId = sid.GetString();
                        if (!string.IsNullOrEmpty(sessionId))
                            SessionInvalidated?.Invoke(sessionId);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                DiagnosticLog.Error("ide-pipe", ex);
            }
            finally
            {
                bool wasConnected = IsConnected;
                Volatile.Write(ref _connected, 0);
                lock (_gate)
                {
                    _writer = null;
                    _pipe = null;
                    foreach (var pending in _pending.Values)
                        pending.TrySetException(new IOException("Расширение VS Code отключилось."));
                }
                pipe?.Dispose();
                if (wasConnected) ConnectionChanged?.Invoke(false);
            }
            // Fast reconnect if the extension restarts.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdown.Cancel();
        lock (_gate)
        {
            _pipe?.Dispose();
            foreach (var value in _pending.Values)
                value.TrySetCanceled();
        }
        _shutdown.Dispose();
    }
}
