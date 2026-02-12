namespace SystemHarness;

/// <summary>
/// Append-only sink for audit log entries.
/// Implement this interface for custom persistence (file, database, remote).
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Appends an entry to the log.
    /// </summary>
    Task AppendAsync(AuditEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Returns all entries in the log (oldest first).
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> GetEntriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns entries matching the given category filter.
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> GetEntriesAsync(string category, CancellationToken ct = default);
}
