namespace SystemHarness;

/// <summary>
/// Records and replays user actions (mouse and keyboard events).
/// Enables AI agents to learn from human demonstrations and repeat workflows.
/// </summary>
public interface IActionRecorder
{
    /// <summary>
    /// Starts recording mouse and keyboard actions using global hooks.
    /// </summary>
    Task StartRecordingAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops recording and finalizes the action sequence.
    /// </summary>
    Task StopRecordingAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the list of recorded actions since the last <see cref="StartRecordingAsync"/>.
    /// </summary>
    Task<IReadOnlyList<RecordedAction>> GetRecordedActionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Replays a sequence of recorded actions using Mouse and Keyboard APIs.
    /// Respects the original timing (DelayBefore) between actions.
    /// </summary>
    /// <param name="actions">The actions to replay.</param>
    /// <param name="speedMultiplier">Replay speed multiplier (1.0 = original speed, 2.0 = double speed).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ReplayAsync(IReadOnlyList<RecordedAction> actions, double speedMultiplier = 1.0, CancellationToken ct = default);
}
