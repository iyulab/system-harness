using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using SystemHarness.Mcp.Update;

namespace SystemHarness.Mcp.Tools;

public sealed class UpdateTools(AutoUpdater updater)
{
    [McpServerTool(Name = "update_check"), Description(
        "Check for available MCP server updates from GitHub Releases. " +
        "Returns current version and latest available version if an update exists.")]
    public async Task<string> CheckAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var release = await updater.CheckForUpdateAsync(ct);

        if (release is null)
            return McpResponse.Ok(new
            {
                upToDate = true,
                currentVersion = updater.CurrentVersion,
                pendingUpdate = updater.HasPendingUpdate,
            }, sw.ElapsedMilliseconds);

        return McpResponse.Ok(new
        {
            upToDate = false,
            currentVersion = updater.CurrentVersion,
            latestVersion = release.Version,
            publishedAt = release.PublishedAt,
            pendingUpdate = updater.HasPendingUpdate,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "update_apply"), Description(
        "Download and stage the latest update. " +
        "The update will be applied automatically on next MCP server restart.")]
    public async Task<string> ApplyAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (updater.HasPendingUpdate)
            return McpResponse.Ok(new
            {
                message = "Update already staged. Will apply on next restart.",
                currentVersion = updater.CurrentVersion,
            }, sw.ElapsedMilliseconds);

        var release = await updater.CheckForUpdateAsync(ct);
        if (release is null)
            return McpResponse.Ok(new
            {
                message = "Already up to date.",
                currentVersion = updater.CurrentVersion,
            }, sw.ElapsedMilliseconds);

        var success = await updater.DownloadUpdateAsync(release, ct);
        if (!success)
            return McpResponse.Error("update_failed",
                $"Failed to download v{release.Version}.", sw.ElapsedMilliseconds);

        ActionLog.Record("update_apply", $"v{updater.CurrentVersion} → v{release.Version}",
            sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm(
            $"Update v{release.Version} staged. Will apply on next restart.",
            sw.ElapsedMilliseconds);
    }
}
