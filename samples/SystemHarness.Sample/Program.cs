using SystemHarness;
using SystemHarness.Windows;

// Create harness with safety features
var options = new HarnessOptions
{
    CommandPolicy = CommandPolicy.CreateDefault(),
    AuditLog = new InMemoryAuditLog(),
};

using var harness = new WindowsHarness(options);

// ── Layer 1: Programmatic Control ──

// Shell
Console.WriteLine("=== Shell ===");
var result = await harness.Shell.RunAsync("cmd", "/C echo Hello from system-harness!");
Console.WriteLine($"Output: {result.StdOut.Trim()}");
Console.WriteLine($"Exit code: {result.ExitCode}, Elapsed: {result.Elapsed.TotalMilliseconds:F0}ms");

// FileSystem
Console.WriteLine("\n=== FileSystem ===");
var tempFile = Path.Combine(Path.GetTempPath(), "harness-sample.txt");
await harness.FileSystem.WriteAsync(tempFile, "Written by system-harness");
var content = await harness.FileSystem.ReadAsync(tempFile);
Console.WriteLine($"File content: {content}");
await harness.FileSystem.DeleteAsync(tempFile);

// Process
Console.WriteLine("\n=== Process ===");
var processes = await harness.Process.ListAsync("explorer");
Console.WriteLine($"Explorer processes: {processes.Count}");

// Window
Console.WriteLine("\n=== Window ===");
var windows = await harness.Window.ListAsync();
Console.WriteLine($"Open windows: {windows.Count}");
foreach (var w in windows.Take(3))
    Console.WriteLine($"  - {w.Title} (PID: {w.ProcessId})");

// Clipboard
Console.WriteLine("\n=== Clipboard ===");
await harness.Clipboard.SetTextAsync("Copied by system-harness");
var clipText = await harness.Clipboard.GetTextAsync();
Console.WriteLine($"Clipboard: {clipText}");

// ── Layer 2: Vision + Action ──

// Screen
Console.WriteLine("\n=== Screen ===");
using var screenshot = await harness.Screen.CaptureAsync();
Console.WriteLine($"Screenshot: {screenshot.Width}x{screenshot.Height}, {screenshot.Bytes.Length / 1024}KB ({screenshot.MimeType})");

// Mouse
Console.WriteLine("\n=== Mouse ===");
var (x, y) = await harness.Mouse.GetPositionAsync();
Console.WriteLine($"Mouse position: ({x}, {y})");

// ── Phase 7-12: Extended APIs ──

// Display (multi-monitor)
Console.WriteLine("\n=== Display ===");
var monitors = await harness.Display.GetMonitorsAsync();
Console.WriteLine($"Monitors: {monitors.Count}");
foreach (var m in monitors)
    Console.WriteLine($"  - {m.Name}: {m.Bounds.Width}x{m.Bounds.Height} @ {m.DpiX}dpi (Primary: {m.IsPrimary})");

// Process extensions
Console.WriteLine("\n=== Process Extensions ===");
var proc = await harness.Process.StartAsync("notepad.exe");
await Task.Delay(1000);
var children = await harness.Process.GetChildProcessesAsync(Environment.ProcessId);
Console.WriteLine($"Child processes of this app: {children.Count}");
var exited = await harness.Process.WaitForExitAsync(proc.Pid, TimeSpan.FromMilliseconds(100));
Console.WriteLine($"Notepad exited within 100ms: {exited}");
await harness.Process.KillTreeAsync(proc.Pid);
Console.WriteLine("Notepad killed via KillTree");

// Window extensions
Console.WriteLine("\n=== Window Extensions ===");
var fg = await harness.Window.GetForegroundAsync();
if (fg is not null)
    Console.WriteLine($"Foreground window: {fg.Title} (State: {await harness.Window.GetStateAsync(fg.Title)})");

// Clipboard extensions
Console.WriteLine("\n=== Clipboard Extensions ===");
var formats = await harness.Clipboard.GetAvailableFormatsAsync();
Console.WriteLine($"Available formats: {string.Join(", ", formats.Take(5))}");

// SystemInfo
Console.WriteLine("\n=== SystemInfo ===");
var machine = await harness.SystemInfo.GetMachineNameAsync();
var user = await harness.SystemInfo.GetUserNameAsync();
var os = await harness.SystemInfo.GetOSVersionAsync();
Console.WriteLine($"Machine: {machine}, User: {user}, OS: {os}");

// ── Safety Features ──

// Command Policy
Console.WriteLine("\n=== Safety ===");
try
{
    await harness.Shell.RunAsync("format", "C: /FS:NTFS");
}
catch (CommandPolicyException ex)
{
    Console.WriteLine($"Blocked: {ex.Message}");
}

// Audit Log
var auditLog = options.AuditLog as InMemoryAuditLog;
var entries = await auditLog!.GetEntriesAsync();
Console.WriteLine($"\nAudit log entries: {entries.Count}");
foreach (var entry in entries.TakeLast(3))
    Console.WriteLine($"  [{entry.Category}] {entry.Action}: {entry.Details} ({entry.Duration?.TotalMilliseconds:F0}ms)");

Console.WriteLine("\nDone! system-harness is ready.");
