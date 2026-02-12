using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemHarness.Mcp;

/// <summary>
/// File-based confirmation system for dangerous actions.
/// Creates a JSON confirmation request file that can be approved/denied externally.
/// Since MCP has no push mechanism, this uses file-based polling.
/// </summary>
public static class ConfirmationManager
{
    private static readonly ConcurrentDictionary<string, ConfirmationRequest> Pending = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Create a new confirmation request. Writes a JSON file that can be approved by editing.
    /// </summary>
    public static ConfirmationRequest Create(string action, string reason)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var path = Path.Combine(Path.GetTempPath(), $"harness-confirm-{id}.json");

        var request = new ConfirmationRequest
        {
            Id = id,
            Action = action,
            Reason = reason,
            Status = ConfirmationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            FilePath = path,
        };

        File.WriteAllText(path, JsonSerializer.Serialize(request, JsonOpts));
        Pending[id] = request;
        return request;
    }

    /// <summary>
    /// Check the status of a confirmation request by reading the file.
    /// External process can approve by changing "status" to "approved" or "denied".
    /// </summary>
    public static ConfirmationRequest Check(string id)
    {
        if (!Pending.TryGetValue(id, out var original))
            throw new HarnessException($"Confirmation request '{id}' not found.");

        if (!File.Exists(original.FilePath))
            throw new HarnessException($"Confirmation file not found: '{original.FilePath}'");

        var json = File.ReadAllText(original.FilePath);
        var updated = JsonSerializer.Deserialize<ConfirmationRequest>(json, JsonOpts)
            ?? throw new HarnessException("Failed to parse confirmation file.");

        // Update in-memory state if status changed
        if (updated.Status != ConfirmationStatus.Pending)
        {
            updated = updated with { ResolvedAt = updated.ResolvedAt ?? DateTime.UtcNow };
            Pending[id] = updated;
        }

        return updated;
    }

    /// <summary>
    /// Approve a confirmation request programmatically.
    /// </summary>
    public static ConfirmationRequest Approve(string id)
    {
        return Resolve(id, ConfirmationStatus.Approved);
    }

    /// <summary>
    /// Deny a confirmation request programmatically.
    /// </summary>
    public static ConfirmationRequest Deny(string id)
    {
        return Resolve(id, ConfirmationStatus.Denied);
    }

    /// <summary>
    /// List all pending confirmation requests.
    /// </summary>
    public static IReadOnlyList<ConfirmationRequest> ListPending()
    {
        return Pending.Values.Where(r => r.Status == ConfirmationStatus.Pending).ToArray();
    }

    private static ConfirmationRequest Resolve(string id, ConfirmationStatus status)
    {
        if (!Pending.TryGetValue(id, out var original))
            throw new HarnessException($"Confirmation request '{id}' not found.");

        var resolved = original with
        {
            Status = status,
            ResolvedAt = DateTime.UtcNow,
        };

        File.WriteAllText(resolved.FilePath, JsonSerializer.Serialize(resolved, JsonOpts));
        Pending[id] = resolved;
        return resolved;
    }
}

public sealed record ConfirmationRequest
{
    public required string Id { get; init; }
    public required string Action { get; init; }
    public required string Reason { get; init; }
    public required ConfirmationStatus Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public required string FilePath { get; init; }
}

public enum ConfirmationStatus
{
    Pending,
    Approved,
    Denied,
}
