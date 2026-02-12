using BenchmarkDotNet.Attributes;
using SystemHarness.Windows;

namespace SystemHarness.Benchmarks;

[MemoryDiagnoser]
public class InputBenchmarks
{
    private WindowsMouse _mouse = null!;

    [GlobalSetup]
    public void Setup()
    {
        _mouse = new WindowsMouse();
    }

    [Benchmark(Description = "Mouse: GetPosition")]
    public Task<(int, int)> MouseGetPosition()
    {
        return _mouse.GetPositionAsync();
    }

    [Benchmark(Description = "Mouse: Move")]
    public Task MouseMove()
    {
        return _mouse.MoveAsync(500, 500);
    }
}
