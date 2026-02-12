namespace SystemHarness.Mcp;

/// <summary>
/// Thread-safe action rate limiter.
/// When enabled, tracks action timestamps and can report whether the rate is exceeded.
/// Advisory: tools can query it but enforcement is at the caller's discretion.
/// </summary>
public static class RateLimiter
{
    private static readonly object Lock = new();
    private static int _maxPerSecond;
    private static readonly Queue<DateTime> _timestamps = new();

    /// <summary>
    /// Gets the current max actions per second limit, or 0 if disabled.
    /// </summary>
    public static int MaxPerSecond
    {
        get { lock (Lock) return _maxPerSecond; }
    }

    /// <summary>
    /// Sets the max actions per second. Pass 0 to disable.
    /// </summary>
    public static void SetLimit(int maxPerSecond)
    {
        lock (Lock)
        {
            _maxPerSecond = Math.Max(0, maxPerSecond);
            _timestamps.Clear();
        }
    }

    /// <summary>
    /// Records an action and returns whether the rate limit is exceeded.
    /// </summary>
    public static bool RecordAndCheck()
    {
        lock (Lock)
        {
            if (_maxPerSecond <= 0) return false;

            var now = DateTime.UtcNow;
            var cutoff = now.AddSeconds(-1);

            // Remove old timestamps
            while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                _timestamps.Dequeue();

            _timestamps.Enqueue(now);
            return _timestamps.Count > _maxPerSecond;
        }
    }

    /// <summary>
    /// Gets the current action count in the last second.
    /// </summary>
    public static int CurrentRate
    {
        get
        {
            lock (Lock)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-1);
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                    _timestamps.Dequeue();
                return _timestamps.Count;
            }
        }
    }
}
