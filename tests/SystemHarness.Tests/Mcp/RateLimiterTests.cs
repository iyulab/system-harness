using SystemHarness.Mcp;

namespace SystemHarness.Tests.Mcp;

[Collection("StaticState")]
[Trait("Category", "CI")]
public class RateLimiterTests : IDisposable
{
    public RateLimiterTests() => RateLimiter.SetLimit(0);
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        RateLimiter.SetLimit(0);
    }

    // --- SetLimit / MaxPerSecond ---

    [Fact]
    public void MaxPerSecond_DefaultIsZero()
    {
        Assert.Equal(0, RateLimiter.MaxPerSecond);
    }

    [Fact]
    public void SetLimit_PositiveValue_UpdatesMaxPerSecond()
    {
        RateLimiter.SetLimit(10);
        Assert.Equal(10, RateLimiter.MaxPerSecond);
    }

    [Fact]
    public void SetLimit_Zero_Disables()
    {
        RateLimiter.SetLimit(10);
        RateLimiter.SetLimit(0);
        Assert.Equal(0, RateLimiter.MaxPerSecond);
    }

    [Fact]
    public void SetLimit_Negative_ClampsToZero()
    {
        RateLimiter.SetLimit(-5);
        Assert.Equal(0, RateLimiter.MaxPerSecond);
    }

    [Fact]
    public void SetLimit_ClearsTimestamps()
    {
        RateLimiter.SetLimit(100);
        RateLimiter.RecordAndCheck();
        RateLimiter.RecordAndCheck();
        Assert.True(RateLimiter.CurrentRate > 0);

        RateLimiter.SetLimit(100);
        Assert.Equal(0, RateLimiter.CurrentRate);
    }

    // --- RecordAndCheck ---

    [Fact]
    public void RecordAndCheck_Disabled_ReturnsFalse()
    {
        // Limit is 0 (disabled)
        Assert.False(RateLimiter.RecordAndCheck());
    }

    [Fact]
    public void RecordAndCheck_WithinLimit_ReturnsFalse()
    {
        RateLimiter.SetLimit(10);

        Assert.False(RateLimiter.RecordAndCheck());
        Assert.False(RateLimiter.RecordAndCheck());
    }

    [Fact]
    public void RecordAndCheck_ExceedsLimit_ReturnsTrue()
    {
        RateLimiter.SetLimit(3);

        // First 3 are within limit
        RateLimiter.RecordAndCheck();
        RateLimiter.RecordAndCheck();
        RateLimiter.RecordAndCheck();

        // 4th exceeds limit
        Assert.True(RateLimiter.RecordAndCheck());
    }

    [Fact]
    public void RecordAndCheck_AtExactLimit_ReturnsFalse()
    {
        RateLimiter.SetLimit(3);

        Assert.False(RateLimiter.RecordAndCheck());
        Assert.False(RateLimiter.RecordAndCheck());
        Assert.False(RateLimiter.RecordAndCheck());
    }

    // --- CurrentRate ---

    [Fact]
    public void CurrentRate_NoRecords_IsZero()
    {
        RateLimiter.SetLimit(10);
        Assert.Equal(0, RateLimiter.CurrentRate);
    }

    [Fact]
    public void CurrentRate_AfterRecords_ReflectsCount()
    {
        RateLimiter.SetLimit(100);
        RateLimiter.RecordAndCheck();
        RateLimiter.RecordAndCheck();
        RateLimiter.RecordAndCheck();

        Assert.Equal(3, RateLimiter.CurrentRate);
    }

    [Fact]
    public void CurrentRate_WhenDisabled_RecordsNotTracked()
    {
        // When disabled, RecordAndCheck still adds timestamps but returns false
        // Actually, let's check: disabled means _maxPerSecond <= 0, so RecordAndCheck
        // returns false early without enqueuing
        RateLimiter.RecordAndCheck();
        RateLimiter.RecordAndCheck();

        Assert.Equal(0, RateLimiter.CurrentRate);
    }
}
