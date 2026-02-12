using SharpHook;
using SharpHook.Data;

namespace SystemHarness.Windows;

/// <summary>
/// Monitors a global hotkey and triggers <see cref="EmergencyStop"/> when pressed.
/// Uses SharpHook for cross-platform global keyboard hooks.
/// Default hotkey: Ctrl+Shift+Escape.
/// </summary>
public sealed class EmergencyStopHook : IDisposable
{
    private readonly EmergencyStop _stop;
    private readonly HashSet<KeyCode> _hotkey;
    private readonly object _keyLock = new();
    private readonly HashSet<KeyCode> _pressedKeys = [];
    private SimpleGlobalHook? _hook;
    private Task? _hookTask;
    private bool _disposed;

    /// <summary>
    /// Creates a hook that triggers the given <see cref="EmergencyStop"/> on the specified hotkey.
    /// </summary>
    /// <param name="stop">The emergency stop to trigger.</param>
    /// <param name="hotkey">Key combination to trigger stop. Null uses default (Ctrl+Shift+Escape).</param>
    public EmergencyStopHook(EmergencyStop stop, IReadOnlyCollection<KeyCode>? hotkey = null)
    {
        _stop = stop ?? throw new ArgumentNullException(nameof(stop));
        _hotkey = hotkey is not null
            ? new HashSet<KeyCode>(hotkey)
            : [KeyCode.VcLeftControl, KeyCode.VcLeftShift, KeyCode.VcEscape];
    }

    /// <summary>
    /// Starts listening for the hotkey in a background thread.
    /// </summary>
    public void Start()
    {
        if (_hook is not null) return;

        _hook = new SimpleGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _hookTask = _hook.RunAsync();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        lock (_keyLock)
        {
            _pressedKeys.Add(e.Data.KeyCode);

            if (_hotkey.IsSubsetOf(_pressedKeys))
            {
                _stop.Trigger();
                _pressedKeys.Clear();
            }
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        lock (_keyLock)
        {
            _pressedKeys.Remove(e.Data.KeyCode);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hook is not null)
        {
            _hook.KeyPressed -= OnKeyPressed;
            _hook.KeyReleased -= OnKeyReleased;
            _hook.Dispose();
            _hook = null;
        }
    }
}
