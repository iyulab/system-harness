namespace SystemHarness;

/// <summary>
/// File and directory operations — read, write, copy, move, delete.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Reads the entire contents of a text file.
    /// </summary>
    Task<string> ReadAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Reads the entire contents of a file as a byte array.
    /// </summary>
    Task<byte[]> ReadBytesAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Writes text content to a file, creating it if it doesn't exist or overwriting if it does.
    /// </summary>
    Task WriteAsync(string path, string content, CancellationToken ct = default);

    /// <summary>
    /// Writes binary data to a file, creating it if it doesn't exist or overwriting if it does.
    /// </summary>
    Task WriteBytesAsync(string path, byte[] data, CancellationToken ct = default);

    /// <summary>
    /// Copies a file from <paramref name="source"/> to <paramref name="destination"/>.
    /// </summary>
    Task CopyAsync(string source, string destination, CancellationToken ct = default);

    /// <summary>
    /// Moves a file from <paramref name="source"/> to <paramref name="destination"/>.
    /// </summary>
    Task MoveAsync(string source, string destination, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file or directory at the specified path.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Returns whether a file or directory exists at the specified path.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Lists files and directories at the specified path. Optionally filters by a glob pattern.
    /// </summary>
    Task<IReadOnlyList<FileEntry>> ListAsync(string path, string? pattern = null, CancellationToken ct = default);

    /// <summary>
    /// Creates a directory at the specified path, including any missing parent directories.
    /// </summary>
    Task CreateDirectoryAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed metadata about a file or directory.
    /// </summary>
    Task<FileMetadata> GetInfoAsync(string path, CancellationToken ct = default)
        => throw new NotSupportedException("GetInfoAsync is not supported by this implementation.");

    /// <summary>
    /// Recursively searches for files and directories matching a name pattern.
    /// </summary>
    Task<IReadOnlyList<FileEntry>> SearchAsync(string path, string pattern, int maxResults = 100, CancellationToken ct = default)
        => throw new NotSupportedException("SearchAsync is not supported by this implementation.");
}
