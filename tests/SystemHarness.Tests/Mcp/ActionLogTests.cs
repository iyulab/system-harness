using SystemHarness.Mcp;

namespace SystemHarness.Tests.Mcp;

[Collection("StaticState")]
[Trait("Category", "CI")]
public class ActionLogTests : IDisposable
{
    public ActionLogTests() => ActionLog.Clear();
    public void Dispose() => ActionLog.Clear();

    [Fact]
    public void Record_StoresAction()
    {
        ActionLog.Record("test_tool", "param=value", 100, true);
        var recent = ActionLog.GetRecent(10);

        Assert.Single(recent);
        Assert.Equal("test_tool", recent[0].Tool);
        Assert.Equal("param=value", recent[0].Parameters);
        Assert.Equal(100, recent[0].DurationMs);
        Assert.True(recent[0].Success);
    }

    [Fact]
    public void GetRecent_ReturnsNewestFirst()
    {
        ActionLog.Record("first", null, 1, true);
        ActionLog.Record("second", null, 2, true);
        ActionLog.Record("third", null, 3, true);

        var recent = ActionLog.GetRecent(10);

        Assert.Equal(3, recent.Count);
        Assert.Equal("third", recent[0].Tool);
        Assert.Equal("first", recent[2].Tool);
    }

    [Fact]
    public void GetRecent_LimitsCount()
    {
        for (int i = 0; i < 10; i++)
            ActionLog.Record($"tool_{i}", null, i, true);

        var recent = ActionLog.GetRecent(3);
        Assert.Equal(3, recent.Count);
    }

    [Fact]
    public void Count_ReflectsEntries()
    {
        Assert.Equal(0, ActionLog.Count);
        ActionLog.Record("a", null, 1, true);
        Assert.Equal(1, ActionLog.Count);
        ActionLog.Record("b", null, 2, false);
        Assert.Equal(2, ActionLog.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        ActionLog.Record("a", null, 1, true);
        ActionLog.Record("b", null, 2, true);
        Assert.Equal(2, ActionLog.Count);

        ActionLog.Clear();
        Assert.Equal(0, ActionLog.Count);
        Assert.Empty(ActionLog.GetRecent(10));
    }

    [Fact]
    public void Record_FailedAction_Stored()
    {
        ActionLog.Record("failing_tool", "bad_param", 50, false);
        var recent = ActionLog.GetRecent(1);

        Assert.Single(recent);
        Assert.False(recent[0].Success);
    }

    [Fact]
    public void Record_NullParameters_AllowedAndStored()
    {
        ActionLog.Record("tool", null, 0, true);
        var recent = ActionLog.GetRecent(1);

        Assert.Single(recent);
        Assert.Null(recent[0].Parameters);
    }

    [Fact]
    public void GetRecent_Zero_ReturnsEmpty()
    {
        ActionLog.Record("a", null, 1, true);
        var recent = ActionLog.GetRecent(0);
        Assert.Empty(recent);
    }

    [Fact]
    public void GetRecent_ExceedsCount_ReturnsAll()
    {
        ActionLog.Record("a", null, 1, true);
        ActionLog.Record("b", null, 2, true);
        var recent = ActionLog.GetRecent(100);

        Assert.Equal(2, recent.Count);
    }

    [Fact]
    public void Record_RingBuffer_EvictsOldest()
    {
        // Fill beyond MaxActions (200)
        for (int i = 0; i < 210; i++)
            ActionLog.Record($"tool_{i}", null, i, true);

        Assert.True(ActionLog.Count <= 200, $"Count should not exceed 200, got {ActionLog.Count}");
        var recent = ActionLog.GetRecent(5);
        // Newest should be tool_209
        Assert.Equal("tool_209", recent[0].Tool);
    }

    [Fact]
    public void Record_TimestampIsUtc()
    {
        var before = DateTime.UtcNow;
        ActionLog.Record("timing", null, 0, true);
        var after = DateTime.UtcNow;

        var recent = ActionLog.GetRecent(1);
        Assert.InRange(recent[0].Timestamp, before, after);
    }

    [Fact]
    public void Record_EmptyList_GetRecentReturnsEmpty()
    {
        var recent = ActionLog.GetRecent(10);
        Assert.Empty(recent);
    }

    [Fact]
    public void ActionRecord_HasExpectedProperties()
    {
        var record = new ActionRecord(
            DateTime.UtcNow, "test", "params", 42, true);

        Assert.Equal("test", record.Tool);
        Assert.Equal("params", record.Parameters);
        Assert.Equal(42, record.DurationMs);
        Assert.True(record.Success);
    }
}
