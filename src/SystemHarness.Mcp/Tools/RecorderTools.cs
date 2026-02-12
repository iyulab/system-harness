using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace SystemHarness.Mcp.Tools;

public sealed class RecorderTools(IActionRecorder recorder)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [McpServerTool(Name = "record_start"), Description(
        "Start recording mouse and keyboard actions using global hooks. " +
        "Call record_stop to finish and record_get_actions to retrieve the recorded sequence.")]
    public async Task<string> StartRecordingAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await recorder.StartRecordingAsync(ct);
        ActionLog.Record("record_start", "started recording", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm("Recording started. Use record_stop to finish.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "record_stop"), Description(
        "Stop recording mouse and keyboard actions. " +
        "Use record_get_actions to retrieve the recorded action sequence.")]
    public async Task<string> StopRecordingAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await recorder.StopRecordingAsync(ct);
        ActionLog.Record("record_stop", "stopped recording", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm("Recording stopped. Use record_get_actions to retrieve actions.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "record_get_actions"), Description(
        "Get the list of recorded actions since the last record_start. " +
        "Returns mouse and keyboard events with timing information.")]
    public async Task<string> GetRecordedActionsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var actions = await recorder.GetRecordedActionsAsync(ct);
        return McpResponse.Items(actions.Select(a => new
        {
            type = a.Type.ToString(),
            timestamp = a.Timestamp.ToString("O"),
            x = a.X,
            y = a.Y,
            button = a.Button?.ToString(),
            key = a.Key?.ToString(),
            scrollDelta = a.ScrollDelta,
            delayBeforeMs = (long)a.DelayBefore.TotalMilliseconds,
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "record_replay"), Description(
        "Replay a sequence of previously recorded actions. " +
        "actionsJson is a JSON array of action objects from record_get_actions. " +
        "Respects original timing between actions, adjustable via speedMultiplier.")]
    public async Task<string> ReplayAsync(
        [Description("JSON array of recorded actions to replay.")] string actionsJson,
        [Description("Replay speed multiplier (1.0 = original, 2.0 = double speed).")] double speedMultiplier = 1.0,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(actionsJson))
            return McpResponse.Error("invalid_parameter", "actionsJson cannot be empty.", sw.ElapsedMilliseconds);
        if (speedMultiplier <= 0)
            return McpResponse.Error("invalid_parameter",
                $"speedMultiplier must be positive (got {speedMultiplier}).", sw.ElapsedMilliseconds);

        List<RecordedActionInput>? inputs;
        try
        {
            inputs = JsonSerializer.Deserialize<List<RecordedActionInput>>(actionsJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return McpResponse.Error("invalid_parameter", $"Invalid actionsJson: {ex.Message}", sw.ElapsedMilliseconds);
        }

        if (inputs is null || inputs.Count == 0)
            return McpResponse.Error("invalid_parameter", "actionsJson must contain at least one action.", sw.ElapsedMilliseconds);

        List<RecordedAction> actions;
        try
        {
            actions = inputs.Select(i => new RecordedAction
            {
                Type = Enum.Parse<RecordedActionType>(i.Type, ignoreCase: true),
                Timestamp = i.Timestamp ?? DateTimeOffset.UtcNow,
                X = i.X,
                Y = i.Y,
                Button = i.Button is not null ? Enum.Parse<MouseButton>(i.Button, ignoreCase: true) : null,
                Key = i.Key is not null ? Enum.Parse<Key>(i.Key, ignoreCase: true) : null,
                ScrollDelta = i.ScrollDelta,
                DelayBefore = TimeSpan.FromMilliseconds(i.DelayBeforeMs ?? 0),
            }).ToList();
        }
        catch (ArgumentException ex)
        {
            return McpResponse.Error("invalid_parameter", $"Invalid enum value in actionsJson: {ex.Message}", sw.ElapsedMilliseconds);
        }

        await recorder.ReplayAsync(actions, speedMultiplier, ct);
        ActionLog.Record("record_replay", $"actions={actions.Count}, speed={speedMultiplier}x", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Replayed {actions.Count} actions at {speedMultiplier}x speed.", sw.ElapsedMilliseconds);
    }

    private sealed class RecordedActionInput
    {
        public string Type { get; set; } = "";
        public DateTimeOffset? Timestamp { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public string? Button { get; set; }
        public string? Key { get; set; }
        public int? ScrollDelta { get; set; }
        public long? DelayBeforeMs { get; set; }
    }
}
