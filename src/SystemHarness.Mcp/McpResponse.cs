using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemHarness.Mcp;

/// <summary>
/// Uniform JSON envelope for all MCP tool responses.
/// Every tool returns: { "ok": true/false, "data": {...}, "meta": {"ts","ms"} }
/// </summary>
public static class McpResponse
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    // ── Success Responses ──

    /// <summary>Return arbitrary structured data.</summary>
    public static string Ok(object data, long? ms = null)
    {
        return Serialize(new Envelope(true, data, null, Meta(ms)));
    }

    /// <summary>Return a list of items with count.</summary>
    public static string Items<T>(IReadOnlyList<T> items, long? ms = null)
    {
        return Ok(new { items, count = items.Count }, ms);
    }

    /// <summary>Return text/markdown content.</summary>
    public static string Content(string content, string format = "text", long? ms = null)
    {
        return Ok(new { content, format }, ms);
    }

    /// <summary>Return a simple confirmation message.</summary>
    public static string Confirm(string message, long? ms = null)
    {
        return Ok(new { message }, ms);
    }

    /// <summary>Return a boolean check result.</summary>
    public static string Check(bool result, string? detail = null, long? ms = null)
    {
        return Ok(new { result, detail }, ms);
    }

    // ── Error Responses ──

    /// <summary>Return a structured error.</summary>
    public static string Error(string code, string message, long? ms = null)
    {
        return Serialize(new Envelope(false, null, new ErrorInfo(code, message), Meta(ms)));
    }

    // ── Internals ──

    private static MetaInfo Meta(long? ms) => new(DateTime.UtcNow.ToString("O"), ms);

    private static string Serialize(Envelope envelope)
    {
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }

    private sealed record Envelope(
        bool Ok,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Data,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ErrorInfo? Error,
        MetaInfo Meta);

    private sealed record ErrorInfo(string Code, string Message);

    private sealed record MetaInfo(
        string Ts,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Ms);
}
