using BenchmarkDotNet.Attributes;
using SystemHarness.Windows;

namespace SystemHarness.Benchmarks;

[MemoryDiagnoser]
public class ShellBenchmarks
{
    private WindowsShell _shell = null!;

    [GlobalSetup]
    public void Setup() => _shell = new WindowsShell();

    [Benchmark(Description = "Shell: echo (cmd.exe)")]
    public Task<ShellResult> ShellEcho()
    {
        return _shell.RunAsync("cmd", "/C echo benchmark");
    }

    [Benchmark(Description = "Shell: dir (cmd.exe)")]
    public Task<ShellResult> ShellDir()
    {
        return _shell.RunAsync("cmd", "/C dir /B");
    }
}
