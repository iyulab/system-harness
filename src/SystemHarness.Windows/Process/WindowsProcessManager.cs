using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IProcessManager"/> using System.Diagnostics.Process.
/// </summary>
public sealed class WindowsProcessManager : IProcessManager
{
    public Task<ProcessInfo> StartAsync(string path, string? arguments = null, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true,
        };

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new HarnessException($"Failed to start process: {path}");

        return Task.FromResult(ToProcessInfo(process));
    }

    public Task<ProcessInfo> StartAsync(string path, ProcessStartOptions options, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = options.Arguments ?? string.Empty,
        };

        if (options.RunElevated)
        {
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
        }
        else if (options.RedirectOutput)
        {
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
        }
        else
        {
            startInfo.UseShellExecute = true;
        }

        if (options.Hidden)
        {
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            if (!options.RedirectOutput)
                startInfo.CreateNoWindow = true;
        }

        if (options.WorkingDirectory is not null)
            startInfo.WorkingDirectory = options.WorkingDirectory;

        if (options.EnvironmentVariables is not null)
        {
            // UseShellExecute must be false for env vars
            if (startInfo.UseShellExecute && !options.RunElevated)
            {
                startInfo.UseShellExecute = false;
            }

            foreach (var (key, value) in options.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new HarnessException($"Failed to start process: {path}");

        return Task.FromResult(ToProcessInfo(process));
    }

    public Task KillAsync(int pid, CancellationToken ct = default)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Process already exited — not an error
        }

        return Task.CompletedTask;
    }

    public Task KillByNameAsync(string name, CancellationToken ct = default)
    {
        var processes = System.Diagnostics.Process.GetProcessesByName(name);
        foreach (var process in processes)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
            finally
            {
                process.Dispose();
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProcessInfo>> ListAsync(string? filter = null, CancellationToken ct = default)
    {
        var processes = System.Diagnostics.Process.GetProcesses();
        try
        {
            IEnumerable<System.Diagnostics.Process> filtered = processes;
            if (filter is not null)
            {
                filtered = processes.Where(p => p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            var result = filtered.Select(p => ToProcessInfo(p)).ToList();
            return Task.FromResult<IReadOnlyList<ProcessInfo>>(result);
        }
        finally
        {
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
    }

    public Task<bool> IsRunningAsync(string name, CancellationToken ct = default)
    {
        var processes = System.Diagnostics.Process.GetProcessesByName(name);
        var running = processes.Length > 0;
        foreach (var p in processes)
        {
            p.Dispose();
        }

        return Task.FromResult(running);
    }

    public Task<IReadOnlyList<ProcessInfo>> FindByPortAsync(int port, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var pids = new HashSet<int>();

            // Use netstat to find PIDs by port (IPGlobalProperties doesn't expose PIDs)
            try
            {
                var psi = new ProcessStartInfo("netstat", $"-ano")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                using var netstat = System.Diagnostics.Process.Start(psi);
                if (netstat is null) return (IReadOnlyList<ProcessInfo>)Array.Empty<ProcessInfo>();

                var output = netstat.StandardOutput.ReadToEnd();
                netstat.WaitForExit();

                var portStr = $":{port} ";
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!line.Contains(portStr)) continue;

                    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5 && int.TryParse(parts[^1], out var pid) && pid > 0)
                    {
                        pids.Add(pid);
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
            {
                // netstat not available or failed to start — return empty
            }

            var result = new List<ProcessInfo>();
            foreach (var pid in pids)
            {
                try
                {
                    using var proc = System.Diagnostics.Process.GetProcessById(pid);
                    result.Add(ToProcessInfo(proc));
                }
                catch (ArgumentException) { /* Process may have exited */ }
            }

            return (IReadOnlyList<ProcessInfo>)result;
        }, ct);
    }

    public Task<IReadOnlyList<ProcessInfo>> FindByPathAsync(string executablePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var normalizedPath = Path.GetFullPath(executablePath);
            var processes = System.Diagnostics.Process.GetProcesses();
            try
            {
                var result = new List<ProcessInfo>();
                foreach (var p in processes)
                {
                    try
                    {
                        var exePath = p.MainModule?.FileName;
                        if (exePath is not null &&
                            exePath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(ToProcessInfo(p));
                        }
                    }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
                }

                return (IReadOnlyList<ProcessInfo>)result;
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }, ct);
    }

    public Task<IReadOnlyList<ProcessInfo>> FindByWindowTitleAsync(string titleSubstring, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            try
            {
                var result = new List<ProcessInfo>();
                foreach (var p in processes)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(p.MainWindowTitle) &&
                            p.MainWindowTitle.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(ToProcessInfo(p));
                        }
                    }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
                }

                return (IReadOnlyList<ProcessInfo>)result;
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }, ct);
    }

    public Task<IReadOnlyList<ProcessInfo>> GetChildProcessesAsync(int pid, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var childPids = GetChildPidsViaToolhelp32((uint)pid);
            var children = new List<ProcessInfo>();

            foreach (var childPid in childPids)
            {
                try
                {
                    using var proc = System.Diagnostics.Process.GetProcessById((int)childPid);
                    var info = ToProcessInfo(proc, parentPid: pid);
                    children.Add(info);
                }
                catch (ArgumentException) { /* Process exited between snapshot and lookup */ }
            }

            return (IReadOnlyList<ProcessInfo>)children;
        }, ct);
    }

    private static List<uint> GetChildPidsViaToolhelp32(uint parentPid)
    {
        var childPids = new List<uint>();

        var snapshot = CreateToolhelp32Snapshot(0x00000002 /* TH32CS_SNAPPROCESS */, 0);
        if (snapshot == nint.Zero || snapshot == new nint(-1))
            return childPids;

        try
        {
            var entry = new PROCESSENTRY32W();
            entry.dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>();

            if (!Process32FirstW(snapshot, ref entry))
                return childPids;

            do
            {
                if (entry.th32ParentProcessID == parentPid && entry.th32ProcessID != parentPid)
                {
                    childPids.Add(entry.th32ProcessID);
                }
            } while (Process32NextW(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return childPids;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nuint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    public Task KillTreeAsync(int pid, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
            }
            catch (ArgumentException)
            {
                // Process already exited
            }
        }, ct);
    }

    public async Task<bool> WaitForExitAsync(int pid, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            if (timeout.HasValue)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout.Value);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                    return true;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    return false; // Timeout
                }
            }
            else
            {
                await process.WaitForExitAsync(ct);
                return true;
            }
        }
        catch (ArgumentException)
        {
            return true; // Process already exited
        }
    }

    private static ProcessInfo ToProcessInfo(System.Diagnostics.Process process, int? parentPid = null)
    {
        string? exePath = null;
        string? windowTitle = null;
        DateTimeOffset? startTime = null;
        var isRunning = false;
        long? memoryUsage = null;

        try { exePath = process.MainModule?.FileName; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
        try { windowTitle = process.MainWindowTitle; }
        catch (InvalidOperationException) { }
        try { startTime = process.StartTime; isRunning = !process.HasExited; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        try { memoryUsage = process.WorkingSet64; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }

        return new ProcessInfo
        {
            Pid = process.Id,
            Name = process.ProcessName,
            ExecutablePath = exePath,
            MainWindowTitle = windowTitle,
            IsRunning = isRunning,
            StartTime = startTime,
            ParentPid = parentPid,
            MemoryUsageBytes = memoryUsage,
        };
    }
}
