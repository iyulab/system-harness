using System.Text.Json;
using SystemHarness.Mcp.Dispatch;
using SystemHarness.Mcp.Tools;

namespace SystemHarness.Mcp;

/// <summary>
/// The one place where the safety settings are enforced. Every dispatched command passes through
/// <see cref="CheckAsync"/> before its handler runs; a non-null result is the refusal returned instead.
/// Checks, in order: emergency stop, rate limit, the server's own files, safe zone. Command policy is enforced by the shell and
/// process decorators the harness is built with. Every check fails closed — a zone window that cannot be
/// found refuses the command rather than letting it through.
/// </summary>
public sealed class SafetyGate(EmergencyStop emergencyStop, RateLimiter rateLimiter, SafeZone safeZone, IWindow windows)
{
    private const string SafetyCategory = "safety";

    // Input-producing categories the safe zone governs.
    private static readonly HashSet<string> ZoneCategories = new(StringComparer.Ordinal)
    {
        "mouse", "keyboard", "ui", "vision", "dialog", "window",
    };

    private static readonly HashSet<string> ZoneCommands = new(StringComparer.Ordinal)
    {
        "app.close", "app.focus", "desktop.move_window", "record.replay",
    };

    // Not input: these only read the screen or a template.
    private static readonly HashSet<string> ZoneExempt = new(StringComparer.Ordinal)
    {
        "vision.find_image",
    };

    // Commands that write, move, or delete files, and the parameters naming those files.
    private static readonly HashSet<string> FileWriteCategories = new(StringComparer.Ordinal) { "file", "office" };

    private static readonly HashSet<string> FileWriteCommands = new(StringComparer.Ordinal)
    {
        "monitor.start", "session.save", "dialog.fill_file",
    };

    private static readonly string[] FileParams = ["path", "source", "destination", "filePath", "outputPath"];

    private static readonly string[] WindowParams = ["titleOrHandle", "parentTitleOrHandle"];

    private static readonly (string X, string Y)[] PointParams = [("x", "y"), ("fromX", "fromY"), ("toX", "toY")];

    /// <summary>
    /// Returns a refusal response if the command must not run, or null if it may.
    /// </summary>
    public async Task<string?> CheckAsync(CommandDescriptor command, JsonElement? args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!command.IsMutation || command.Category == SafetyCategory)
            return null;

        if (emergencyStop.IsTriggered)
            return McpResponse.Error("emergency_stopped",
                $"Emergency stop is active; '{command.Name}' was not run. " +
                (emergencyStop.TriggeredBy == EmergencyStopSource.Operator
                    ? "The operator stopped the session; restart the server to resume."
                    : "Use safety.resume to resume."));

        if (rateLimiter.RecordAndCheck())
            return McpResponse.Error("rate_limited",
                $"Rate limit of {rateLimiter.MaxPerSecond} actions per second reached; '{command.Name}' was not run.");

        if (FileWriteCategories.Contains(command.Category) || FileWriteCommands.Contains(command.Name))
        {
            foreach (var name in FileParams)
            {
                if (GetString(args, name) is { } path && SessionFiles.Contains(path))
                    return McpResponse.Error("protected_path",
                        $"'{command.Name}' was not run: '{path}' is inside the server's own directory (screenshots, confirmation requests).");
            }
        }

        var zone = safeZone.Current;
        if (zone is not null && IsZoneScoped(command))
            return await CheckZoneAsync(zone, command, args, ct);

        return null;
    }

    private static bool IsZoneScoped(CommandDescriptor command)
        => !ZoneExempt.Contains(command.Name)
           && (ZoneCategories.Contains(command.Category) || ZoneCommands.Contains(command.Name));

    private async Task<string?> CheckZoneAsync(SafeZoneConfig zone, CommandDescriptor command, JsonElement? args, CancellationToken ct)
    {
        var all = await windows.ListAsync(ct);
        var zoneWindow = ToolHelpers.FindWindow(all, zone.Window);
        if (zoneWindow is null)
            return Refuse(command, $"the zone window '{zone.Window}' was not found");

        // Commands whose target is only known after they run cannot be checked beforehand.
        if (command.Name is "vision.click_text" or "record.replay"
            || (command.Name == "vision.click_image" && GetString(args, "titleOrHandle") is null))
            return Refuse(command, "its target cannot be checked before it runs");

        var checkedSomething = false;

        foreach (var name in WindowParams)
        {
            var value = GetString(args, name);
            if (value is null) continue;

            checkedSomething = true;
            var target = ToolHelpers.FindWindow(all, value);
            if (target is null || target.Handle != zoneWindow.Handle)
                return Refuse(command, $"window '{value}' is outside the zone");
        }

        if (command.Category is "mouse" or "vision")
        {
            var rect = ZoneRect(zoneWindow, zone);
            foreach (var (xName, yName) in PointParams)
            {
                if (GetInt(args, xName) is not { } x || GetInt(args, yName) is not { } y) continue;

                checkedSomething = true;
                if (!rect.Contains(x, y))
                    return Refuse(command, $"point ({x}, {y}) is outside the zone");
            }
        }

        // Keystrokes go to whatever window has focus, so a keyboard command always needs the zone in front;
        // so does anything with no explicit target.
        if (command.Category == "keyboard" || !checkedSomething)
        {
            var foreground = await windows.GetForegroundAsync(ct);
            if (foreground is null || foreground.Handle != zoneWindow.Handle)
                return Refuse(command, "the foreground window is outside the zone");
        }

        return null;
    }

    private static Rectangle ZoneRect(WindowInfo window, SafeZoneConfig zone)
        => zone.Region is { } r
            ? new Rectangle(window.Bounds.X + r.X, window.Bounds.Y + r.Y, r.Width, r.Height)
            : window.Bounds;

    private static string Refuse(CommandDescriptor command, string why)
        => McpResponse.Error("outside_safe_zone", $"'{command.Name}' was not run: {why}.");

    private static string? GetString(JsonElement? args, string name)
        => args is { ValueKind: JsonValueKind.Object } a
           && a.TryGetProperty(name, out var v)
           && v.ValueKind == JsonValueKind.String
           && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;

    private static int? GetInt(JsonElement? args, string name)
        => args is { ValueKind: JsonValueKind.Object } a
           && a.TryGetProperty(name, out var v)
           && v.ValueKind == JsonValueKind.Number
           && v.TryGetInt32(out var i)
            ? i
            : null;
}
