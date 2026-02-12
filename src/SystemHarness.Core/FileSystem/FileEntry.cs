namespace SystemHarness;

/// <summary>
/// Represents a file or directory entry in a listing.
/// </summary>
public sealed class FileEntry
{
    /// <summary>
    /// Full path to the file or directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// File or directory name (without parent path).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// True if this entry is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// File size in bytes. 0 for directories.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Last modification time.
    /// </summary>
    public DateTimeOffset? LastModified { get; init; }
}
