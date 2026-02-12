namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests error recovery, timeout handling, and cancellation behavior.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Local")]
public class ErrorRecoveryTests : SimulationTestBase
{
    public ErrorRecoveryTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task FocusNonExistentWindow_ThrowsHarnessException()
    {
        await Assert.ThrowsAsync<HarnessException>(async () =>
        {
            await Window.FocusAsync("NonExistentWindow_" + Guid.NewGuid().ToString("N"));
        });
    }

    [Fact]
    public async Task CloseNonExistentWindow_ThrowsHarnessException()
    {
        await Assert.ThrowsAsync<HarnessException>(async () =>
        {
            await Window.CloseAsync("NonExistentWindow_" + Guid.NewGuid().ToString("N"));
        });
    }

    [Fact]
    public async Task WaitForWindow_TimesOut()
    {
        await Assert.ThrowsAsync<HarnessException>(async () =>
        {
            await Window.WaitForWindowAsync(
                "NonExistentWindow_" + Guid.NewGuid().ToString("N"),
                TimeSpan.FromMilliseconds(500));
        });
    }

    [Fact]
    public async Task CancellationToken_StopsOperation()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await Window.WaitForWindowAsync(
                "WaitForeverWindow_" + Guid.NewGuid().ToString("N"),
                TimeSpan.FromSeconds(30),
                cts.Token);
        });
    }

    [Fact]
    public async Task KillNonExistentProcess_DoesNotThrow()
    {
        // Using a PID that's very unlikely to exist
        await Process.KillAsync(999999);
    }

    [Fact]
    public async Task WaitForExit_AlreadyExited()
    {
        var options = new ProcessStartOptions
        {
            Arguments = "/c echo done",
            RedirectOutput = true,
        };

        var proc = await Process.StartAsync("cmd.exe", options);
        await Task.Delay(1000); // Let it finish

        var exited = await Process.WaitForExitAsync(proc.Pid, TimeSpan.FromSeconds(1));
        Assert.True(exited);
    }

    [Fact]
    public async Task EmergencyStop_CancelsOperations()
    {
        var stop = new EmergencyStop();

        try
        {
            // Trigger stop after 100ms
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                stop.Trigger();
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                // This should be cancelled by the emergency stop
                await Task.Delay(TimeSpan.FromSeconds(30), stop.Token);
            });

            Assert.True(stop.IsTriggered);
        }
        finally
        {
            stop.Dispose();
        }
    }

    [Fact]
    public async Task RetryHelper_SucceedsEventually()
    {
        var attempt = 0;

        await RetryAsync(async () =>
        {
            attempt++;
            if (attempt < 3)
                throw new InvalidOperationException("Not ready yet");
            await Task.CompletedTask;
        }, TimeSpan.FromSeconds(5));

        Assert.True(attempt >= 3);
    }

    [Fact]
    public async Task RetryHelper_ThrowsOnTimeout()
    {
        await Assert.ThrowsAsync<HarnessException>(async () =>
        {
            await RetryAsync(() =>
                Task.FromException(new InvalidOperationException("Always fails")),
                TimeSpan.FromMilliseconds(500));
        });
    }
}
