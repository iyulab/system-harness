using SystemHarness.Mcp;
using System.Text.Json;

namespace SystemHarness.Tests.Mcp;

[Trait("Category", "CI")]
public class MonitorManagerTests : IDisposable
{
    private readonly MonitorManager _manager = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"harness-test-{Guid.NewGuid():N}");

    public MonitorManagerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _manager.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Start_ReturnsMonitorId()
    {
        var outputPath = Path.Combine(_tempDir, "events.jsonl");
        var id = _manager.Start("test", outputPath, async (path, ct) =>
        {
            while (!ct.IsCancellationRequested)
                await Task.Delay(100, ct);
        });

        Assert.StartsWith("test-", id);
    }

    [Fact]
    public void Stop_ReturnsTrue_ForExistingMonitor()
    {
        var outputPath = Path.Combine(_tempDir, "events.jsonl");
        var id = _manager.Start("test", outputPath, async (path, ct) =>
        {
            while (!ct.IsCancellationRequested)
                await Task.Delay(100, ct);
        });

        Assert.True(_manager.Stop(id));
    }

    [Fact]
    public void Stop_ReturnsFalse_ForUnknownId()
    {
        Assert.False(_manager.Stop("nonexistent-99"));
    }

    [Fact]
    public void ListActive_ReturnsRunningMonitors()
    {
        var outputPath = Path.Combine(_tempDir, "events.jsonl");
        _manager.Start("test", outputPath, async (path, ct) =>
        {
            while (!ct.IsCancellationRequested)
                await Task.Delay(100, ct);
        });

        var active = _manager.ListActive();
        Assert.Single(active);
        Assert.Equal("test", active[0].Type);
    }

    [Fact]
    public async Task WriteEventAsync_CreatesJsonlFile()
    {
        var outputPath = Path.Combine(_tempDir, "write-test.jsonl");

        await MonitorManager.WriteEventAsync(outputPath, new { type = "test", value = 42 });
        await MonitorManager.WriteEventAsync(outputPath, new { type = "test", value = 99 });

        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(2, lines.Length);

        var first = JsonDocument.Parse(lines[0]);
        Assert.Equal("test", first.RootElement.GetProperty("type").GetString());
        Assert.Equal(42, first.RootElement.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task ReadEventsAsync_ReturnsAllEvents()
    {
        var outputPath = Path.Combine(_tempDir, "read-test.jsonl");

        await MonitorManager.WriteEventAsync(outputPath, new { type = "a", timestamp = DateTime.UtcNow.ToString("O") });
        await MonitorManager.WriteEventAsync(outputPath, new { type = "b", timestamp = DateTime.UtcNow.ToString("O") });

        var events = await MonitorManager.ReadEventsAsync(outputPath);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ReadEventsAsync_NonexistentFile_ReturnsEmpty()
    {
        var events = await MonitorManager.ReadEventsAsync(Path.Combine(_tempDir, "nope.jsonl"));
        Assert.Empty(events);
    }

    [Fact]
    public void ListActive_AfterStop_DoesNotIncludeStopped()
    {
        var outputPath = Path.Combine(_tempDir, "events.jsonl");
        var id = _manager.Start("test", outputPath, async (_, ct) =>
        {
            while (!ct.IsCancellationRequested) await Task.Delay(100, ct);
        });

        _manager.Stop(id);
        Assert.Empty(_manager.ListActive());
    }

    [Fact]
    public void Start_MultipleMonitors_AllTracked()
    {
        var id1 = _manager.Start("type_a", Path.Combine(_tempDir, "a.jsonl"), async (_, ct) =>
        {
            while (!ct.IsCancellationRequested) await Task.Delay(100, ct);
        });
        var id2 = _manager.Start("type_b", Path.Combine(_tempDir, "b.jsonl"), async (_, ct) =>
        {
            while (!ct.IsCancellationRequested) await Task.Delay(100, ct);
        });

        var active = _manager.ListActive();
        Assert.Equal(2, active.Count);
        Assert.Contains(active, m => m.Id == id1);
        Assert.Contains(active, m => m.Id == id2);
    }

    [Fact]
    public void Dispose_StopsAllMonitors()
    {
        _manager.Start("a", Path.Combine(_tempDir, "a.jsonl"), async (_, ct) =>
        {
            while (!ct.IsCancellationRequested) await Task.Delay(100, ct);
        });
        _manager.Start("b", Path.Combine(_tempDir, "b.jsonl"), async (_, ct) =>
        {
            while (!ct.IsCancellationRequested) await Task.Delay(100, ct);
        });

        _manager.Dispose();
        Assert.Empty(_manager.ListActive());
    }

    [Fact]
    public void Stop_SameIdTwice_ReturnsFalseSecondTime()
    {
        var id = _manager.Start("test", Path.Combine(_tempDir, "e.jsonl"), async (_, ct) =>
        {
            while (!ct.IsCancellationRequested) await Task.Delay(100, ct);
        });

        Assert.True(_manager.Stop(id));
        Assert.False(_manager.Stop(id));
    }

    [Fact]
    public async Task ReadEventsAsync_WithSinceFilter_ExcludesOldEvents()
    {
        var outputPath = Path.Combine(_tempDir, "filter-test.jsonl");
        // Use explicit UTC strings to avoid timezone parsing issues
        var oldTime = "2020-01-01T00:00:00Z";
        var newTime = "2030-01-01T00:00:00Z";

        await MonitorManager.WriteEventAsync(outputPath, new { type = "old", timestamp = oldTime });
        await MonitorManager.WriteEventAsync(outputPath, new { type = "new", timestamp = newTime });

        // since = 2025 — should exclude 2020 event, include 2030 event
        var events = await MonitorManager.ReadEventsAsync(outputPath, since: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Single(events);
        Assert.Equal("new", events[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task ReadEventsAsync_MalformedLines_Skipped()
    {
        var outputPath = Path.Combine(_tempDir, "malformed.jsonl");
        await File.WriteAllTextAsync(outputPath, "{\"type\":\"good\"}\nnot-json\n{\"type\":\"also_good\"}\n");

        var events = await MonitorManager.ReadEventsAsync(outputPath);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ReadEventsAsync_EmptyLines_Skipped()
    {
        var outputPath = Path.Combine(_tempDir, "empty-lines.jsonl");
        await File.WriteAllTextAsync(outputPath, "{\"a\":1}\n\n\n{\"b\":2}\n");

        var events = await MonitorManager.ReadEventsAsync(outputPath);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void MonitorInfo_HasExpectedProperties()
    {
        var outputPath = Path.Combine(_tempDir, "e.jsonl");
        _manager.Start("file", outputPath, async (_, ct) =>
        {
            while (!ct.IsCancellationRequested) await Task.Delay(100, ct);
        });

        var info = _manager.ListActive()[0];
        Assert.Equal("file", info.Type);
        Assert.Equal(outputPath, info.OutputPath);
        Assert.True(info.IsRunning);
        Assert.True(info.StartedAt <= DateTime.UtcNow);
    }
}
