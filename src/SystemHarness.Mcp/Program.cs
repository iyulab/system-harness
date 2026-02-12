using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SystemHarness;
using SystemHarness.Apps.Office;
using SystemHarness.Mcp;
using SystemHarness.Mcp.Dispatch;
using SystemHarness.Mcp.Tools;
using SystemHarness.Windows;

var builder = Host.CreateApplicationBuilder(args);

// Route all logs to stderr to keep stdout clean for MCP protocol
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register SystemHarness (Windows)
builder.Services.AddSystemHarness();

// Register Office document readers (Tier 1: file-based, no Office installation required)
builder.Services.AddOfficeReaders();

// Register safety and monitoring services
builder.Services.AddSingleton<EmergencyStop>();
builder.Services.AddSingleton<MonitorManager>();

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
            Version = "0.26.0"
        };
        options.ServerInstructions = """
            SystemHarness MCP server — 172 commands for programmatic computer control.

            ## 3 Tools: help, do, get

            This server uses a command dispatch pattern. Instead of 172 separate tools,
            all commands are accessed through 3 tools:

            - **help(topic?)** — Discover commands. No args = categories. Category name = commands. Command name = parameters.
            - **do(command, params?)** — Execute mutation commands (click, type, write, start, stop, etc.)
            - **get(command, params?)** — Execute read-only queries (list, read, capture, find, etc.)

            ## Quick Start

            1. help() → see 25 categories
            2. help("mouse") → see mouse commands
            3. help("mouse.click") → see parameters
            4. do("mouse.click", '{"x":100,"y":200}') → click at (100, 200)
            5. get("window.list") → list all windows

            ## Command Naming

            Commands use dot notation: {category}.{action}[_{qualifier}]
            Examples: mouse.click, file.read_bytes, vision.click_and_verify

            ## Response Format

            All commands return JSON: {"ok": true/false, "data": {...}, "meta": {"ts": "...", "ms": N}}
            """;
    })
    .WithStdioServerTransport()
    .WithTools<DispatchTools>();

var app = builder.Build();

// Build command registry from all tool classes
var registry = app.Services.GetRequiredService<CommandRegistry>();
CommandRegistrar.RegisterAll(registry, app.Services);

await app.RunAsync();
