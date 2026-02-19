using System.Globalization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace SystemHarness.Mcp.Tools;

public sealed class MonitorTools(IHarness harness, MonitorManager monitors)
{
    [McpServerTool(Name = "monitor_start"), Description(
        "Start a background monitor that records events to a JSONL file. " +
        "Types: 'file' (directory changes), 'process' (start/stop), 'window' (focus/title changes), " +
        "'clipboard' (content changes), 'screen' (visual changes with snapshots), " +
        "'dialog' (dialog/popup appearances and closings). " +
        "For 'screen' type, use target='<title>' to monitor a specific window, or omit for full screen. " +
        "Returns a monitor ID for later stop/read.")]
    public Task<string> StartAsync(
        [Description("Monitor type: 'file', 'process', 'window', 'clipboard', 'screen', or 'dialog'.")] string type,
        [Description("Path for the JSONL output file.")] string outputPath,
        [Description("Optional target: directory for 'file', window title for 'screen'. Omit for defaults.")] string? target = null,
        [Description("Polling interval in milliseconds.")] int intervalMs = 2000,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(type))
            return Task.FromResult(McpResponse.Error("invalid_parameter", "type cannot be empty.", sw.ElapsedMilliseconds));
        if (string.IsNullOrWhiteSpace(outputPath))
            return Task.FromResult(McpResponse.Error("invalid_parameter", "outputPath cannot be empty.", sw.ElapsedMilliseconds));

        Func<string, CancellationToken, Task>? monitorFunc = type.ToLowerInvariant() switch
        {
            "file" => (path, token) => FileMonitorLoop(path, target ?? ".", token),
            "process" => (path, token) => ProcessMonitorLoop(path, intervalMs, token),
            "window" => (path, token) => WindowMonitorLoop(path, intervalMs, token),
            "clipboard" => (path, token) => ClipboardMonitorLoop(path, intervalMs, token),
            "screen" => (path, token) => ScreenMonitorLoop(path, target, intervalMs, token),
            "dialog" => (path, token) => DialogMonitorLoop(path, intervalMs, token),
            _ => null,
        };

        if (monitorFunc is null)
            return Task.FromResult(McpResponse.Error("invalid_parameter",
                $"Unknown monitor type: '{type}'. Supported: file, process, window, clipboard, screen, dialog",
                sw.ElapsedMilliseconds));

        var id = monitors.Start(type, outputPath, monitorFunc);

        ActionLog.Record("monitor_start", $"type={type}, output={outputPath}", sw.ElapsedMilliseconds, true);
        return Task.FromResult(McpResponse.Ok(new
        {
            monitorId = id,
            type,
            outputPath,
        }, sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "monitor_stop"), Description(
        "Stop a running background monitor by its ID.")]
    public Task<string> StopAsync(
        [Description("Monitor ID returned by monitor_start.")] string monitorId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(monitorId))
            return Task.FromResult(McpResponse.Error("invalid_parameter", "monitorId cannot be empty.", sw.ElapsedMilliseconds));
        var stopped = monitors.Stop(monitorId);

        ActionLog.Record("monitor_stop", $"id={monitorId}", sw.ElapsedMilliseconds, stopped);
        return Task.FromResult(stopped
            ? McpResponse.Confirm($"Monitor '{monitorId}' stopped.", sw.ElapsedMilliseconds)
            : McpResponse.Error("monitor_not_found", $"Monitor '{monitorId}' not found.", sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "monitor_list"), Description(
        "List all active background monitors.")]
    public Task<string> ListAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var active = monitors.ListActive();
        return Task.FromResult(McpResponse.Items(active.Select(m => new
        {
            m.Id, m.Type, m.OutputPath,
            startedAt = m.StartedAt.ToString("O"),
            m.IsRunning,
        }).ToArray(), sw.ElapsedMilliseconds));
    }

    [McpServerTool(Name = "monitor_read"), Description(
        "Read events from a monitor's JSONL output file. " +
        "Optionally filter events since a timestamp (ISO 8601).")]
    public static async Task<string> ReadAsync(
        [Description("Path to the JSONL output file to read.")] string outputPath,
        [Description("Optional ISO 8601 timestamp to filter events after (e.g., '2024-01-15T10:30:00Z').")] string? since = null,
        [Description("Maximum number of events to return (from the end).")] int limit = 100,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(outputPath))
            return McpResponse.Error("invalid_parameter", "outputPath cannot be empty.", sw.ElapsedMilliseconds);
        DateTime? sinceDate = since is not null ? DateTime.Parse(since, CultureInfo.InvariantCulture) : null;

        var events = await MonitorManager.ReadEventsAsync(outputPath, sinceDate, ct);
        var limited = events.TakeLast(limit).ToArray();

        return McpResponse.Ok(new
        {
            totalEvents = events.Count,
            returnedEvents = limited.Length,
            events = limited,
        }, sw.ElapsedMilliseconds);
    }

    // ── Monitor Implementations ──

    private async Task FileMonitorLoop(string outputPath, string watchDir, CancellationToken ct)
    {
        var fullDir = Path.GetFullPath(watchDir);
        if (!Directory.Exists(fullDir))
            throw new DirectoryNotFoundException($"Directory not found: '{fullDir}'");

        using var watcher = new FileSystemWatcher(fullDir)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true,
        };

        var eventQueue = new System.Collections.Concurrent.ConcurrentQueue<object>();

        void OnChange(object s, FileSystemEventArgs e) =>
            eventQueue.Enqueue(new
            {
                type = "file_" + e.ChangeType.ToString().ToLowerInvariant(),
                path = e.FullPath,
                name = e.Name,
                timestamp = DateTime.UtcNow.ToString("O"),
            });

        void OnRenamed(object s, RenamedEventArgs e) =>
            eventQueue.Enqueue(new
            {
                type = "file_renamed",
                path = e.FullPath,
                name = e.Name,
                oldPath = e.OldFullPath,
                oldName = e.OldName,
                timestamp = DateTime.UtcNow.ToString("O"),
            });

        watcher.Created += OnChange;
        watcher.Changed += OnChange;
        watcher.Deleted += OnChange;
        watcher.Renamed += OnRenamed;

        // Write start event
        await MonitorManager.WriteEventAsync(outputPath, new
        {
            type = "monitor_started",
            monitorType = "file",
            watchDir = fullDir,
            timestamp = DateTime.UtcNow.ToString("O"),
        }, ct);

        // Flush event queue periodically
        while (!ct.IsCancellationRequested)
        {
            while (eventQueue.TryDequeue(out var evt))
                await MonitorManager.WriteEventAsync(outputPath, evt, ct);

            await Task.Delay(500, ct);
        }
    }

    private async Task ProcessMonitorLoop(string outputPath, int intervalMs, CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 500));
        var knownPids = new HashSet<int>();

        // Snapshot current processes
        var initial = await harness.Process.ListAsync(null, ct);
        foreach (var p in initial)
            knownPids.Add(p.Pid);

        await MonitorManager.WriteEventAsync(outputPath, new
        {
            type = "monitor_started",
            monitorType = "process",
            initialProcessCount = knownPids.Count,
            timestamp = DateTime.UtcNow.ToString("O"),
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);

            var current = await harness.Process.ListAsync(null, ct);
            var currentPids = current.Select(p => p.Pid).ToHashSet();

            // Detect new processes
            foreach (var proc in current.Where(p => !knownPids.Contains(p.Pid)))
            {
                await MonitorManager.WriteEventAsync(outputPath, new
                {
                    type = "process_started",
                    pid = proc.Pid,
                    name = proc.Name,
                    timestamp = DateTime.UtcNow.ToString("O"),
                }, ct);
            }

            // Detect exited processes
            foreach (var pid in knownPids.Where(p => !currentPids.Contains(p)))
            {
                await MonitorManager.WriteEventAsync(outputPath, new
                {
                    type = "process_exited",
                    pid,
                    timestamp = DateTime.UtcNow.ToString("O"),
                }, ct);
            }

            knownPids = currentPids;
        }
    }

    private async Task WindowMonitorLoop(string outputPath, int intervalMs, CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 500));

        // Snapshot current windows
        var knownWindows = new Dictionary<nint, (string Title, bool IsVisible)>();
        var initial = await harness.Window.ListAsync(ct);
        foreach (var w in initial.Where(w => !string.IsNullOrWhiteSpace(w.Title)))
            knownWindows[w.Handle] = (w.Title, w.IsVisible);

        nint? lastForeground = null;
        try
        {
            var fg = await harness.Window.GetForegroundAsync(ct);
            lastForeground = fg?.Handle;
        }
        catch { /* GetForegroundAsync may not be available */ }

        await MonitorManager.WriteEventAsync(outputPath, new
        {
            type = "monitor_started",
            monitorType = "window",
            initialWindowCount = knownWindows.Count,
            timestamp = DateTime.UtcNow.ToString("O"),
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);

            var current = await harness.Window.ListAsync(ct);
            var currentMap = new Dictionary<nint, WindowInfo>();
            foreach (var w in current.Where(w => !string.IsNullOrWhiteSpace(w.Title)))
                currentMap[w.Handle] = w;

            // Detect new windows
            foreach (var (handle, win) in currentMap)
            {
                if (!knownWindows.ContainsKey(handle))
                {
                    await MonitorManager.WriteEventAsync(outputPath, new
                    {
                        type = "window_created",
                        handle = handle.ToString(CultureInfo.InvariantCulture),
                        title = win.Title,
                        processId = win.ProcessId,
                        timestamp = DateTime.UtcNow.ToString("O"),
                    }, ct);
                }
            }

            // Detect closed windows
            foreach (var (handle, info) in knownWindows)
            {
                if (!currentMap.ContainsKey(handle))
                {
                    await MonitorManager.WriteEventAsync(outputPath, new
                    {
                        type = "window_closed",
                        handle = handle.ToString(CultureInfo.InvariantCulture),
                        title = info.Title,
                        timestamp = DateTime.UtcNow.ToString("O"),
                    }, ct);
                }
            }

            // Detect title changes
            foreach (var (handle, win) in currentMap)
            {
                if (knownWindows.TryGetValue(handle, out var prev) && prev.Title != win.Title)
                {
                    await MonitorManager.WriteEventAsync(outputPath, new
                    {
                        type = "window_title_changed",
                        handle = handle.ToString(CultureInfo.InvariantCulture),
                        oldTitle = prev.Title,
                        newTitle = win.Title,
                        timestamp = DateTime.UtcNow.ToString("O"),
                    }, ct);
                }
            }

            // Detect foreground change
            try
            {
                var fg = await harness.Window.GetForegroundAsync(ct);
                if (fg is not null && fg.Handle != lastForeground)
                {
                    await MonitorManager.WriteEventAsync(outputPath, new
                    {
                        type = "window_focused",
                        handle = fg.Handle.ToString(CultureInfo.InvariantCulture),
                        title = fg.Title,
                        timestamp = DateTime.UtcNow.ToString("O"),
                    }, ct);
                    lastForeground = fg.Handle;
                }
            }
            catch { /* best effort */ }

            // Update known state
            knownWindows.Clear();
            foreach (var (handle, win) in currentMap)
                knownWindows[handle] = (win.Title, win.IsVisible);
        }
    }

    private async Task ClipboardMonitorLoop(string outputPath, int intervalMs, CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 1000));
        string? lastTextHash = null;

        // Get initial clipboard state
        try
        {
            var text = await harness.Clipboard.GetTextAsync(ct);
            if (text is not null)
                lastTextHash = Convert.ToHexString(SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(text)));
        }
        catch { /* clipboard may be locked */ }

        await MonitorManager.WriteEventAsync(outputPath, new
        {
            type = "monitor_started",
            monitorType = "clipboard",
            timestamp = DateTime.UtcNow.ToString("O"),
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);

            try
            {
                var text = await harness.Clipboard.GetTextAsync(ct);
                string? currentHash = null;
                if (text is not null)
                    currentHash = Convert.ToHexString(SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(text)));

                if (currentHash != lastTextHash)
                {
                    var preview = text is not null && text.Length > 200
                        ? text[..200] + "..."
                        : text;

                    await MonitorManager.WriteEventAsync(outputPath, new
                    {
                        type = "clipboard_changed",
                        format = "text",
                        preview,
                        length = text?.Length,
                        timestamp = DateTime.UtcNow.ToString("O"),
                    }, ct);
                    lastTextHash = currentHash;
                }
            }
            catch { /* clipboard access may fail temporarily */ }
        }
    }

    private async Task DialogMonitorLoop(string outputPath, int intervalMs, CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 500));
        var knownDialogs = new HashSet<nint>();

        await MonitorManager.WriteEventAsync(outputPath, new
        {
            type = "monitor_started",
            monitorType = "dialog",
            timestamp = DateTime.UtcNow.ToString("O"),
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);

            try
            {
                var windows = await harness.Window.ListAsync(ct);

                // Detect dialog-like windows: has parent handle, or class name is #32770
                var currentDialogs = new Dictionary<nint, WindowInfo>();
                foreach (var w in windows.Where(w => !string.IsNullOrWhiteSpace(w.Title) && w.IsVisible))
                {
                    if (w.ClassName == "#32770" || (w.ParentHandle.HasValue && w.ParentHandle.Value != 0))
                        currentDialogs[w.Handle] = w;
                }

                // Detect new dialogs
                foreach (var (handle, win) in currentDialogs)
                {
                    if (knownDialogs.Add(handle))
                    {
                        await MonitorManager.WriteEventAsync(outputPath, new
                        {
                            type = "dialog_appeared",
                            handle = handle.ToString(CultureInfo.InvariantCulture),
                            title = win.Title,
                            className = win.ClassName,
                            processId = win.ProcessId,
                            parentHandle = win.ParentHandle?.ToString(CultureInfo.InvariantCulture),
                            bounds = new { win.Bounds.X, win.Bounds.Y, win.Bounds.Width, win.Bounds.Height },
                            timestamp = DateTime.UtcNow.ToString("O"),
                        }, ct);
                    }
                }

                // Detect closed dialogs
                var currentHandles = currentDialogs.Keys.ToHashSet();
                foreach (var handle in knownDialogs.Where(h => !currentHandles.Contains(h)).ToList())
                {
                    knownDialogs.Remove(handle);
                    await MonitorManager.WriteEventAsync(outputPath, new
                    {
                        type = "dialog_closed",
                        handle = handle.ToString(CultureInfo.InvariantCulture),
                        timestamp = DateTime.UtcNow.ToString("O"),
                    }, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                /* window enumeration may fail temporarily */
            }
        }
    }

    private async Task ScreenMonitorLoop(string outputPath, string? target, int intervalMs, CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 1000));

        // Create snapshot directory next to the JSONL file
        var snapshotDir = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(outputPath) + "-snapshots");
        Directory.CreateDirectory(snapshotDir);

        string? lastHash = null;

        await MonitorManager.WriteEventAsync(outputPath, new
        {
            type = "monitor_started",
            monitorType = "screen",
            target = target ?? "full_screen",
            snapshotDir,
            timestamp = DateTime.UtcNow.ToString("O"),
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);

            try
            {
                using var screenshot = target is not null
                    ? await harness.Screen.CaptureWindowAsync(target, ct)
                    : await harness.Screen.CaptureAsync(ct: ct);

                var currentHash = Convert.ToHexString(SHA256.HashData(screenshot.Bytes));

                if (currentHash != lastHash)
                {
                    if (lastHash is not null) // Skip initial capture (not a "change")
                    {
                        var snapshotName = $"snap-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png";
                        var snapshotPath = Path.Combine(snapshotDir, snapshotName);
                        await screenshot.SaveAsync(snapshotPath, ct);

                        await MonitorManager.WriteEventAsync(outputPath, new
                        {
                            type = "screen_changed",
                            target = target ?? "full_screen",
                            snapshotPath,
                            hash = currentHash,
                            width = screenshot.Width,
                            height = screenshot.Height,
                            timestamp = DateTime.UtcNow.ToString("O"),
                        }, ct);
                    }

                    lastHash = currentHash;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                /* capture may fail temporarily (window not found, etc.) */
            }
        }
    }
}
