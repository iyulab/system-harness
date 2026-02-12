namespace SystemHarness;

/// <summary>
/// Virtual desktop management (Windows 10/11 virtual desktops).
/// </summary>
public interface IVirtualDesktop
{
    /// <summary>
    /// Gets the number of virtual desktops.
    /// </summary>
    Task<int> GetDesktopCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the zero-based index of the current virtual desktop.
    /// </summary>
    Task<int> GetCurrentDesktopIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Switches to the virtual desktop at the specified index.
    /// </summary>
    Task SwitchToDesktopAsync(int index, CancellationToken ct = default);

    /// <summary>
    /// Moves a window to a specific virtual desktop.
    /// </summary>
    Task MoveWindowToDesktopAsync(string titleOrHandle, int desktopIndex, CancellationToken ct = default);
}
