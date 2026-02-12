namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class AuditEntryModelTests
{
    [Fact]
    public void AuditEntry_RequiredProperties()
    {
        var ts = DateTimeOffset.UtcNow;
        var entry = new AuditEntry
        {
            Timestamp = ts,
            Category = "Shell",
            Action = "RunAsync",
        };

        Assert.Equal(ts, entry.Timestamp);
        Assert.Equal("Shell", entry.Category);
        Assert.Equal("RunAsync", entry.Action);
    }

    [Fact]
    public void AuditEntry_OptionalDefaults()
    {
        var entry = new AuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = "Test",
            Action = "Test",
        };

        Assert.Null(entry.Details);
        Assert.Null(entry.Duration);
        Assert.True(entry.Success); // Default is true
        Assert.Null(entry.Error);
    }

    [Fact]
    public void AuditEntry_FullyPopulated()
    {
        var ts = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromMilliseconds(250);

        var entry = new AuditEntry
        {
            Timestamp = ts,
            Category = "Mouse",
            Action = "ClickAsync",
            Details = "x=100, y=200",
            Duration = duration,
            Success = false,
            Error = "Element not found",
        };

        Assert.Equal("Mouse", entry.Category);
        Assert.Equal("ClickAsync", entry.Action);
        Assert.Equal("x=100, y=200", entry.Details);
        Assert.Equal(duration, entry.Duration);
        Assert.False(entry.Success);
        Assert.Equal("Element not found", entry.Error);
    }

    [Fact]
    public void AuditEntry_IsRecord()
    {
        // Records support value equality
        var ts = DateTimeOffset.UtcNow;
        var a = new AuditEntry { Timestamp = ts, Category = "A", Action = "B" };
        var b = new AuditEntry { Timestamp = ts, Category = "A", Action = "B" };
        Assert.Equal(a, b);
    }

    [Fact]
    public void AuditEntry_RecordInequality()
    {
        var ts = DateTimeOffset.UtcNow;
        var a = new AuditEntry { Timestamp = ts, Category = "A", Action = "B" };
        var b = new AuditEntry { Timestamp = ts, Category = "X", Action = "B" };
        Assert.NotEqual(a, b);
    }
}
