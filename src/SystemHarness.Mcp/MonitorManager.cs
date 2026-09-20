using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemHarness.Mcp;

/// <summary>
/// Manages background monitor tasks that write events to JSONL files.
/// Each monitor has a unique ID, a cancellation token, and an output file.
/// </summary>
public sealed class MonitorManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly ConcurrentDictionary<string, MonitorEntry> _monitors = new();
    private int _nextId;

    /// <summary>How long <see cref="Stop"/> and <see cref="Dispose"/> wait for one monitor to end.</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    public string Start(string type, string outputPath, Func<string, CancellationToken, Task> monitorFunc)
    {
        var id = $"{type}-{Interlocked.Increment(ref _nextId)}";
        var cts = new CancellationTokenSource();

        // Ensure output directory exists
        var dir = Path.GetDirectoryName(outputPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var task = Task.Run(async () =>
        {
            try
            {
                await monitorFunc(outputPath, cts.Token);
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex)
            {
                // Write error event to the output file
                await WriteEventAsync(outputPath, new
                {
                    type = "error",
                    message = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("O"),
                });
            }
        }, cts.Token);

        var entry = new MonitorEntry(id, type, outputPath, cts, task, DateTime.UtcNow);
        _monitors[id] = entry;
        return id;
    }

    public bool Stop(string monitorId)
    {
        if (!_monitors.TryRemove(monitorId, out var entry))
            return false;

        Quiesce(entry);
        return true;
    }

    /// <summary>
    /// Cancels a monitor and waits for it to finish before releasing its token source. Returning earlier would let
    /// the monitor write to its output file after the caller was told it had stopped - a caller that then deletes or
    /// moves the file races that write. The wait is bounded: a monitor function that ignores its token is abandoned
    /// after <see cref="StopTimeout"/>.
    /// </summary>
    private static void Quiesce(MonitorEntry entry)
    {
        entry.Cts.Cancel();
        try
        {
            entry.Task.Wait(StopTimeout);
        }
        catch (AggregateException)
        {
            // The monitor ended with an error or as cancelled; either way it has ended.
        }

        entry.Cts.Dispose();
    }

    public IReadOnlyList<MonitorInfo> ListActive()
    {
        return _monitors.Values
            .Select(e => new MonitorInfo(e.Id, e.Type, e.OutputPath, e.StartedAt, !e.Task.IsCompleted))
            .ToArray();
    }

    public static async Task WriteEventAsync(string outputPath, object eventData, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(eventData, JsonOpts);
        await File.AppendAllTextAsync(outputPath, json + "\n", ct);
    }

    public static async Task<IReadOnlyList<JsonElement>> ReadEventsAsync(
        string outputPath, DateTime? since = null, CancellationToken ct = default)
    {
        if (!File.Exists(outputPath))
            return [];

        var lines = await File.ReadAllLinesAsync(outputPath, ct);
        var events = new List<JsonElement>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var doc = JsonDocument.Parse(line);
                if (since.HasValue)
                {
                    if (doc.RootElement.TryGetProperty("timestamp", out var ts) &&
                        DateTime.TryParse(ts.GetString(), out var tsVal) &&
                        tsVal < since.Value)
                    {
                        doc.Dispose();
                        continue;
                    }
                }
                events.Add(doc.RootElement.Clone());
                doc.Dispose();
            }
            catch { /* skip malformed lines */ }
        }

        return events;
    }

    public void Dispose()
    {
        foreach (var entry in _monitors.Values)
        {
            Quiesce(entry);
        }
        _monitors.Clear();
    }

    private sealed record MonitorEntry(
        string Id, string Type, string OutputPath,
        CancellationTokenSource Cts, Task Task, DateTime StartedAt);
}

public sealed record MonitorInfo(
    string Id, string Type, string OutputPath, DateTime StartedAt, bool IsRunning);
