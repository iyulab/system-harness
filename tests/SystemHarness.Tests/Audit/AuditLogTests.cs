using SystemHarness.Windows;

namespace SystemHarness.Tests.Audit;

[Trait("Category", "CI")]
public class AuditLogTests
{
    [Fact]
    public async Task InMemoryLog_AppendsAndRetrieves()
    {
        var log = new InMemoryAuditLog();
        await log.AppendAsync(new AuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = "Shell",
            Action = "RunAsync",
            Details = "echo hello",
        });

        var entries = await log.GetEntriesAsync();
        Assert.Single(entries);
        Assert.Equal("Shell", entries[0].Category);
    }

    [Fact]
    public async Task InMemoryLog_FiltersByCategory()
    {
        var log = new InMemoryAuditLog();
        await log.AppendAsync(new AuditEntry { Timestamp = DateTimeOffset.UtcNow, Category = "Shell", Action = "Run", Details = "cmd" });
        await log.AppendAsync(new AuditEntry { Timestamp = DateTimeOffset.UtcNow, Category = "Mouse", Action = "Click", Details = "100,200" });
        await log.AppendAsync(new AuditEntry { Timestamp = DateTimeOffset.UtcNow, Category = "Shell", Action = "Run", Details = "dir" });

        var shellEntries = await log.GetEntriesAsync("Shell");
        Assert.Equal(2, shellEntries.Count);

        var mouseEntries = await log.GetEntriesAsync("Mouse");
        Assert.Single(mouseEntries);
    }

    [Fact]
    public async Task InMemoryLog_EvictsOldestWhenFull()
    {
        var log = new InMemoryAuditLog(maxEntries: 3);
        for (int i = 0; i < 5; i++)
        {
            await log.AppendAsync(new AuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Category = "Shell",
                Action = "Run",
                Details = $"cmd-{i}",
            });
        }

        var entries = await log.GetEntriesAsync();
        Assert.Equal(3, entries.Count);
        // Oldest should be evicted
        Assert.Equal("cmd-2", entries[0].Details);
        Assert.Equal("cmd-4", entries[2].Details);
    }

    [Fact]
    public async Task InMemoryLog_ThreadSafe()
    {
        var log = new InMemoryAuditLog(maxEntries: 1000);
        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
        {
            await log.AppendAsync(new AuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Category = "Test",
                Action = "Concurrent",
                Details = $"item-{i}",
            });
        }));

        await Task.WhenAll(tasks);
        var entries = await log.GetEntriesAsync();
        Assert.Equal(100, entries.Count);
    }

    [Fact]
    public async Task AuditingShell_LogsSuccessfulCommand()
    {
        var log = new InMemoryAuditLog();
        var shell = new AuditingShell(new WindowsShell(), log);

        var result = await shell.RunAsync("cmd", "/C echo audited");
        Assert.True(result.Success);

        var entries = await log.GetEntriesAsync();
        Assert.Single(entries);
        Assert.Equal("Shell", entries[0].Category);
        Assert.Equal("RunAsync", entries[0].Action);
        Assert.Contains("echo audited", entries[0].Details!);
        Assert.True(entries[0].Success);
        Assert.NotNull(entries[0].Duration);
    }

    [Fact]
    public async Task AuditingShell_LogsFailedCommand()
    {
        var log = new InMemoryAuditLog();
        var shell = new AuditingShell(new WindowsShell(), log);

        var result = await shell.RunAsync("cmd", "/C exit 1");
        Assert.False(result.Success);

        var entries = await log.GetEntriesAsync();
        Assert.Single(entries);
        Assert.False(entries[0].Success);
    }

    [Fact]
    public async Task AuditingShell_LogsSingleStringCommand()
    {
        var log = new InMemoryAuditLog();
        var shell = new AuditingShell(new WindowsShell(), log);

        await shell.RunAsync("echo single-string");

        var entries = await log.GetEntriesAsync();
        Assert.Single(entries);
        Assert.Equal("echo single-string", entries[0].Details);
    }

    [Fact]
    public void AuditEntry_DefaultSuccessIsTrue()
    {
        var entry = new AuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = "Test",
            Action = "Test",
        };
        Assert.True(entry.Success);
    }

    // --- AuditingShell edge cases (cycle 248) ---

    [Fact]
    public void AuditingShell_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditingShell(null!, new InMemoryAuditLog()));
    }

    [Fact]
    public void AuditingShell_NullLog_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditingShell(new WindowsShell(), null!));
    }

    [Fact]
    public async Task AuditingShell_LogsDuration()
    {
        var log = new InMemoryAuditLog();
        var shell = new AuditingShell(new WindowsShell(), log);

        await shell.RunAsync("cmd", "/C echo test");

        var entries = await log.GetEntriesAsync();
        Assert.Single(entries);
        Assert.NotNull(entries[0].Duration);
        Assert.True(entries[0].Duration!.Value.TotalMilliseconds >= 0);
    }

    [Fact]
    public async Task AuditingShell_FailedCommand_LogsStdErr()
    {
        var log = new InMemoryAuditLog();
        var shell = new AuditingShell(new WindowsShell(), log);

        await shell.RunAsync("cmd", "/C echo error 1>&2 && exit 1");

        var entries = await log.GetEntriesAsync();
        Assert.Single(entries);
        Assert.False(entries[0].Success);
        Assert.NotNull(entries[0].Error);
    }

    [Fact]
    public async Task InMemoryAuditLog_EmptyCategory_ReturnsEmpty()
    {
        var log = new InMemoryAuditLog();
        await log.AppendAsync(new AuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = "Shell",
            Action = "Run",
        });

        var results = await log.GetEntriesAsync("Keyboard");
        Assert.Empty(results);
    }

    [Fact]
    public async Task InMemoryAuditLog_DefaultMaxEntries_Is10000()
    {
        var log = new InMemoryAuditLog(maxEntries: 5);

        for (var i = 0; i < 8; i++)
            await log.AppendAsync(new AuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Category = "Test",
                Action = $"Action_{i}",
            });

        var entries = await log.GetEntriesAsync();
        Assert.True(entries.Count <= 5, $"Expected at most 5, got {entries.Count}");
    }

    [Fact]
    public void InMemoryAuditLog_InvalidMaxEntries_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryAuditLog(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryAuditLog(-1));
    }
}
