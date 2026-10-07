using System.Text.Json;
using SystemHarness.Mcp;
using SystemHarness.Mcp.Dispatch;
using SystemHarness.Mcp.Tools;

namespace SystemHarness.Tests.Mcp;

/// <summary>
/// Drives <see cref="DispatchTools"/> end to end: a refused command must never reach its handler,
/// an allowed one must. Each check is exercised in both directions.
/// </summary>
[Trait("Category", "CI")]
public class SafetyGateTests : IDisposable
{
    private static readonly WindowInfo Notepad = Window(1, "Notepad", new Rectangle(100, 100, 400, 300));
    private static readonly WindowInfo Browser = Window(2, "Browser", new Rectangle(600, 100, 400, 300));

    private readonly EmergencyStop _stop = new();
    private readonly RateLimiter _rate = new();
    private readonly SafeZone _zone = new();
    private readonly FakeWindows _windows = new([Notepad, Browser]) { Foreground = Notepad };
    private readonly CommandRegistry _registry = new();
    private readonly Dictionary<string, int> _calls = [];
    private readonly DispatchTools _dispatch;

    public SafetyGateTests()
    {
        _dispatch = new DispatchTools(_registry, new SafetyGate(_stop, _rate, _zone, _windows), _stop);
        Register("mouse.click", "mouse");
        Register("keyboard.type", "keyboard");
        Register("window.close", "window");
        Register("vision.click_text", "vision");
        Register("file.write", "file");
        Register("safety.resume", "safety");
        Register("window.list", "window", mutation: false);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _stop.Dispose();
    }

    // --- Emergency stop ---

    [Fact]
    public async Task EmergencyStop_RefusesMutation_HandlerNotRun()
    {
        _stop.Trigger();

        var result = await Do("file.write");

        AssertRefused(result, "emergency_stopped");
        Assert.Equal(0, Calls("file.write"));
    }

    [Fact]
    public async Task EmergencyStop_Reset_RunsAgain()
    {
        _stop.Trigger(EmergencyStopSource.Agent);
        await Do("file.write");
        _stop.Reset();

        var result = await Do("file.write");

        AssertOk(result);
        Assert.Equal(1, Calls("file.write"));
    }

    [Fact]
    public async Task EmergencyStop_SafetyCommandsAndReadsStillRun()
    {
        _stop.Trigger();

        AssertOk(await Do("safety.resume"));
        AssertOk(await _dispatch.GetAsync("window.list", null, TestContext.Current.CancellationToken));
        Assert.Equal(1, Calls("safety.resume"));
        Assert.Equal(1, Calls("window.list"));
    }

    [Fact]
    public async Task EmergencyStop_CancelsTheRunningCommand()
    {
        var started = new TaskCompletionSource();
        Register("process.wait", "process", handler: async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return McpResponse.Ok(new { });
        });

        var running = Do("process.wait");
        await started.Task;
        _stop.Trigger();

        AssertRefused(await running, "emergency_stopped");
    }

    // --- Rate limit ---

    [Fact]
    public async Task RateLimit_SecondActionInTheSameSecond_Refused()
    {
        _rate.SetLimit(1);

        AssertOk(await Do("file.write"));
        AssertRefused(await Do("file.write"), "rate_limited");
        Assert.Equal(1, Calls("file.write"));
    }

    [Fact]
    public async Task RateLimit_Disabled_EveryActionRuns()
    {
        for (var i = 0; i < 5; i++)
            AssertOk(await Do("file.write"));

        Assert.Equal(5, Calls("file.write"));
    }

    [Fact]
    public void RateLimit_OperatorCeiling_CannotBeRaisedOrDisabled()
    {
        _rate.SetOperatorLimit(2);

        Assert.Throws<HarnessException>(() => _rate.SetLimit(0));
        Assert.Throws<HarnessException>(() => _rate.SetLimit(5));
        _rate.SetLimit(1);
        Assert.Equal(1, _rate.MaxPerSecond);
    }

    // --- Safe zone ---

    [Fact]
    public async Task Zone_PointInside_Runs()
    {
        _zone.Set("Notepad");

        AssertOk(await Do("mouse.click", """{"x":150,"y":150}"""));
        Assert.Equal(1, Calls("mouse.click"));
    }

    [Fact]
    public async Task Zone_PointOutside_Refused()
    {
        _zone.Set("Notepad");

        AssertRefused(await Do("mouse.click", """{"x":700,"y":150}"""), "outside_safe_zone");
        Assert.Equal(0, Calls("mouse.click"));
    }

    [Fact]
    public async Task Zone_RegionIsRelativeToTheWindow()
    {
        _zone.Set("Notepad", new Rectangle(0, 0, 50, 50));

        AssertOk(await Do("mouse.click", """{"x":120,"y":120}"""));
        AssertRefused(await Do("mouse.click", """{"x":300,"y":300}"""), "outside_safe_zone");
    }

    [Fact]
    public async Task Zone_OtherWindowParameter_Refused()
    {
        _zone.Set("Notepad");

        AssertRefused(await Do("window.close", """{"titleOrHandle":"Browser"}"""), "outside_safe_zone");
        AssertOk(await Do("window.close", """{"titleOrHandle":"Notepad"}"""));
        Assert.Equal(1, Calls("window.close"));
    }

    [Fact]
    public async Task Zone_KeyboardNeedsTheZoneInFront()
    {
        _zone.Set("Notepad");

        AssertOk(await Do("keyboard.type", """{"text":"a"}"""));
        _windows.Foreground = Browser;
        AssertRefused(await Do("keyboard.type", """{"text":"a"}"""), "outside_safe_zone");
        Assert.Equal(1, Calls("keyboard.type"));
    }

    [Fact]
    public async Task Zone_WindowNotFound_FailsClosed()
    {
        _zone.Set("Calculator");

        AssertRefused(await Do("mouse.click", """{"x":150,"y":150}"""), "outside_safe_zone");
        Assert.Equal(0, Calls("mouse.click"));
    }

    [Fact]
    public async Task Zone_TargetUnknownBeforeRunning_Refused()
    {
        _zone.Set("Notepad");

        AssertRefused(await Do("vision.click_text", """{"text":"OK"}"""), "outside_safe_zone");
        Assert.Equal(0, Calls("vision.click_text"));
    }

    [Fact]
    public async Task Zone_DoesNotGovernNonInputCommands()
    {
        _zone.Set("Calculator");

        AssertOk(await Do("file.write", """{"path":"a.txt","content":"x"}"""));
    }

    [Fact]
    public void Zone_SetByOperator_CannotBeChangedOrCleared()
    {
        _zone.SetByOperator("Notepad");

        Assert.Throws<HarnessException>(() => _zone.Set("Browser"));
        Assert.Throws<HarnessException>(() => _zone.Clear());
        Assert.Equal("Notepad", _zone.Current!.Window);
    }

    // --- Command policy ---

    [Fact]
    public async Task PolicyViolation_IsARefusal_NotAnException()
    {
        Register("process.start", "process", handler: (_, _) =>
            throw new CommandPolicyException("Program 'shutdown' is blocked by command policy."));

        AssertRefused(await Do("process.start", """{"path":"shutdown"}"""), "policy_blocked");
    }

    // --- Helpers ---

    private Task<string> Do(string command, string? json = null)
        => _dispatch.DoAsync(command, json, TestContext.Current.CancellationToken);

    private int Calls(string name) => _calls.GetValueOrDefault(name);

    private void Register(string name, string category, bool mutation = true,
        Func<JsonElement?, CancellationToken, Task<string>>? handler = null)
    {
        _registry.Register(new CommandDescriptor
        {
            Name = name,
            Category = category,
            Description = name,
            IsMutation = mutation,
            Parameters = [],
            Handler = (args, ct) =>
            {
                _calls[name] = Calls(name) + 1;
                return handler is null ? Task.FromResult(McpResponse.Ok(new { ran = name })) : handler(args, ct);
            },
        });
    }

    private static void AssertOk(string response)
        => Assert.True(JsonDocument.Parse(response).RootElement.GetProperty("ok").GetBoolean(), response);

    private static void AssertRefused(string response, string code)
    {
        var root = JsonDocument.Parse(response).RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean(), response);
        Assert.Contains(code, response, StringComparison.Ordinal);
    }

    private static WindowInfo Window(nint handle, string title, Rectangle bounds) => new()
    {
        Handle = handle,
        Title = title,
        Bounds = bounds,
        IsVisible = true,
    };

    private sealed class FakeWindows(IReadOnlyList<WindowInfo> windows) : IWindow
    {
        public WindowInfo? Foreground { get; set; }

        public Task<IReadOnlyList<WindowInfo>> ListAsync(CancellationToken ct = default) => Task.FromResult(windows);

        public Task<WindowInfo?> GetForegroundAsync(CancellationToken ct = default) => Task.FromResult(Foreground);

        public Task FocusAsync(string titleOrHandle, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MinimizeAsync(string titleOrHandle, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MaximizeAsync(string titleOrHandle, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ResizeAsync(string titleOrHandle, int width, int height, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MoveAsync(string titleOrHandle, int x, int y, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CloseAsync(string titleOrHandle, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
