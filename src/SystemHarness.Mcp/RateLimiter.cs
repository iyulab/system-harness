namespace SystemHarness.Mcp;

/// <summary>
/// Thread-safe action rate limiter, enforced by <see cref="SafetyGate"/> on every mutation command.
/// A limit set by the operator at startup is a ceiling: the agent can tighten it but not loosen or disable it.
/// </summary>
public sealed class RateLimiter
{
    private readonly object _lock = new();
    private readonly Queue<DateTime> _timestamps = new();
    private int _maxPerSecond;
    private int _operatorMax;

    /// <summary>
    /// Gets the current max actions per second limit, or 0 if disabled.
    /// </summary>
    public int MaxPerSecond
    {
        get { lock (_lock) return _maxPerSecond; }
    }

    /// <summary>
    /// The operator's ceiling, or 0 when the operator set none.
    /// </summary>
    public int OperatorMaxPerSecond
    {
        get { lock (_lock) return _operatorMax; }
    }

    /// <summary>
    /// Sets the operator's ceiling and the current limit to it. Pass 0 for no ceiling.
    /// </summary>
    public void SetOperatorLimit(int maxPerSecond)
    {
        lock (_lock)
        {
            _operatorMax = Math.Max(0, maxPerSecond);
            _maxPerSecond = _operatorMax;
            _timestamps.Clear();
        }
    }

    /// <summary>
    /// Sets the max actions per second. Pass 0 to disable.
    /// </summary>
    /// <exception cref="HarnessException">The value would loosen or disable the operator's ceiling.</exception>
    public void SetLimit(int maxPerSecond)
    {
        maxPerSecond = Math.Max(0, maxPerSecond);
        lock (_lock)
        {
            if (_operatorMax > 0 && (maxPerSecond == 0 || maxPerSecond > _operatorMax))
                throw new HarnessException(
                    $"The operator limited actions to {_operatorMax} per second; a limit can only be lowered.");

            _maxPerSecond = maxPerSecond;
            _timestamps.Clear();
        }
    }

    /// <summary>
    /// Records an action and returns whether the rate limit is exceeded.
    /// </summary>
    public bool RecordAndCheck()
    {
        lock (_lock)
        {
            if (_maxPerSecond <= 0) return false;

            var now = DateTime.UtcNow;
            Trim(now);

            if (_timestamps.Count >= _maxPerSecond)
                return true;

            _timestamps.Enqueue(now);
            return false;
        }
    }

    /// <summary>
    /// Gets the current action count in the last second.
    /// </summary>
    public int CurrentRate
    {
        get
        {
            lock (_lock)
            {
                Trim(DateTime.UtcNow);
                return _timestamps.Count;
            }
        }
    }

    private void Trim(DateTime now)
    {
        var cutoff = now.AddSeconds(-1);
        while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
            _timestamps.Dequeue();
    }
}
