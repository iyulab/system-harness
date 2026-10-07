using SystemHarness.Mcp;

namespace SystemHarness.Tests.Mcp;

[Collection("StaticState")]
[Trait("Category", "CI")]
public class RateLimiterTests : IDisposable
{
    private readonly RateLimiter _rate = new();

    public RateLimiterTests() => _rate.SetLimit(0);
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _rate.SetLimit(0);
    }

    // --- SetLimit / MaxPerSecond ---

    [Fact]
    public void MaxPerSecond_DefaultIsZero()
    {
        Assert.Equal(0, _rate.MaxPerSecond);
    }

    [Fact]
    public void SetLimit_PositiveValue_UpdatesMaxPerSecond()
    {
        _rate.SetLimit(10);
        Assert.Equal(10, _rate.MaxPerSecond);
    }

    [Fact]
    public void SetLimit_Zero_Disables()
    {
        _rate.SetLimit(10);
        _rate.SetLimit(0);
        Assert.Equal(0, _rate.MaxPerSecond);
    }

    [Fact]
    public void SetLimit_Negative_ClampsToZero()
    {
        _rate.SetLimit(-5);
        Assert.Equal(0, _rate.MaxPerSecond);
    }

    [Fact]
    public void SetLimit_ClearsTimestamps()
    {
        _rate.SetLimit(100);
        _rate.RecordAndCheck();
        _rate.RecordAndCheck();
        Assert.True(_rate.CurrentRate > 0);

        _rate.SetLimit(100);
        Assert.Equal(0, _rate.CurrentRate);
    }

    // --- RecordAndCheck ---

    [Fact]
    public void RecordAndCheck_Disabled_ReturnsFalse()
    {
        // Limit is 0 (disabled)
        Assert.False(_rate.RecordAndCheck());
    }

    [Fact]
    public void RecordAndCheck_WithinLimit_ReturnsFalse()
    {
        _rate.SetLimit(10);

        Assert.False(_rate.RecordAndCheck());
        Assert.False(_rate.RecordAndCheck());
    }

    [Fact]
    public void RecordAndCheck_ExceedsLimit_ReturnsTrue()
    {
        _rate.SetLimit(3);

        // First 3 are within limit
        _rate.RecordAndCheck();
        _rate.RecordAndCheck();
        _rate.RecordAndCheck();

        // 4th exceeds limit
        Assert.True(_rate.RecordAndCheck());
    }

    [Fact]
    public void RecordAndCheck_AtExactLimit_ReturnsFalse()
    {
        _rate.SetLimit(3);

        Assert.False(_rate.RecordAndCheck());
        Assert.False(_rate.RecordAndCheck());
        Assert.False(_rate.RecordAndCheck());
    }

    // --- CurrentRate ---

    [Fact]
    public void CurrentRate_NoRecords_IsZero()
    {
        _rate.SetLimit(10);
        Assert.Equal(0, _rate.CurrentRate);
    }

    [Fact]
    public void CurrentRate_AfterRecords_ReflectsCount()
    {
        _rate.SetLimit(100);
        _rate.RecordAndCheck();
        _rate.RecordAndCheck();
        _rate.RecordAndCheck();

        Assert.Equal(3, _rate.CurrentRate);
    }

    [Fact]
    public void CurrentRate_WhenDisabled_RecordsNotTracked()
    {
        // When disabled, RecordAndCheck still adds timestamps but returns false
        // Actually, let's check: disabled means _maxPerSecond <= 0, so RecordAndCheck
        // returns false early without enqueuing
        _rate.RecordAndCheck();
        _rate.RecordAndCheck();

        Assert.Equal(0, _rate.CurrentRate);
    }
}
