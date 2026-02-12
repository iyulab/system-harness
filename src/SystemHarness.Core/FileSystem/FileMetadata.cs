namespace SystemHarness;

/// <summary>
/// Detailed metadata about a file or directory.
/// </summary>
public sealed class FileMetadata
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
    /// File extension including the dot (e.g., ".txt"), or empty for directories.
    /// </summary>
    public string Extension { get; init; } = "";

    /// <summary>
    /// True if this entry is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// True if the file or directory is read-only.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// True if the file or directory is hidden.
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// File size in bytes. 0 for directories.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Creation time (UTC).
    /// </summary>
    public DateTimeOffset? CreatedTime { get; init; }

    /// <summary>
    /// Last modification time (UTC).
    /// </summary>
    public DateTimeOffset? LastModifiedTime { get; init; }

    /// <summary>
    /// Last access time (UTC).
    /// </summary>
    public DateTimeOffset? LastAccessTime { get; init; }
}
