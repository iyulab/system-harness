using ChildProcessGuard;

namespace SystemHarness.SimulationTests;

/// <summary>
/// Shared fixture for simulation tests providing process cleanup via Windows Job Objects.
/// Ensures all child processes started during simulation tests are cleaned up,
/// even when assertions fail before cleanup runs.
/// </summary>
public class SimulationFixture : IAsyncLifetime
{
    public ProcessGuardian Guardian { get; } = new();
    public WindowsHarness Harness { get; } = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await Guardian.KillAllProcessesAsync();
        Guardian.Dispose();
        Harness.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Collection definition for simulation tests.
/// Tests run sequentially to avoid desktop state conflicts (focus, clipboard, input).
/// </summary>
[CollectionDefinition("Simulation", DisableParallelization = true)]
public class SimulationDefinition : ICollectionFixture<SimulationFixture>;
