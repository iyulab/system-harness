namespace SystemHarness;

/// <summary>
/// Base exception for all system-harness operations.
/// </summary>
public class HarnessException : Exception
{
    public HarnessException(string message) : base(message) { }
    public HarnessException(string message, Exception innerException) : base(message, innerException) { }
}
