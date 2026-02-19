using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace SystemHarness.Mcp.Update;

/// <summary>
/// Shadow-copy auto-updater for the MCP server.
/// Downloads updates from GitHub Releases and stages them for next launch.
/// </summary>
public sealed class AutoUpdater : IDisposable
{
    private const string ReleasesUrl = "https://api.github.com/repos/iyulab/system-harness/releases/latest";
    private const string AssetName = "system-harness-mcp-win-x64.zip";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly HttpClient _http;
    private readonly string _exePath;
    private readonly string _exeDir;

    public string CurrentVersion { get; }

    public AutoUpdater()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("system-harness-mcp");
        _http.Timeout = TimeSpan.FromMinutes(5);

        _exePath = Environment.ProcessPath!;
        _exeDir = Path.GetDirectoryName(_exePath)!;
        CurrentVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion?.Split('+')[0] ?? "0.0.0";
    }

    private string UpdatePath => _exePath + ".update";
    private string OldPath => _exePath + ".old";
    private string CheckFile => Path.Combine(_exeDir, ".update-check");

    /// <summary>
    /// Apply pending update on startup. Call before MCP initialization.
    /// Renames: current → .old, .update → current, deletes .old.
    /// </summary>
    public static void ApplyPendingUpdate()
    {
        var exePath = Environment.ProcessPath!;
        var updatePath = exePath + ".update";
        var oldPath = exePath + ".old";

        // Clean up old version from previous swap
        if (File.Exists(oldPath))
            try { File.Delete(oldPath); } catch { /* next launch will retry */ }

        if (!File.Exists(updatePath)) return;

        try
        {
            File.Move(exePath, oldPath);
            File.Move(updatePath, exePath);
            try { File.Delete(oldPath); } catch { /* still locked — next launch cleans up */ }
        }
        catch
        {
            // Rollback if swap fails
            if (!File.Exists(exePath) && File.Exists(oldPath))
                try { File.Move(oldPath, exePath); } catch { }
        }
    }

    /// <summary>
    /// Background check with 24h cooldown. Fire-and-forget after server starts.
    /// Waits 5 seconds before checking to avoid startup contention.
    /// </summary>
    public async Task BackgroundCheckAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            if (!ShouldCheck()) return;

            WriteCheckTimestamp();
            var release = await CheckForUpdateAsync(ct);
            if (release is not null)
                await DownloadUpdateAsync(release, ct);
        }
        catch { /* silent — never crash the server */ }
    }

    /// <summary>
    /// Check GitHub Releases for a newer version. Returns null if up-to-date.
    /// </summary>
    public async Task<ReleaseInfo?> CheckForUpdateAsync(CancellationToken ct)
    {
        var response = await _http.GetAsync(ReleasesUrl, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var root = doc.RootElement;
        var tagName = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";

        if (!Version.TryParse(tagName, out var latest) ||
            !Version.TryParse(CurrentVersion, out var current) ||
            latest <= current)
            return null;

        string? downloadUrl = null;
        var publishedAt = root.GetProperty("published_at").GetString();

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == AssetName)
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        return downloadUrl is null ? null : new ReleaseInfo(tagName, downloadUrl, publishedAt);
    }

    /// <summary>
    /// Download the release zip and extract the exe as .update file.
    /// </summary>
    public async Task<bool> DownloadUpdateAsync(ReleaseInfo release, CancellationToken ct)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"system-harness-mcp-{release.Version}.zip");
        try
        {
            using (var stream = await _http.GetStreamAsync(release.DownloadUrl, ct))
            using (var file = File.Create(tempZip))
                await stream.CopyToAsync(file, ct);

            using var archive = ZipFile.OpenRead(tempZip);
            var exeName = Path.GetFileName(_exePath);
            var entry = archive.Entries.FirstOrDefault(e =>
                e.Name.Equals(exeName, StringComparison.OrdinalIgnoreCase));

            if (entry is null) return false;

            entry.ExtractToFile(UpdatePath, overwrite: true);
            return true;
        }
        catch { return false; }
        finally
        {
            try { File.Delete(tempZip); } catch { }
        }
    }

    public bool HasPendingUpdate => File.Exists(UpdatePath);

    private bool ShouldCheck()
    {
        if (File.Exists(UpdatePath)) return false;
        if (!File.Exists(CheckFile)) return true;

        try
        {
            var lastCheck = DateTime.Parse(File.ReadAllText(CheckFile).Trim(), CultureInfo.InvariantCulture);
            return DateTime.UtcNow - lastCheck > CheckInterval;
        }
        catch { return true; }
    }

    private void WriteCheckTimestamp()
    {
        try { File.WriteAllText(CheckFile, DateTime.UtcNow.ToString("O")); } catch { }
    }

    public void Dispose() => _http.Dispose();
}

public sealed record ReleaseInfo(string Version, string DownloadUrl, string? PublishedAt);
