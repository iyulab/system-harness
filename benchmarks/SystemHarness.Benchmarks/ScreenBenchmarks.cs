using BenchmarkDotNet.Attributes;
using SystemHarness.Windows;

namespace SystemHarness.Benchmarks;

[MemoryDiagnoser]
public class ScreenBenchmarks : IDisposable
{
    private WindowsScreen _screen = null!;

    [GlobalSetup]
    public void Setup() => _screen = new WindowsScreen();

    [Benchmark(Description = "Screen: Full capture (JPEG 80%, 1024x768)")]
    public async Task CaptureJpeg()
    {
        using var screenshot = await _screen.CaptureAsync(new CaptureOptions
        {
            Format = ImageFormat.Jpeg,
            Quality = 80,
            TargetWidth = 1024,
            TargetHeight = 768,
        });
    }

    [Benchmark(Description = "Screen: Full capture (PNG, native res)")]
    public async Task CapturePng()
    {
        using var screenshot = await _screen.CaptureAsync(new CaptureOptions
        {
            Format = ImageFormat.Png,
            TargetWidth = null,
            TargetHeight = null,
        });
    }

    [Benchmark(Description = "Screen: Full capture (JPEG, native res)")]
    public async Task CaptureJpegNative()
    {
        using var screenshot = await _screen.CaptureAsync(new CaptureOptions
        {
            Format = ImageFormat.Jpeg,
            Quality = 80,
            TargetWidth = null,
            TargetHeight = null,
        });
    }

    public void Dispose() => _screen.Dispose();
}
