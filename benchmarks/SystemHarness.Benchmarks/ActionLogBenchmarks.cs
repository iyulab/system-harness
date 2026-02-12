using BenchmarkDotNet.Attributes;
using SystemHarness.Mcp;

namespace SystemHarness.Benchmarks;

[MemoryDiagnoser]
public class ActionLogBenchmarks
{
    [GlobalSetup]
    public void Setup() => ActionLog.Clear();

    [IterationSetup]
    public void IterationSetup() => ActionLog.Clear();

    [Benchmark(Description = "ActionLog.Record (single)")]
    public void RecordSingle()
    {
        ActionLog.Record("mouse_click", "x=100, y=200", 5, true);
    }

    [Benchmark(Description = "ActionLog.Record (100 entries)")]
    public void Record100()
    {
        for (var i = 0; i < 100; i++)
            ActionLog.Record("mouse_click", $"x={i}, y={i}", i, true);
    }

    [Benchmark(Description = "ActionLog.GetRecent (50 from 200)")]
    public IReadOnlyList<ActionRecord> GetRecent50()
    {
        // Fill the buffer first
        for (var i = 0; i < 200; i++)
            ActionLog.Record("tool", $"p={i}", i, true);
        return ActionLog.GetRecent(50);
    }
}
