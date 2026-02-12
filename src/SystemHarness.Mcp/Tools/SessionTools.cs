using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemHarness.Mcp.Tools;

public sealed class SessionTools(IHarness harness)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    [McpServerTool(Name = "session_save"), Description(
        "Save the current desktop state to a JSON file. " +
        "Captures: windows (title, bounds, state), processes with windows, " +
        "foreground window, mouse position, and clipboard text. " +
        "Use session_compare to detect changes later.")]
    public async Task<string> SaveAsync(
        [Description("File path to save the session state JSON.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var state = await CaptureStateAsync(ct);

        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(state, JsonOpts);
        await File.WriteAllTextAsync(path, json, ct);

        ActionLog.Record("session_save", $"path={path}, windows={state.Windows.Count}", sw.ElapsedMilliseconds, true);
        return McpResponse.Ok(new
        {
            path,
            windowCount = state.Windows.Count,
            processCount = state.Processes.Count,
            foregroundWindow = state.ForegroundTitle,
            hasClipboardText = state.ClipboardText is not null,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "session_compare"), Description(
        "Compare the current desktop state against a previously saved session. " +
        "Returns differences: new/closed windows, moved/resized windows, " +
        "foreground change, process changes, clipboard changes.")]
    public async Task<string> CompareAsync(
        [Description("Path to previously saved session JSON file.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);

        if (!File.Exists(path))
            return McpResponse.Error("file_not_found", $"Session file not found: '{path}'", sw.ElapsedMilliseconds);

        var savedJson = await File.ReadAllTextAsync(path, ct);
        var saved = JsonSerializer.Deserialize<SessionState>(savedJson, JsonOpts);
        if (saved is null)
            return McpResponse.Error("invalid_parameter", "Failed to deserialize session file.", sw.ElapsedMilliseconds);

        var current = await CaptureStateAsync(ct);

        // Compare windows
        var savedHandles = saved.Windows.ToDictionary(w => w.Handle);
        var currentHandles = current.Windows.ToDictionary(w => w.Handle);

        var newWindows = current.Windows.Where(w => !savedHandles.ContainsKey(w.Handle)).ToList();
        var closedWindows = saved.Windows.Where(w => !currentHandles.ContainsKey(w.Handle)).ToList();
        var movedWindows = new List<object>();

        foreach (var cur in current.Windows)
        {
            if (savedHandles.TryGetValue(cur.Handle, out var prev))
            {
                if (prev.Title != cur.Title || prev.X != cur.X || prev.Y != cur.Y ||
                    prev.Width != cur.Width || prev.Height != cur.Height || prev.State != cur.State)
                {
                    movedWindows.Add(new
                    {
                        handle = cur.Handle,
                        before = new { prev.Title, prev.X, prev.Y, prev.Width, prev.Height, prev.State },
                        after = new { cur.Title, cur.X, cur.Y, cur.Width, cur.Height, cur.State },
                    });
                }
            }
        }

        return McpResponse.Ok(new
        {
            newWindows = newWindows.Select(w => new { w.Handle, w.Title }).ToArray(),
            closedWindows = closedWindows.Select(w => new { w.Handle, w.Title }).ToArray(),
            changedWindows = movedWindows.ToArray(),
            foregroundChanged = saved.ForegroundTitle != current.ForegroundTitle,
            savedForeground = saved.ForegroundTitle,
            currentForeground = current.ForegroundTitle,
            clipboardChanged = saved.ClipboardText != current.ClipboardText,
            mouseChanged = saved.MouseX != current.MouseX || saved.MouseY != current.MouseY,
        }, sw.ElapsedMilliseconds);
    }

    private async Task<SessionState> CaptureStateAsync(CancellationToken ct)
    {
        var windowsTask = harness.Window.ListAsync(ct);
        var foregroundTask = harness.Window.GetForegroundAsync(ct);
        var mouseTask = harness.Mouse.GetPositionAsync(ct);

        string? clipboardText = null;
        try { clipboardText = await harness.Clipboard.GetTextAsync(ct); }
        catch { /* clipboard may be locked */ }

        await Task.WhenAll(windowsTask, foregroundTask, mouseTask);

        var windows = await windowsTask;
        var foreground = await foregroundTask;
        var (mx, my) = await mouseTask;

        // Get processes that have visible windows
        var processIds = windows.Where(w => !string.IsNullOrWhiteSpace(w.Title))
            .Select(w => w.ProcessId).Distinct().ToHashSet();

        return new SessionState
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            Windows = windows.Where(w => !string.IsNullOrWhiteSpace(w.Title))
                .Select(w => new SessionWindow
                {
                    Handle = w.Handle.ToString(),
                    Title = w.Title,
                    ProcessId = w.ProcessId,
                    X = w.Bounds.X, Y = w.Bounds.Y,
                    Width = w.Bounds.Width, Height = w.Bounds.Height,
                    State = w.State.ToString(),
                }).ToList(),
            Processes = processIds.Select(pid => new SessionProcess { Pid = pid }).ToList(),
            ForegroundTitle = foreground?.Title,
            MouseX = mx, MouseY = my,
            ClipboardText = clipboardText,
        };
    }

    [McpServerTool(Name = "session_bookmark"), Description(
        "Save a named bookmark of a window's visual state (screenshot + hash). " +
        "Use session_bookmark_compare to later check if the window has changed. " +
        "Useful for marking a known-good state before performing actions.")]
    public async Task<string> BookmarkAsync(
        [Description("Bookmark name for later reference.")] string name,
        [Description("Optional window to capture (title substring or handle). Omit for full screen.")] string? titleOrHandle = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(name))
            return McpResponse.Error("invalid_parameter", "name cannot be empty.", sw.ElapsedMilliseconds);

        using var screenshot = titleOrHandle is not null
            ? await harness.Screen.CaptureWindowAsync(titleOrHandle, ct)
            : await harness.Screen.CaptureAsync(ct: ct);

        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(screenshot.Bytes));

        var snapshotPath = Path.Combine(
            Path.GetTempPath(), $"harness-bookmark-{name}-{DateTime.Now:HHmmss}.png");
        await screenshot.SaveAsync(snapshotPath, ct);

        _bookmarks[name] = new BookmarkEntry(hash, snapshotPath, screenshot.Width, screenshot.Height, DateTime.UtcNow);

        ActionLog.Record("session_bookmark", $"name={name}", sw.ElapsedMilliseconds, true);
        return McpResponse.Ok(new
        {
            name,
            hash,
            snapshotPath,
            width = screenshot.Width,
            height = screenshot.Height,
            bookmarkCount = _bookmarks.Count,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "session_bookmark_compare"), Description(
        "Compare the current screen state against a saved bookmark. " +
        "Returns whether the visual state has changed since the bookmark was taken.")]
    public async Task<string> BookmarkCompareAsync(
        [Description("Bookmark name to compare against.")] string name,
        [Description("Optional window to capture (title substring or handle). Omit for full screen.")] string? titleOrHandle = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(name))
            return McpResponse.Error("invalid_parameter", "name cannot be empty.", sw.ElapsedMilliseconds);

        if (!_bookmarks.TryGetValue(name, out var bookmark))
            return McpResponse.Error("bookmark_not_found", $"Bookmark '{name}' not found.", sw.ElapsedMilliseconds);

        using var screenshot = titleOrHandle is not null
            ? await harness.Screen.CaptureWindowAsync(titleOrHandle, ct)
            : await harness.Screen.CaptureAsync(ct: ct);

        var currentHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(screenshot.Bytes));

        var changed = currentHash != bookmark.Hash;

        return McpResponse.Ok(new
        {
            name,
            changed,
            bookmarkHash = bookmark.Hash,
            currentHash,
            bookmarkTime = bookmark.Timestamp.ToString("O"),
            bookmarkPath = bookmark.SnapshotPath,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "session_bookmark_list"), Description(
        "List all saved bookmarks with their names, hashes, and timestamps.")]
    public Task<string> BookmarkListAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        return Task.FromResult(McpResponse.Items(_bookmarks.Select(kv => new
        {
            name = kv.Key,
            hash = kv.Value.Hash,
            snapshotPath = kv.Value.SnapshotPath,
            width = kv.Value.Width,
            height = kv.Value.Height,
            timestamp = kv.Value.Timestamp.ToString("O"),
        }).ToArray(), sw.ElapsedMilliseconds));
    }

    private readonly Dictionary<string, BookmarkEntry> _bookmarks = new();

    private sealed record BookmarkEntry(
        string Hash, string SnapshotPath, int Width, int Height, DateTime Timestamp);

    // ── Serialization Models ──

    private sealed class SessionState
    {
        public string Timestamp { get; set; } = "";
        public List<SessionWindow> Windows { get; set; } = [];
        public List<SessionProcess> Processes { get; set; } = [];
        public string? ForegroundTitle { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public string? ClipboardText { get; set; }
    }

    private sealed class SessionWindow
    {
        public string Handle { get; set; } = "";
        public string Title { get; set; } = "";
        public int ProcessId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string State { get; set; } = "";
    }

    private sealed class SessionProcess
    {
        public int Pid { get; set; }
    }
}
