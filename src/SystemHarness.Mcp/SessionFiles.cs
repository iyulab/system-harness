using System.Diagnostics;

namespace SystemHarness.Mcp;

/// <summary>
/// The one directory this server writes its own files to — screenshots, clipboard images, confirmation requests.
/// It is private to the server process (<c>%TEMP%\system-harness\&lt;pid&gt;</c>), deleted when the server stops,
/// and directories left behind by a server that did not stop cleanly are deleted at the next start.
/// File commands cannot write, move, or delete anything inside it (see <see cref="SafetyGate"/>), so the agent cannot
/// answer its own confirmation request by editing the file.
/// </summary>
public static class SessionFiles
{
    private static readonly string Parent = Path.Combine(Path.GetTempPath(), "system-harness");
    private static readonly string RootPath =
        Path.Combine(Parent, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private static readonly Lazy<string> RootLazy = new(() =>
    {
        Directory.CreateDirectory(RootPath);
        return RootPath;
    });
    private static int _sequence;

    /// <summary>
    /// The session directory, created on first use.
    /// </summary>
    public static string Root => RootLazy.Value;

    /// <summary>
    /// A new file path in the session directory. <paramref name="label"/> is reduced to letters, digits, '-' and '_',
    /// so a caller-supplied name cannot point outside the directory.
    /// </summary>
    public static string NewPath(string label, string extension)
    {
        var safe = new string(label.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (safe.Length == 0) safe = "file";
        var n = Interlocked.Increment(ref _sequence);
        return Path.Combine(Root, $"harness-{safe}-{DateTime.Now:HHmmss}-{n}.{extension.TrimStart('.')}");
    }

    /// <summary>
    /// Whether <paramref name="path"/> resolves inside the session directory.
    /// </summary>
    public static bool Contains(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var root = Path.GetFullPath(RootPath);
        return full.Equals(root, StringComparison.OrdinalIgnoreCase)
               || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deletes this session's directory. Best effort: a file still open elsewhere stays until the next start.
    /// </summary>
    public static void DeleteCurrent()
    {
        if (!RootLazy.IsValueCreated) return;
        TryDelete(RootLazy.Value);
    }

    /// <summary>
    /// Deletes directories left by servers that are no longer running.
    /// </summary>
    public static void DeleteAbandoned()
    {
        if (!Directory.Exists(Parent)) return;

        foreach (var dir in Directory.EnumerateDirectories(Parent))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid) || pid == Environment.ProcessId)
                continue;

            if (!IsRunning(pid))
                TryDelete(dir);
        }
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // A file is still open; the next start retries
        }
        catch (UnauthorizedAccessException)
        {
            // Same
        }
    }
}
