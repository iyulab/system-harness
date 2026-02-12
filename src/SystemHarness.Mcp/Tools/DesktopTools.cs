using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class DesktopTools(IHarness harness)
{
    [McpServerTool(Name = "desktop_count"), Description("Get the number of virtual desktops.")]
    public async Task<string> GetCountAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var count = await harness.VirtualDesktop.GetDesktopCountAsync(ct);
        return McpResponse.Ok(new { count }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "desktop_current"), Description("Get the zero-based index of the current virtual desktop.")]
    public async Task<string> GetCurrentAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var index = await harness.VirtualDesktop.GetCurrentDesktopIndexAsync(ct);
        return McpResponse.Ok(new { index }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "desktop_switch"), Description("Switch to a virtual desktop by zero-based index.")]
    public async Task<string> SwitchAsync(
        [Description("Zero-based index of the virtual desktop to switch to.")] int index,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (index < 0)
            return McpResponse.Error("invalid_parameter", $"index must be non-negative (got {index}).", sw.ElapsedMilliseconds);
        await harness.VirtualDesktop.SwitchToDesktopAsync(index, ct);
        ActionLog.Record("desktop_switch", $"index={index}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Switched to virtual desktop {index}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "desktop_move_window"), Description("Move a window to a specific virtual desktop.")]
    public async Task<string> MoveWindowAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Zero-based index of the target virtual desktop.")] int desktopIndex,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (desktopIndex < 0)
            return McpResponse.Error("invalid_parameter", $"desktopIndex must be non-negative (got {desktopIndex}).", sw.ElapsedMilliseconds);
        await harness.VirtualDesktop.MoveWindowToDesktopAsync(titleOrHandle, desktopIndex, ct);
        ActionLog.Record("desktop_move_window", $"window={titleOrHandle}, desktop={desktopIndex}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Moved '{titleOrHandle}' to virtual desktop {desktopIndex}.", sw.ElapsedMilliseconds);
    }
}
