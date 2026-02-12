using System.Collections.Concurrent;

namespace SystemHarness.Mcp;

/// <summary>
/// Thread-safe in-memory ring buffer of recent tool actions.
/// Provides action history for AI safety and debugging.
/// </summary>
public static class ActionLog
{
    private static readonly ConcurrentQueue<ActionRecord> Actions = new();
    private const int MaxActions = 200;

    public static void Record(string tool, string? parameters, long durationMs, bool success)
    {
        Actions.Enqueue(new ActionRecord(
            DateTime.UtcNow, tool, parameters, durationMs, success));

        while (Actions.Count > MaxActions && Actions.TryDequeue(out _)) { }
    }

    public static IReadOnlyList<ActionRecord> GetRecent(int count = 50)
    {
        return Actions.ToArray()
            .Reverse()
            .Take(count)
            .ToArray();
    }

    public static int Count => Actions.Count;

    public static void Clear()
    {
        while (Actions.TryDequeue(out _)) { }
    }
}

public sealed record ActionRecord(
    DateTime Timestamp,
    string Tool,
    string? Parameters,
    long DurationMs,
    bool Success);
