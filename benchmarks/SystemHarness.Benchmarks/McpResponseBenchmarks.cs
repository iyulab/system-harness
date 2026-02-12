using BenchmarkDotNet.Attributes;
using SystemHarness.Mcp;

namespace SystemHarness.Benchmarks;

[MemoryDiagnoser]
public class McpResponseBenchmarks
{
    private static readonly object SimpleData = new { name = "test", value = 42 };
    private static readonly object NestedData = new
    {
        window = new { handle = "0x1234", title = "Notepad", x = 100, y = 200, width = 800, height = 600 },
        changed = true,
        timestamp = "2026-01-01T00:00:00Z",
    };
    private static readonly IReadOnlyList<object> ItemsList = Enumerable.Range(0, 10)
        .Select(i => (object)new { id = i, name = $"item_{i}", active = i % 2 == 0 })
        .ToList();

    [Benchmark(Description = "McpResponse.Ok (simple)")]
    public string OkSimple() => McpResponse.Ok(SimpleData, 5);

    [Benchmark(Description = "McpResponse.Ok (nested)")]
    public string OkNested() => McpResponse.Ok(NestedData, 12);

    [Benchmark(Description = "McpResponse.Items (10 items)")]
    public string Items10() => McpResponse.Items(ItemsList, 3);

    [Benchmark(Description = "McpResponse.Error")]
    public string Error() => McpResponse.Error("not_found", "Window not found: 'Notepad'", 2);

    [Benchmark(Description = "McpResponse.Confirm")]
    public string Confirm() => McpResponse.Confirm("Safe zone cleared.", 1);

    [Benchmark(Description = "McpResponse.Check (true)")]
    public string CheckTrue() => McpResponse.Check(true, "Process is running", 1);

    [Benchmark(Description = "McpResponse.Content (markdown)")]
    public string ContentMarkdown() => McpResponse.Content("# Title\n\nSome **bold** text.", "markdown", 2);
}
