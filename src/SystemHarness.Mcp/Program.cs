using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SystemHarness;
using SystemHarness.Apps.Office;
using SystemHarness.Mcp;
using SystemHarness.Mcp.Dispatch;
using SystemHarness.Mcp.Tools;
using SystemHarness.Mcp.Update;
using SystemHarness.Windows;

var builder = Host.CreateApplicationBuilder(args);

// Operator settings (command line, e.g. --rate-limit=5). The agent can tighten these but not loosen them.
var config = builder.Configuration;
var autoUpdate = config.GetValue("auto-update", false);
var commandPolicy = config.GetValue("command-policy", "default");
var operatorRateLimit = config.GetValue("rate-limit", 0);
var operatorZone = config.GetValue<string?>("safe-zone", null);
var stopHotkey = config.GetValue("stop-hotkey", true);

// Apply a staged update before anything else (rename .update -> exe) — only when updates are enabled
if (autoUpdate)
    AutoUpdater.ApplyPendingUpdate();

// Route all logs to stderr to keep stdout clean for MCP protocol
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register SystemHarness (Windows). The command policy guards shell commands and process starts.
builder.Services.AddSystemHarness(new HarnessOptions
{
    CommandPolicy = commandPolicy switch
    {
        "default" => CommandPolicy.CreateDefault(),
        "none" => null,
        _ => throw new ArgumentException($"Unknown --command-policy '{commandPolicy}'. Use 'default' or 'none'."),
    },
});

// Register Office document readers (Tier 1: file-based, no Office installation required)
builder.Services.AddOfficeReaders();

// Register safety and monitoring services
builder.Services.AddSingleton<EmergencyStop>();
builder.Services.AddSingleton<MonitorManager>();
builder.Services.AddSingleton(_ =>
{
    var limiter = new RateLimiter();
    if (operatorRateLimit > 0)
        limiter.SetOperatorLimit(operatorRateLimit);
    return limiter;
});
builder.Services.AddSingleton(_ =>
{
    var zone = new SafeZone();
    if (!string.IsNullOrWhiteSpace(operatorZone))
        zone.SetByOperator(operatorZone);
    return zone;
});
builder.Services.AddSingleton<SafetyGate>();

// Register auto-updater
builder.Services.AddSingleton(_ => new AutoUpdater(autoUpdate));

// Register command dispatch infrastructure
builder.Services.AddSingleton<CommandRegistry>();
CommandRegistrar.RegisterToolTypes(builder.Services);

// Register MCP server with 3 dispatch tools (help, do, get)
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "system-harness",
            Version = typeof(DispatchTools).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion.Split('+')[0] ?? "0.0.0",
        };
        options.ServerInstructions = """
            SystemHarness MCP server — commands for programmatic computer control.

            ## 3 Tools: help, do, get

            This server uses a command dispatch pattern. Instead of one tool per command,
            all commands are accessed through 3 tools:

            - **help(topic?)** — Discover commands. No args = categories. Category name = commands. Command name = parameters.
            - **do(command, params?)** — Execute mutation commands (click, type, write, start, stop, etc.)
            - **get(command, params?)** — Execute read-only queries (list, read, capture, find, etc.)

            ## Quick Start

            1. help() → see the categories
            2. help("mouse") → see mouse commands
            3. help("mouse.click") → see parameters
            4. do("mouse.click", '{"x":100,"y":200}') → click at (100, 200)
            5. get("window.list") → list all windows

            ## Command Naming

            Commands use dot notation: {category}.{action}[_{qualifier}]
            Examples: mouse.click, file.read_bytes, vision.click_and_verify

            ## Response Format

            All commands return JSON: {"ok": true/false, "data": {...}, "meta": {"ts": "...", "ms": N}}

            ## Safety

            Mutations can be refused before they run: emergency_stopped (an emergency stop is active),
            rate_limited, outside_safe_zone (see safety.set_zone), policy_blocked (the command policy
            blocks that program). A refusal means nothing was done.
            """;
    })
    .WithStdioServerTransport()
    .WithTools<DispatchTools>();

var app = builder.Build();

// Build command registry from all tool classes
var registry = app.Services.GetRequiredService<CommandRegistry>();
CommandRegistrar.RegisterAll(registry, app.Services);

// An emergency stop also stops every running monitor
var emergencyStop = app.Services.GetRequiredService<EmergencyStop>();
var monitors = app.Services.GetRequiredService<MonitorManager>();
emergencyStop.Triggered += () =>
{
    foreach (var m in monitors.ListActive())
        monitors.Stop(m.Id);
};

// The operator's hotkey (Ctrl+Shift+Escape) stops the session; only a restart resumes it
EmergencyStopHook? hook = null;
if (stopHotkey)
{
    try
    {
        hook = new EmergencyStopHook(emergencyStop);
        hook.Start();
    }
    catch (Exception ex)
    {
        ServerLog.StopHotkeyUnavailable(
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SystemHarness.Mcp"), ex);
        hook?.Dispose();
        hook = null;
    }
}

// Fire background update check (non-blocking) — opt-in with --auto-update=true
if (autoUpdate)
{
    var updater = app.Services.GetRequiredService<AutoUpdater>();
    _ = updater.BackgroundCheckAsync(app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
}

await app.RunAsync();
hook?.Dispose();

internal static partial class ServerLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Emergency stop hotkey is unavailable; the session can only be stopped with safety.emergency_stop")]
    public static partial void StopHotkeyUnavailable(ILogger logger, Exception exception);
}
