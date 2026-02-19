using System.Globalization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class ProcessTools(IHarness harness)
{
    [McpServerTool(Name = "process_list"), Description("List running processes. Optional filter by name substring.")]
    public async Task<string> ListAsync(
        [Description("Optional process name filter (case-insensitive substring match).")] string? filter = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var processes = await harness.Process.ListAsync(filter, ct);
        return McpResponse.Items(processes.Select(p => new
        {
            p.Pid, p.Name,
            title = string.IsNullOrEmpty(p.MainWindowTitle) ? null : p.MainWindowTitle,
            memoryMB = p.MemoryUsageBytes / (1024 * 1024),
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_get_info"), Description(
        "Get detailed information about a specific process by PID. " +
        "Returns name, executable path, command line, start time, parent PID, " +
        "memory usage, CPU usage percentage, and main window title.")]
    public async Task<string> GetInfoAsync(
        [Description("Process ID to query.")] int pid, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var processes = await harness.Process.ListAsync(null, ct);
        var proc = processes.FirstOrDefault(p => p.Pid == pid);
        if (proc is null)
            return McpResponse.Error("process_not_found",
                $"Process {pid} not found (not running or invalid PID).", sw.ElapsedMilliseconds);

        return McpResponse.Ok(new
        {
            proc.Pid,
            proc.Name,
            proc.ExecutablePath,
            proc.CommandLine,
            proc.MainWindowTitle,
            proc.IsRunning,
            startTime = proc.StartTime?.ToString("O"),
            proc.ParentPid,
            memoryBytes = proc.MemoryUsageBytes,
            memoryMB = proc.MemoryUsageBytes.HasValue ? proc.MemoryUsageBytes.Value / (1024 * 1024) : (long?)null,
            proc.CpuUsagePercent,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_start"), Description(
        "Start a new process by path with optional arguments. " +
        "Note: For UWP/Store apps (e.g. calc.exe), the returned PID may be a launcher proxy — " +
        "use window_close instead of process_stop for these apps.")]
    public async Task<string> StartAsync(
        [Description("Executable path or file to open.")] string path,
        [Description("Optional command-line arguments.")] string? arguments = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var info = await harness.Process.StartAsync(path, arguments, ct);
        ActionLog.Record("process_start", $"path={path}, pid={info.Pid}", sw.ElapsedMilliseconds, true);
        return McpResponse.Ok(new { info.Pid, info.Name }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_start_advanced"), Description(
        "Start a new process with advanced options (working directory, hidden window, elevated, output redirect).")]
    public async Task<string> StartAdvancedAsync(
        [Description("Executable path or file to open.")] string path,
        [Description("Optional command-line arguments.")] string? arguments = null,
        [Description("Working directory for the process.")] string? workingDirectory = null,
        [Description("Start with hidden window.")] bool hidden = false,
        [Description("Start with elevated/admin privileges (triggers UAC on Windows).")] bool elevated = false,
        [Description("Redirect stdout/stderr (hides console window).")] bool redirectOutput = false,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var options = new ProcessStartOptions
        {
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            Hidden = hidden,
            RunElevated = elevated,
            RedirectOutput = redirectOutput,
        };
        var info = await harness.Process.StartAsync(path, options, ct);
        ActionLog.Record("process_start_advanced", $"path={path}, pid={info.Pid}", sw.ElapsedMilliseconds, true);
        return McpResponse.Ok(new { info.Pid, info.Name }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_stop"), Description("Stop (kill) a process by PID.")]
    public async Task<string> KillAsync(
        [Description("Process ID to kill.")] int pid, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        bool existed;
        try
        {
            using var _ = System.Diagnostics.Process.GetProcessById(pid);
            existed = true;
        }
        catch (ArgumentException)
        {
            existed = false;
        }

        if (!existed)
            return McpResponse.Error("process_not_found", $"Process {pid} not found (not running or invalid PID).", sw.ElapsedMilliseconds);

        await harness.Process.KillAsync(pid, ct);
        ActionLog.Record("process_stop", $"pid={pid}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Process {pid} killed.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_stop_by_name"), Description("Kill all processes matching a name (e.g., 'notepad', 'chrome').")]
    public async Task<string> KillByNameAsync(
        [Description("Process name to kill (case-insensitive).")] string name,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(name))
            return McpResponse.Error("invalid_parameter", "name cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Process.KillByNameAsync(name, ct);
        ActionLog.Record("process_stop_by_name", $"name={name}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Killed all processes named '{name}'.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_stop_tree"), Description("Kill a process and all its descendant processes (process tree kill).")]
    public async Task<string> KillTreeAsync(
        [Description("Process ID of the root process to kill.")] int pid,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        bool existed;
        try
        {
            using var _ = System.Diagnostics.Process.GetProcessById(pid);
            existed = true;
        }
        catch (ArgumentException)
        {
            existed = false;
        }

        if (!existed)
            return McpResponse.Error("process_not_found", $"Process {pid} not found (not running or invalid PID).", sw.ElapsedMilliseconds);

        await harness.Process.KillTreeAsync(pid, ct);
        ActionLog.Record("process_stop_tree", $"pid={pid}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Killed process tree rooted at PID {pid}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_check"), Description("Check if a process is running by name.")]
    public async Task<string> IsRunningAsync(
        [Description("Process name to check (e.g., 'notepad', 'chrome').")] string name, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(name))
            return McpResponse.Error("invalid_parameter", "name cannot be empty.", sw.ElapsedMilliseconds);
        var running = await harness.Process.IsRunningAsync(name, ct);
        return McpResponse.Check(running, running ? $"'{name}' is running." : $"'{name}' is not running.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_wait_exit"), Description(
        "Wait for a process to exit by PID. Polls until the process terminates or timeout is reached.")]
    public static async Task<string> WaitExitAsync(
        [Description("Process ID to wait for.")] int pid,
        [Description("Maximum time to wait in milliseconds.")] int timeoutMs = 30000,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);
        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(500);
        var attempts = 0;

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            attempts++;

            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById(pid);
                // Still running — wait
            }
            catch (ArgumentException)
            {
                // Process no longer exists
                return McpResponse.Ok(new { exited = true, pid, attempts }, sw.ElapsedMilliseconds);
            }

            var remaining = deadline - sw.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(remaining < interval ? remaining : interval, ct);
        }

        return McpResponse.Ok(new { exited = false, pid, attempts }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_find_by_port"), Description("Find processes listening on a specific network port.")]
    public async Task<string> FindByPortAsync(
        [Description("Network port number to search for.")] int port,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (port < 0 || port > 65535)
            return McpResponse.Error("invalid_parameter", $"Port must be 0-65535 (got {port}).", sw.ElapsedMilliseconds);
        var processes = await harness.Process.FindByPortAsync(port, ct);
        return McpResponse.Items(processes.Select(p => new
        {
            p.Pid, p.Name,
            title = string.IsNullOrEmpty(p.MainWindowTitle) ? null : p.MainWindowTitle,
            memoryMB = p.MemoryUsageBytes / (1024 * 1024),
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_find_by_path"), Description("Find processes by their executable path (substring match).")]
    public async Task<string> FindByPathAsync(
        [Description("Executable path or substring to search for.")] string executablePath,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(executablePath))
            return McpResponse.Error("invalid_parameter", "executablePath cannot be empty.", sw.ElapsedMilliseconds);
        var processes = await harness.Process.FindByPathAsync(executablePath, ct);
        return McpResponse.Items(processes.Select(p => new
        {
            p.Pid, p.Name, p.ExecutablePath,
            memoryMB = p.MemoryUsageBytes / (1024 * 1024),
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_get_children"), Description("Get all child processes of a given process by PID.")]
    public async Task<string> GetChildrenAsync(
        [Description("Parent process ID.")] int pid,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var children = await harness.Process.GetChildProcessesAsync(pid, ct);
        return McpResponse.Items(children.Select(p => new
        {
            p.Pid, p.Name,
            title = string.IsNullOrEmpty(p.MainWindowTitle) ? null : p.MainWindowTitle,
            memoryMB = p.MemoryUsageBytes / (1024 * 1024),
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_find_by_window"), Description("Find processes that have a window matching the given title substring.")]
    public async Task<string> FindByWindowAsync(
        [Description("Window title substring to search for (case-insensitive).")] string titleSubstring,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleSubstring))
            return McpResponse.Error("invalid_parameter", "titleSubstring cannot be empty.", sw.ElapsedMilliseconds);
        var processes = await harness.Process.FindByWindowTitleAsync(titleSubstring, ct);
        return McpResponse.Items(processes.Select(p => new
        {
            p.Pid, p.Name, p.MainWindowTitle,
            memoryMB = p.MemoryUsageBytes / (1024 * 1024),
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "process_list_by_window"), Description(
        "List processes that have visible windows. Useful for finding running GUI applications.")]
    public async Task<string> ListByWindowAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var windows = await harness.Window.ListAsync(ct);
        var grouped = windows
            .Where(w => !string.IsNullOrWhiteSpace(w.Title))
            .GroupBy(w => w.ProcessId)
            .Select(g => new
            {
                pid = g.Key,
                windows = g.Select(w => new
                {
                    handle = w.Handle.ToString(CultureInfo.InvariantCulture),
                    w.Title,
                }).ToArray(),
            })
            .ToArray();

        return McpResponse.Items(grouped, sw.ElapsedMilliseconds);
    }
}
