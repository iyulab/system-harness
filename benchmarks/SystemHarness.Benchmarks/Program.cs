using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using SystemHarness.Benchmarks;

// InProcess toolchain required for net10.0-windows TFM (BDN can't generate out-of-process projects for this TFM)
var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance));

if (args.Length > 0 && args[0] == "--all")
{
    BenchmarkRunner.Run([
        typeof(ShellBenchmarks),
        typeof(ScreenBenchmarks),
        typeof(InputBenchmarks),
        typeof(McpResponseBenchmarks),
        typeof(ActionLogBenchmarks),
    ], config);
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(ShellBenchmarks).Assembly).Run(args, config);
}
