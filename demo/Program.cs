// Find solution root
var solutionRoot = FindSolutionRoot();
var envPath = Path.Combine(solutionRoot, "demo", ".env");
var mcpProjectPath = Path.Combine(solutionRoot, "src", "SystemHarness.Mcp", "SystemHarness.Mcp.csproj");

// Parse .env
var env = ParseEnv(envPath);
if (!env.TryGetValue("GPUSTACK_ENDPOINT", out var endpoint) ||
    !env.TryGetValue("GPUSTACK_API_KEY", out var apiKey) ||
    !env.TryGetValue("GPUSTACK_MODEL", out var model))
{
    Console.Error.WriteLine("Error: Missing required .env variables (GPUSTACK_ENDPOINT, GPUSTACK_API_KEY, GPUSTACK_MODEL)");
    Console.Error.WriteLine($"Checked: {envPath}");
    return 1;
}

// Parse CLI args
string? scenarioFilter = null;
string? categoryFilter = null;
bool listOnly = false;
int timeoutSeconds = 120;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--scenario" when i + 1 < args.Length:
            scenarioFilter = args[++i];
            break;
        case "--category" when i + 1 < args.Length:
            categoryFilter = args[++i];
            break;
        case "--timeout" when i + 1 < args.Length:
            timeoutSeconds = int.Parse(args[++i]);
            break;
        case "--list":
            listOnly = true;
            break;
    }
}

// Filter scenarios
IReadOnlyList<Scenario> scenarios = Scenarios.All;
if (scenarioFilter != null)
    scenarios = Scenarios.All
        .Where(s => s.Name.Equals(scenarioFilter, StringComparison.OrdinalIgnoreCase))
        .ToList();
else if (categoryFilter != null)
    scenarios = Scenarios.All
        .Where(s => s.Category.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase))
        .ToList();

// --list: print scenario list and exit
if (listOnly)
{
    PrintHeader();
    Console.WriteLine();
    foreach (var group in Scenarios.All.GroupBy(s => s.Category))
    {
        Console.WriteLine($"  {group.Key}:");
        foreach (var s in group)
            Console.WriteLine($"    {s.Name,-25} {Truncate(s.Task, 60)}");
        Console.WriteLine();
    }
    Console.WriteLine($"  Total: {Scenarios.All.Count} scenarios");
    return 0;
}

if (scenarios.Count == 0)
{
    Console.Error.WriteLine("No scenarios found matching filter.");
    return 1;
}

// Print header
PrintHeader();
Console.WriteLine();
Console.WriteLine($"  Endpoint : {endpoint}");
Console.WriteLine($"  Model    : {model}");
Console.WriteLine($"  Scenarios: {scenarios.Count}");
Console.WriteLine($"  Timeout  : {timeoutSeconds}s per scenario");
Console.WriteLine();

// Resolve MCP server command — prefer pre-built exe, fallback to dotnet run
string mcpCommand;
string[] mcpArgs;
var mcpExePath = Path.Combine(solutionRoot, "src", "SystemHarness.Mcp", "bin", "Debug",
    "net10.0-windows10.0.19041.0", "SystemHarness.Mcp.exe");

if (File.Exists(mcpExePath))
{
    mcpCommand = mcpExePath;
    mcpArgs = [];
}
else
{
    mcpCommand = "dotnet";
    mcpArgs = ["run", "--project", mcpProjectPath, "--no-launch-profile"];
}

// Create agent
McpAIAgent agent;
try
{
    Console.Write("  Starting MCP server and connecting AI... ");
    agent = await McpAIAgent.CreateAsync(new McpAIAgentConfig
    {
        McpServerCommand = mcpCommand,
        McpServerArgs = mcpArgs,
        AiEndpoint = endpoint.TrimEnd('/') + "/v1",
        AiApiKey = apiKey,
        AiModel = model,
        ScenarioTimeoutSeconds = timeoutSeconds,
    });
    Console.WriteLine($"OK ({agent.ToolCount} tools loaded)");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine("FAILED");
    Console.Error.WriteLine($"  Error: {ex.Message}");
    return 1;
}

// Run scenarios
await using (agent)
{
    var results = new List<ScenarioResult>();
    int total = scenarios.Count;

    for (int i = 0; i < total; i++)
    {
        var scenario = scenarios[i];
        Console.WriteLine($"[{i + 1}/{total}] {scenario.Name} ({scenario.Category})");
        Console.WriteLine($"  Task: {Truncate(scenario.Task, 76)}");

        var result = await agent.RunAsync(scenario);
        results.Add(result);

        if (result.Success)
        {
            Console.WriteLine($"  Duration: {result.Elapsed.TotalSeconds:F1}s | Tools: {result.ToolCallCount} calls");
            Console.WriteLine($"  Result: PASS");
            Console.WriteLine($"  Response: {Truncate(result.Response, 120)}");
        }
        else
        {
            Console.WriteLine($"  Duration: {result.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"  Result: FAIL");
            Console.WriteLine($"  Error: {Truncate(result.Error ?? "Unknown", 120)}");
        }

        // Clean up orphaned processes from failed scenarios
        KillOrphaned("notepad");

        Console.WriteLine();
    }

    // Summary
    int passed = results.Count(r => r.Success);
    PrintSeparator();
    Console.WriteLine($"  Summary: {passed}/{total} PASS, {total - passed} FAIL");
    PrintSeparator();

    // Write JSON results file
    var resultsPath = Path.Combine(solutionRoot, "demo", "results.json");
    var jsonResults = results.Select(r => new
    {
        r.ScenarioName,
        r.Success,
        r.Response,
        r.ToolCallCount,
        ElapsedSeconds = Math.Round(r.Elapsed.TotalSeconds, 1),
        r.Error,
    });
    var json = System.Text.Json.JsonSerializer.Serialize(jsonResults, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
    });
    File.WriteAllText(resultsPath, json);
    Console.WriteLine($"  Results written to: {resultsPath}");
}

return 0;

// --- Helper methods ---

static void PrintHeader()
{
    PrintSeparator();
    Console.WriteLine("  SystemHarness AI Demo \u2014 MCP Scenario Runner");
    PrintSeparator();
}

static void PrintSeparator()
{
    Console.WriteLine(new string('\u2550', 54));
}

static string Truncate(string text, int maxLength)
{
    text = text.ReplaceLineEndings(" ");
    return text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 3), "...");
}

static string FindSolutionRoot()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir, "SystemHarness.slnx")))
            return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }
    throw new InvalidOperationException(
        "Could not find solution root (SystemHarness.slnx). Run from the repository directory.");
}

static void KillOrphaned(string processName)
{
    foreach (var p in System.Diagnostics.Process.GetProcessesByName(processName))
    {
        try { p.Kill(); } catch { /* ignore */ }
        p.Dispose();
    }
}

static Dictionary<string, string> ParseEnv(string path)
{
    var env = new Dictionary<string, string>();
    if (!File.Exists(path)) return env;
    foreach (var line in File.ReadAllLines(path))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('='))
            continue;
        var idx = trimmed.IndexOf('=');
        env[trimmed[..idx].Trim()] = trimmed[(idx + 1)..].Trim();
    }
    return env;
}
