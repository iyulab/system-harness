using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class SafetyTools(EmergencyStop emergencyStop, MonitorManager monitors, SafeZone safeZone, RateLimiter rateLimiter)
{
    [McpServerTool(Name = "safety_action_history"), Description(
        "Get the history of recent tool actions. " +
        "Returns timestamps, tool names, parameters, duration, and success status. " +
        "Useful for reviewing what actions have been performed and debugging automation issues.")]
    public static Task<string> ActionHistoryAsync(
        [Description("Number of recent actions to return (1-200).")] int count = 50, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var actions = ActionLog.GetRecent(Math.Clamp(count, 1, 200));
        return Task.FromResult(McpResponse.Items(actions.Select(a => new
        {
            timestamp = a.Timestamp.ToString("O"),
            tool = a.Tool,
            parameters = a.Parameters,
            durationMs = a.DurationMs,
            success = a.Success,
        }).ToArray(), sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_clear_history"), Description(
        "Clear the action history log.")]
    public static Task<string> ClearHistoryAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var previous = ActionLog.Count;
        ActionLog.Clear();
        ActionLog.Record("safety_clear_history", $"cleared={previous}", sw.ElapsedMilliseconds, true);
        return Task.FromResult(McpResponse.Confirm(
            $"Cleared {previous} action history entries.", sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_emergency_stop"), Description(
        "Trigger an emergency stop — cancels running commands, stops all running monitors, " +
        "and refuses every further action until safety_resume. " +
        "The operator can also stop the session with Ctrl+Shift+Escape; that stop holds until the server restarts.")]
    public Task<string> EmergencyStopAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Monitors are stopped by the server's EmergencyStop.Triggered handler
        var active = monitors.ListActive();
        emergencyStop.Trigger(EmergencyStopSource.Agent);

        ActionLog.Record("safety_emergency_stop", $"stopped_monitors={active.Count}", sw.ElapsedMilliseconds, true);
        return Task.FromResult(McpResponse.Confirm(
            $"Emergency stop triggered. {active.Count} monitor(s) stopped. Use safety_resume to reset.", sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_resume"), Description(
        "Reset an emergency stop triggered with safety_emergency_stop and resume normal operations. " +
        "A stop the operator triggered with the hotkey cannot be reset here; it holds until the server restarts.")]
    public Task<string> ResumeAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (emergencyStop.TriggeredBy == EmergencyStopSource.Operator)
        {
            ActionLog.Record("safety_resume", "refused: operator stop", sw.ElapsedMilliseconds, false);
            return Task.FromResult(McpResponse.Error("operator_stop",
                "The operator stopped this session; restart the server to resume.", sw.ElapsedMilliseconds));
        }

        var wasTriggered = emergencyStop.IsTriggered;
        emergencyStop.Reset();

        ActionLog.Record("safety_resume", null, sw.ElapsedMilliseconds, true);
        return Task.FromResult(McpResponse.Ok(new
        {
            resumed = true,
            wasTriggered,
        }, sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_set_zone"), Description(
        "Restrict input actions (mouse, keyboard, UI automation, vision clicks, dialogs, window changes) " +
        "to one window, optionally a region inside it (regionX/regionY relative to the window). " +
        "Actions outside the zone are refused; keyboard actions need the zone window in the foreground. " +
        "Pass titleOrHandle as null/empty to clear the safe zone. A zone the operator set at startup cannot be changed.")]
    public Task<string> SetZoneAsync(
        [Description("Window to restrict actions to (title substring or handle). Pass null/empty to clear.")] string? titleOrHandle = null,
        [Description("Optional region X within the window.")] int? regionX = null,
        [Description("Optional region Y within the window.")] int? regionY = null,
        [Description("Optional region width.")] int? regionW = null,
        [Description("Optional region height.")] int? regionH = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (safeZone.IsOperatorLocked)
            return Task.FromResult(McpResponse.Error("operator_locked",
                "The operator set the safe zone; it cannot be changed.", sw.ElapsedMilliseconds));

        if (string.IsNullOrWhiteSpace(titleOrHandle))
        {
            safeZone.Clear();
            ActionLog.Record("safety_set_zone", "cleared", sw.ElapsedMilliseconds, true);
            return Task.FromResult(McpResponse.Confirm("Safe zone cleared. Actions unrestricted.", sw.ElapsedMilliseconds));
        }

        var hasRegion = regionX.HasValue && regionY.HasValue && regionW.HasValue && regionH.HasValue;
        safeZone.Set(titleOrHandle, hasRegion
            ? new Rectangle(regionX!.Value, regionY!.Value, regionW!.Value, regionH!.Value)
            : null);

        ActionLog.Record("safety_set_zone", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
        return Task.FromResult(McpResponse.Ok(new
        {
            window = titleOrHandle,
            hasRegion,
            region = hasRegion ? new { x = regionX, y = regionY, w = regionW, h = regionH } : null,
        }, sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_get_zone"), Description(
        "Get the current safe zone restriction (if any).")]
    public Task<string> GetZoneAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var zone = safeZone.Current;
        return Task.FromResult(McpResponse.Ok(new
        {
            isSet = zone is not null,
            operatorLocked = safeZone.IsOperatorLocked,
            window = zone?.Window,
            region = zone?.Region is { } r ? new { r.X, r.Y, r.Width, r.Height } : null,
        }, sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_set_rate_limit"), Description(
        "Set the maximum number of actions per second (rate limit). " +
        "Pass 0 to disable rate limiting. Actions over the limit are refused. " +
        "A limit the operator set at startup can be lowered but not raised or disabled.")]
    public Task<string> SetRateLimitAsync(
        [Description("Maximum actions per second. Pass 0 to disable.")] int maxPerSecond, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var ceiling = rateLimiter.OperatorMaxPerSecond;
        if (ceiling > 0 && (maxPerSecond <= 0 || maxPerSecond > ceiling))
            return Task.FromResult(McpResponse.Error("operator_locked",
                $"The operator limited actions to {ceiling} per second; a limit can only be lowered.", sw.ElapsedMilliseconds));

        rateLimiter.SetLimit(maxPerSecond);
        ActionLog.Record("safety_set_rate_limit", $"max={maxPerSecond}", sw.ElapsedMilliseconds, true);
        return Task.FromResult(maxPerSecond > 0
            ? McpResponse.Confirm($"Rate limit set to {maxPerSecond} actions/second.", sw.ElapsedMilliseconds)
            : McpResponse.Confirm("Rate limit disabled.", sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_status"), Description(
        "Get the current safety status — emergency stop, safe zone, rate limit, " +
        "running monitors, and action history count.")]
    public Task<string> StatusAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var activeMonitors = monitors.ListActive();
        var zone = safeZone.Current;
        return Task.FromResult(McpResponse.Ok(new
        {
            emergencyStopped = emergencyStop.IsTriggered,
            stoppedBy = emergencyStop.TriggeredBy?.ToString().ToLowerInvariant(),
            safeZone = zone is not null ? new { zone.Window, region = zone.Region?.ToString(), operatorLocked = safeZone.IsOperatorLocked } : null,
            rateLimit = new
            {
                maxPerSecond = rateLimiter.MaxPerSecond,
                operatorMaxPerSecond = rateLimiter.OperatorMaxPerSecond,
                currentRate = rateLimiter.CurrentRate,
            },
            activeMonitors = activeMonitors.Count,
            actionHistoryCount = ActionLog.Count,
            pendingConfirmations = ConfirmationManager.ListPending().Count,
        }, sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_confirm_before"), Description(
        "Request user confirmation before performing a dangerous action. " +
        "Creates a JSON confirmation file for the user to approve or deny by setting its status. " +
        "Returns the confirmation ID and file path. " +
        "Use safety_check_confirmation to poll for the user's answer; the agent cannot answer its own request.")]
    public static Task<string> ConfirmBeforeAsync(
        [Description("Description of the action requiring confirmation.")] string action,
        [Description("Reason why confirmation is needed.")] string reason,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(action))
            return Task.FromResult(McpResponse.Error("invalid_parameter", "action cannot be empty.", sw.ElapsedMilliseconds));
        if (string.IsNullOrWhiteSpace(reason))
            return Task.FromResult(McpResponse.Error("invalid_parameter", "reason cannot be empty.", sw.ElapsedMilliseconds));
        var request = ConfirmationManager.Create(action, reason);

        ActionLog.Record("safety_confirm_before", $"id={request.Id}, action={action}",
            sw.ElapsedMilliseconds, true);

        return Task.FromResult(McpResponse.Ok(new
        {
            request.Id,
            request.Action,
            request.Reason,
            status = request.Status.ToString().ToLowerInvariant(),
            request.FilePath,
            instructions = "Ask the user to edit the JSON file and set status to 'approved' or 'denied', " +
                           "then poll with safety_check_confirmation.",
        }, sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "safety_check_confirmation"), Description(
        "Check the status of a pending confirmation request. " +
        "Returns the current status: pending, approved, or denied. " +
        "The status can change when the JSON file is edited externally.")]
    public static Task<string> CheckConfirmationAsync(
        [Description("Confirmation request ID returned by safety_confirm_before.")] string confirmationId,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(confirmationId))
            return Task.FromResult(McpResponse.Error("invalid_parameter", "confirmationId cannot be empty.", sw.ElapsedMilliseconds));
        var request = ConfirmationManager.Check(confirmationId);

        return Task.FromResult(McpResponse.Ok(new
        {
            request.Id,
            request.Action,
            request.Reason,
            status = request.Status.ToString().ToLowerInvariant(),
            request.FilePath,
            createdAt = request.CreatedAt.ToString("O"),
            resolvedAt = request.ResolvedAt?.ToString("O"),
        }, sw.ElapsedMilliseconds));
    }
}
