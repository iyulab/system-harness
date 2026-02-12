namespace SystemHarness;

/// <summary>
/// Global emergency stop mechanism.
/// Provides a <see cref="CancellationToken"/> that can be triggered to cancel all ongoing operations.
/// Thread-safe: can be triggered from any thread (including input hook callbacks).
/// </summary>
public sealed class EmergencyStop : IDisposable
{
    private CancellationTokenSource _cts = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Cancellation token to pass to all operations.
    /// Becomes cancelled when <see cref="Trigger"/> is called.
    /// </summary>
    public CancellationToken Token
    {
        get
        {
            lock (_lock)
            {
                return _cts.Token;
            }
        }
    }

    /// <summary>
    /// Whether the emergency stop has been triggered and not yet reset.
    /// </summary>
    public bool IsTriggered
    {
        get
        {
            lock (_lock)
            {
                return _cts.IsCancellationRequested;
            }
        }
    }

    /// <summary>
    /// Raised when emergency stop is triggered. Handlers run synchronously on the triggering thread.
    /// </summary>
    public event Action? Triggered;

    /// <summary>
    /// Cancels the token, signaling all operations to stop.
    /// </summary>
    public void Trigger()
    {
        lock (_lock)
        {
            if (_disposed) return;
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }

        Triggered?.Invoke();
    }

    /// <summary>
    /// Resets the emergency stop, creating a new token for subsequent operations.
    /// Operations that captured the previous token remain cancelled.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Dispose();
        }
    }
}
