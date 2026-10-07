using SystemHarness.Mcp;

namespace SystemHarness.Tests.Safety;

[Collection("StaticState")]
[Trait("Category", "CI")]
public class SafetyInfrastructureTests
{
    private readonly SafeZone _zone = new();
    private readonly RateLimiter _rate = new();

    // --- SafeZone Tests ---

    [Fact]
    public void SafeZone_InitiallyNull()
    {
        _zone.Clear(); // reset
        Assert.Null(_zone.Current);
    }

    [Fact]
    public void SafeZone_Set_StoresWindowAndRegion()
    {
        try
        {
            _zone.Set("Notepad", new Rectangle(10, 20, 100, 200));
            var zone = _zone.Current;

            Assert.NotNull(zone);
            Assert.Equal("Notepad", zone.Window);
            Assert.NotNull(zone.Region);
            Assert.Equal(10, zone.Region.Value.X);
            Assert.Equal(20, zone.Region.Value.Y);
            Assert.Equal(100, zone.Region.Value.Width);
            Assert.Equal(200, zone.Region.Value.Height);
        }
        finally
        {
            _zone.Clear();
        }
    }

    [Fact]
    public void SafeZone_SetWithoutRegion()
    {
        try
        {
            _zone.Set("Calculator");
            var zone = _zone.Current;

            Assert.NotNull(zone);
            Assert.Equal("Calculator", zone.Window);
            Assert.Null(zone.Region);
        }
        finally
        {
            _zone.Clear();
        }
    }

    [Fact]
    public void SafeZone_Clear_SetsToNull()
    {
        _zone.Set("Test");
        _zone.Clear();
        Assert.Null(_zone.Current);
    }

    // --- RateLimiter Tests ---

    [Fact]
    public void RateLimiter_Disabled_NeverExceeds()
    {
        _rate.SetLimit(0);
        Assert.Equal(0, _rate.MaxPerSecond);

        for (var i = 0; i < 100; i++)
            Assert.False(_rate.RecordAndCheck());
    }

    [Fact]
    public void RateLimiter_Enabled_ExceedsWhenOverLimit()
    {
        try
        {
            _rate.SetLimit(5);
            Assert.Equal(5, _rate.MaxPerSecond);

            // First 5 should not exceed
            for (var i = 0; i < 5; i++)
                Assert.False(_rate.RecordAndCheck());

            // 6th should exceed
            Assert.True(_rate.RecordAndCheck());
        }
        finally
        {
            _rate.SetLimit(0);
        }
    }

    [Fact]
    public void RateLimiter_SetLimit_ClearsHistory()
    {
        try
        {
            _rate.SetLimit(2);
            _rate.RecordAndCheck();
            _rate.RecordAndCheck();

            // Reset clears history
            _rate.SetLimit(2);
            Assert.Equal(0, _rate.CurrentRate);
            Assert.False(_rate.RecordAndCheck()); // 1st after reset
        }
        finally
        {
            _rate.SetLimit(0);
        }
    }

    // --- ActionLog Tests ---

    [Fact]
    public void ActionLog_Record_StoresEntries()
    {
        ActionLog.Clear();
        ActionLog.Record("test_tool", "param=1", 42, true);

        var entries = ActionLog.GetRecent(10);
        Assert.Single(entries);
        Assert.Equal("test_tool", entries[0].Tool);
        Assert.Equal("param=1", entries[0].Parameters);
        Assert.Equal(42, entries[0].DurationMs);
        Assert.True(entries[0].Success);
    }

    [Fact]
    public void ActionLog_GetRecent_ReturnsNewestFirst()
    {
        ActionLog.Clear();
        ActionLog.Record("tool_1", null, 10, true);
        ActionLog.Record("tool_2", null, 20, true);
        ActionLog.Record("tool_3", null, 30, true);

        var entries = ActionLog.GetRecent(2);
        Assert.Equal(2, entries.Count);
        Assert.Equal("tool_3", entries[0].Tool);
        Assert.Equal("tool_2", entries[1].Tool);
    }

    [Fact]
    public void ActionLog_Clear_EmptiesLog()
    {
        ActionLog.Clear();
        ActionLog.Record("test", null, 1, true);
        Assert.Equal(1, ActionLog.Count);

        ActionLog.Clear();
        Assert.Equal(0, ActionLog.Count);
    }

    [Fact]
    public void ActionLog_RingBuffer_LimitsSize()
    {
        ActionLog.Clear();
        // Record more than max (200)
        for (var i = 0; i < 210; i++)
            ActionLog.Record($"tool_{i}", null, i, true);

        Assert.True(ActionLog.Count <= 200);
        ActionLog.Clear();
    }

    // --- EmergencyStop Tests ---

    [Fact]
    public void EmergencyStop_InitiallyNotTriggered()
    {
        using var es = new EmergencyStop();
        Assert.False(es.IsTriggered);
        Assert.False(es.Token.IsCancellationRequested);
    }

    [Fact]
    public void EmergencyStop_Trigger_CancelsToken()
    {
        using var es = new EmergencyStop();
        es.Trigger();
        Assert.True(es.IsTriggered);
        Assert.True(es.Token.IsCancellationRequested);
    }

    [Fact]
    public void EmergencyStop_Reset_CreatesNewToken()
    {
        using var es = new EmergencyStop();
        es.Trigger();
        var oldToken = es.Token;

        es.Reset();
        Assert.False(es.IsTriggered);
        Assert.False(es.Token.IsCancellationRequested);
        Assert.True(oldToken.IsCancellationRequested); // Old token stays cancelled
    }

    [Fact]
    public void EmergencyStop_TriggeredEvent_Fires()
    {
        using var es = new EmergencyStop();
        var fired = false;
        es.Triggered += () => fired = true;

        es.Trigger();
        Assert.True(fired);
    }

    [Fact]
    public void EmergencyStop_DoubleTrigger_SafelyIgnored()
    {
        using var es = new EmergencyStop();
        es.Trigger();
        es.Trigger(); // Should not throw
        Assert.True(es.IsTriggered);
    }
}
