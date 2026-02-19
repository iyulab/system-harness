using ChildProcessGuard;

namespace SystemHarness.Tests;

/// <summary>
/// Shared fixture that ensures all child processes started during desktop interaction tests
/// are cleaned up via Windows Job Objects, even when assertions fail before cleanup runs.
/// Also snapshots Notepad handles at init and closes any new ones at dispose,
/// handling the Win11 Store Notepad PID mismatch problem.
/// </summary>
public class DesktopInteractionFixture : IAsyncLifetime
{
    public ProcessGuardian Guardian { get; } = new();
    private HashSet<nint> _initialNotepadHandles = [];

    public async Task InitializeAsync()
    {
        _initialNotepadHandles = await NotepadHelper.SnapshotNotepadHandlesAsync();
    }

    public async Task DisposeAsync()
    {
        // Close any Notepad windows created during this test collection
        await NotepadHelper.CloseNewNotepadWindowsAsync(_initialNotepadHandles);
        await Guardian.KillAllProcessesAsync();
        Guardian.Dispose();
    }
}

/// <summary>
/// Collection definition for tests that interact with the desktop (window focus, input simulation).
/// Tests in this collection run sequentially to avoid focus/input conflicts.
/// </summary>
[CollectionDefinition("DesktopInteraction", DisableParallelization = true)]
public class DesktopInteractionDefinition : ICollectionFixture<DesktopInteractionFixture>;
