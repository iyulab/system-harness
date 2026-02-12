namespace SystemHarness;

/// <summary>
/// Multi-display management — enumerate monitors, query DPI, map coordinates.
/// </summary>
public interface IDisplay
{
    /// <summary>
    /// Gets information about all connected monitors.
    /// </summary>
    Task<IReadOnlyList<MonitorInfo>> GetMonitorsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets information about the primary monitor.
    /// </summary>
    Task<MonitorInfo> GetPrimaryMonitorAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the monitor that contains the specified point.
    /// </summary>
    Task<MonitorInfo> GetMonitorAtPointAsync(int x, int y, CancellationToken ct = default);

    /// <summary>
    /// Gets the monitor that contains the majority of the specified window.
    /// </summary>
    Task<MonitorInfo> GetMonitorForWindowAsync(string titleOrHandle, CancellationToken ct = default);

    /// <summary>
    /// Gets the bounding rectangle of the virtual screen (all monitors combined).
    /// </summary>
    Task<Rectangle> GetVirtualScreenBoundsAsync(CancellationToken ct = default);
}
