namespace SystemHarness.Tests.Safety;

[Trait("Category", "CI")]
public class EmergencyStopTests
{
    [Fact]
    public void InitialState_NotTriggered()
    {
        using var stop = new EmergencyStop();
        Assert.False(stop.IsTriggered);
        Assert.False(stop.Token.IsCancellationRequested);
    }

    [Fact]
    public void Trigger_CancelsToken()
    {
        using var stop = new EmergencyStop();
        stop.Trigger();
        Assert.True(stop.IsTriggered);
        Assert.True(stop.Token.IsCancellationRequested);
    }

    [Fact]
    public void Trigger_RaisesEvent()
    {
        using var stop = new EmergencyStop();
        var triggered = false;
        stop.Triggered += () => triggered = true;

        stop.Trigger();
        Assert.True(triggered);
    }

    [Fact]
    public void Trigger_MultipleTimes_DoesNotThrow()
    {
        using var stop = new EmergencyStop();
        stop.Trigger();
        stop.Trigger(); // should not throw
        Assert.True(stop.IsTriggered);
    }

    [Fact]
    public void Reset_CreatesNewToken()
    {
        using var stop = new EmergencyStop();
        stop.Trigger();
        Assert.True(stop.IsTriggered);

        var oldToken = stop.Token;
        stop.Reset();

        Assert.False(stop.IsTriggered);
        Assert.False(stop.Token.IsCancellationRequested);
        Assert.True(oldToken.IsCancellationRequested); // old token stays cancelled
    }

    [Fact]
    public async Task Token_CancelsRunningOperation()
    {
        using var stop = new EmergencyStop();

        var task = Task.Run(async () =>
        {
            await Task.Delay(Timeout.Infinite, stop.Token);
        });

        await Task.Delay(50); // let task start
        stop.Trigger();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var stop = new EmergencyStop();
        stop.Dispose();
        stop.Dispose(); // should not throw
    }

    [Fact]
    public void TriggerAfterDispose_DoesNotThrow()
    {
        var stop = new EmergencyStop();
        stop.Dispose();
        stop.Trigger(); // should not throw
    }

    [Fact]
    public void ResetAfterDispose_DoesNotThrow()
    {
        var stop = new EmergencyStop();
        stop.Dispose();
        stop.Reset(); // should not throw
    }

    [Fact]
    public void Reset_ThenRetrigger_Works()
    {
        using var stop = new EmergencyStop();
        stop.Trigger();
        Assert.True(stop.IsTriggered);

        stop.Reset();
        Assert.False(stop.IsTriggered);

        stop.Trigger();
        Assert.True(stop.IsTriggered);
        Assert.True(stop.Token.IsCancellationRequested);
    }

    [Fact]
    public void Triggered_Event_FiresEveryCall()
    {
        using var stop = new EmergencyStop();
        int count = 0;
        stop.Triggered += () => count++;

        stop.Trigger();
        stop.Trigger(); // event fires again even if already triggered

        Assert.Equal(2, count);
    }

    [Fact]
    public void Triggered_Event_MultipleSubscribers()
    {
        using var stop = new EmergencyStop();
        int count1 = 0, count2 = 0;
        stop.Triggered += () => count1++;
        stop.Triggered += () => count2++;

        stop.Trigger();

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void Reset_NewToken_IndependentOfOld()
    {
        using var stop = new EmergencyStop();
        var token1 = stop.Token;
        stop.Trigger();

        stop.Reset();
        var token2 = stop.Token;

        Assert.True(token1.IsCancellationRequested);
        Assert.False(token2.IsCancellationRequested);
        Assert.NotEqual(token1, token2);
    }
}
