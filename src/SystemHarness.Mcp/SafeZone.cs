using SystemHarness;

namespace SystemHarness.Mcp;

/// <summary>
/// Thread-safe safe zone configuration.
/// When set, input actions (mouse, keyboard, UI automation, vision clicks, dialogs, window changes) are
/// refused unless they target the zone window — see <see cref="SafetyGate"/>.
/// A zone set by the operator at startup is locked: the agent can neither move nor clear it.
/// </summary>
public sealed class SafeZone
{
    private readonly object _lock = new();
    private SafeZoneConfig? _current;
    private bool _operatorLocked;

    /// <summary>
    /// Gets the current safe zone, or null if no restriction is active.
    /// </summary>
    public SafeZoneConfig? Current
    {
        get { lock (_lock) return _current; }
    }

    /// <summary>
    /// Whether the zone was set by the operator and cannot be changed through <see cref="Set"/> or <see cref="Clear"/>.
    /// </summary>
    public bool IsOperatorLocked
    {
        get { lock (_lock) return _operatorLocked; }
    }

    /// <summary>
    /// Sets the zone on behalf of the operator and locks it.
    /// </summary>
    public void SetByOperator(string window, Rectangle? region = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(window);
        lock (_lock)
        {
            _current = new SafeZoneConfig(window, region);
            _operatorLocked = true;
        }
    }

    /// <summary>
    /// Sets the safe zone to restrict actions to a window and optional region.
    /// </summary>
    /// <exception cref="HarnessException">The operator locked the zone.</exception>
    public void Set(string window, Rectangle? region = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(window);
        lock (_lock)
        {
            ThrowIfLocked();
            _current = new SafeZoneConfig(window, region);
        }
    }

    /// <summary>
    /// Clears the safe zone, allowing unrestricted actions.
    /// </summary>
    /// <exception cref="HarnessException">The operator locked the zone.</exception>
    public void Clear()
    {
        lock (_lock)
        {
            ThrowIfLocked();
            _current = null;
        }
    }

    private void ThrowIfLocked()
    {
        if (_operatorLocked)
            throw new HarnessException("The safe zone was set by the operator and cannot be changed.");
    }
}

/// <summary>
/// Safe zone configuration. <see cref="Region"/> is relative to the window's top-left corner.
/// </summary>
public sealed record SafeZoneConfig(string Window, Rectangle? Region);
