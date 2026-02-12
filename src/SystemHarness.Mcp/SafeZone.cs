using SystemHarness;

namespace SystemHarness.Mcp;

/// <summary>
/// Thread-safe safe zone configuration.
/// When set, mouse/keyboard actions should be restricted to the specified window/region.
/// </summary>
public static class SafeZone
{
    private static readonly object Lock = new();
    private static SafeZoneConfig? _current;

    /// <summary>
    /// Gets the current safe zone, or null if no restriction is active.
    /// </summary>
    public static SafeZoneConfig? Current
    {
        get { lock (Lock) return _current; }
    }

    /// <summary>
    /// Sets the safe zone to restrict actions to a window and optional region.
    /// </summary>
    public static void Set(string window, Rectangle? region = null)
    {
        lock (Lock)
            _current = new SafeZoneConfig(window, region);
    }

    /// <summary>
    /// Clears the safe zone, allowing unrestricted actions.
    /// </summary>
    public static void Clear()
    {
        lock (Lock)
            _current = null;
    }
}

/// <summary>
/// Safe zone configuration.
/// </summary>
public sealed record SafeZoneConfig(string Window, Rectangle? Region);
