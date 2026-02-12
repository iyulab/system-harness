using System.Collections.Concurrent;

namespace SystemHarness;

/// <summary>
/// Thread-safe in-memory audit log with a configurable maximum entry count.
/// When the limit is reached, oldest entries are discarded.
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private readonly int _maxEntries;
    private int _count;

    /// <summary>
    /// Creates an in-memory audit log.
    /// </summary>
    /// <param name="maxEntries">Maximum entries to retain. Default: 10,000.</param>
    public InMemoryAuditLog(int maxEntries = 10_000)
    {
        _maxEntries = maxEntries > 0 ? maxEntries : throw new ArgumentOutOfRangeException(nameof(maxEntries));
    }

    public Task AppendAsync(AuditEntry entry, CancellationToken ct = default)
    {
        _entries.Enqueue(entry);
        var count = Interlocked.Increment(ref _count);

        // Evict oldest entries if over limit
        while (count > _maxEntries && _entries.TryDequeue(out _))
        {
            count = Interlocked.Decrement(ref _count);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> GetEntriesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AuditEntry>>([.. _entries]);
    }

    public Task<IReadOnlyList<AuditEntry>> GetEntriesAsync(string category, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AuditEntry>>(
            _entries.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList());
    }
}
