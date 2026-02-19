using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SystemHarness.Mcp.Tools;

public sealed class FileSystemTools(IHarness harness)
{
    [McpServerTool(Name = "file_read"), Description("Read the text contents of a file.")]
    public async Task<string> ReadAsync(
        [Description("Absolute or relative file path to read.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var exists = await harness.FileSystem.ExistsAsync(path, ct);
        if (!exists)
            return McpResponse.Error("file_not_found", $"File not found: '{path}'", sw.ElapsedMilliseconds);
        var content = await harness.FileSystem.ReadAsync(path, ct);
        return McpResponse.Content(content, "text", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_write"), Description("Write text content to a file (creates or overwrites).")]
    public async Task<string> WriteAsync(
        [Description("File path to write to (creates parent directories).")] string path,
        [Description("Text content to write.")] string content,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        await harness.FileSystem.WriteAsync(path, content, ct);
        ActionLog.Record("file_write", $"path={path}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Written to {path}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_read_bytes"), Description(
        "Read a file as binary data, returned as base64-encoded string. " +
        "Use for images, archives, executables, or any non-text file.")]
    public async Task<string> ReadBytesAsync(
        [Description("Absolute or relative file path to read.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var exists = await harness.FileSystem.ExistsAsync(path, ct);
        if (!exists)
            return McpResponse.Error("file_not_found", $"File not found: '{path}'", sw.ElapsedMilliseconds);
        var info = await harness.FileSystem.GetInfoAsync(path, ct);
        if (info.Size > 10 * 1024 * 1024)
            return McpResponse.Error("invalid_parameter",
                $"File too large ({info.Size / (1024 * 1024)}MB). Maximum is 10MB for base64 transfer.", sw.ElapsedMilliseconds);
        var bytes = await harness.FileSystem.ReadBytesAsync(path, ct);
        return McpResponse.Ok(new
        {
            path,
            sizeBytes = bytes.Length,
            base64 = Convert.ToBase64String(bytes),
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_write_bytes"), Description(
        "Write binary data (base64-encoded) to a file (creates or overwrites). " +
        "Use for images, archives, or any non-text file.")]
    public async Task<string> WriteBytesAsync(
        [Description("File path to write to.")] string path,
        [Description("Base64-encoded binary content to write.")] string base64Content,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        if (string.IsNullOrWhiteSpace(base64Content))
            return McpResponse.Error("invalid_parameter", "base64Content cannot be empty.", sw.ElapsedMilliseconds);
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Content);
        }
        catch (FormatException)
        {
            return McpResponse.Error("invalid_parameter", "base64Content is not valid base64.", sw.ElapsedMilliseconds);
        }
        await harness.FileSystem.WriteBytesAsync(path, bytes, ct);
        ActionLog.Record("file_write_bytes", $"path={path}, size={bytes.Length}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Written {bytes.Length} bytes to {path}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_hash"), Description(
        "Compute a cryptographic hash of a file. Returns hex-encoded hash. " +
        "Useful for integrity verification, duplicate detection, and change detection.")]
    public async Task<string> HashAsync(
        [Description("File path to hash.")] string path,
        [Description("Hash algorithm: 'sha256' (default), 'sha1', or 'md5'.")] string algorithm = "sha256",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var exists = await harness.FileSystem.ExistsAsync(path, ct);
        if (!exists)
            return McpResponse.Error("file_not_found", $"File not found: '{path}'", sw.ElapsedMilliseconds);
        var bytes = await harness.FileSystem.ReadBytesAsync(path, ct);
        // SHA1/MD5 used for file fingerprinting, not security — suppress weak-crypto warnings
#pragma warning disable CA5350, CA5351
        byte[] hash = algorithm.ToLowerInvariant() switch
        {
            "sha256" => SHA256.HashData(bytes),
            "sha1" => SHA1.HashData(bytes),
            "md5" => MD5.HashData(bytes),
            _ => [],
        };
#pragma warning restore CA5350, CA5351
        if (hash.Length == 0)
            return McpResponse.Error("invalid_parameter",
                $"Unsupported algorithm '{algorithm}'. Use 'sha256', 'sha1', or 'md5'.", sw.ElapsedMilliseconds);
        return McpResponse.Ok(new
        {
            path,
            algorithm = algorithm.ToLowerInvariant(),
            hash = Convert.ToHexString(hash).ToLowerInvariant(),
            sizeBytes = bytes.Length,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_list"), Description("List files and directories in a path. Optional glob pattern.")]
    public async Task<string> ListAsync(
        [Description("Directory path to list.")] string path,
        [Description("Optional glob pattern to filter entries (e.g., '*.txt', '*.cs').")] string? pattern = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var entries = await harness.FileSystem.ListAsync(path, pattern, ct);
        return McpResponse.Items(entries.Select(e => new
        {
            e.Name, e.IsDirectory, e.Size, e.LastModified,
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_info"), Description(
        "Get detailed metadata about a file or directory. " +
        "Returns name, extension, size, creation time, last modified time, " +
        "last access time, and attributes (read-only, hidden).")]
    public async Task<string> GetInfoAsync(
        [Description("Absolute or relative path to the file or directory.")] string path,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var exists = await harness.FileSystem.ExistsAsync(path, ct);
        if (!exists)
            return McpResponse.Error("file_not_found",
                $"File or directory not found: '{path}'", sw.ElapsedMilliseconds);
        var info = await harness.FileSystem.GetInfoAsync(path, ct);
        return McpResponse.Ok(new
        {
            info.Path,
            info.Name,
            info.Extension,
            info.IsDirectory,
            info.IsReadOnly,
            info.IsHidden,
            info.Size,
            sizeMB = info.Size / (1024.0 * 1024.0),
            createdTime = info.CreatedTime?.ToString("O"),
            lastModifiedTime = info.LastModifiedTime?.ToString("O"),
            lastAccessTime = info.LastAccessTime?.ToString("O"),
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_search"), Description(
        "Recursively search for files and directories by name pattern. " +
        "Searches all subdirectories and returns matching entries with path, size, and modified time.")]
    public async Task<string> SearchAsync(
        [Description("Directory path to search from.")] string path,
        [Description("File name pattern to match (e.g., '*.txt', 'readme*', '*.cs').")] string pattern,
        [Description("Maximum number of results to return.")] int maxResults = 100,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        if (string.IsNullOrWhiteSpace(pattern))
            return McpResponse.Error("invalid_parameter", "pattern cannot be empty.", sw.ElapsedMilliseconds);
        if (maxResults <= 0)
            return McpResponse.Error("invalid_parameter",
                $"maxResults must be positive (got {maxResults}).", sw.ElapsedMilliseconds);
        var entries = await harness.FileSystem.SearchAsync(path, pattern, maxResults, ct);
        return McpResponse.Items(entries.Select(e => new
        {
            e.Path, e.Name, e.IsDirectory, e.Size, e.LastModified,
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_check"), Description("Check if a file or directory exists at the given path.")]
    public async Task<string> ExistsAsync(
        [Description("Path to check for existence.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var exists = await harness.FileSystem.ExistsAsync(path, ct);
        return McpResponse.Check(exists, exists ? $"'{path}' exists." : $"'{path}' does not exist.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_copy"), Description("Copy a file or directory from source to destination.")]
    public async Task<string> CopyAsync(
        [Description("Source file or directory path.")] string source,
        [Description("Destination file or directory path.")] string destination,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(source))
            return McpResponse.Error("invalid_parameter", "source path cannot be empty.", sw.ElapsedMilliseconds);
        if (string.IsNullOrWhiteSpace(destination))
            return McpResponse.Error("invalid_parameter", "destination path cannot be empty.", sw.ElapsedMilliseconds);
        var exists = await harness.FileSystem.ExistsAsync(source, ct);
        if (!exists)
            return McpResponse.Error("file_not_found", $"Source not found: '{source}'", sw.ElapsedMilliseconds);
        await harness.FileSystem.CopyAsync(source, destination, ct);
        ActionLog.Record("file_copy", $"src={source}, dst={destination}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Copied '{source}' to '{destination}'.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_move"), Description("Move (rename) a file or directory from source to destination.")]
    public async Task<string> MoveAsync(
        [Description("Source file or directory path.")] string source,
        [Description("Destination file or directory path.")] string destination,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(source))
            return McpResponse.Error("invalid_parameter", "source path cannot be empty.", sw.ElapsedMilliseconds);
        if (string.IsNullOrWhiteSpace(destination))
            return McpResponse.Error("invalid_parameter", "destination path cannot be empty.", sw.ElapsedMilliseconds);
        var exists = await harness.FileSystem.ExistsAsync(source, ct);
        if (!exists)
            return McpResponse.Error("file_not_found", $"Source not found: '{source}'", sw.ElapsedMilliseconds);
        await harness.FileSystem.MoveAsync(source, destination, ct);
        ActionLog.Record("file_move", $"src={source}, dst={destination}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Moved '{source}' to '{destination}'.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_create_directory"), Description("Create a directory, including any missing parent directories.")]
    public async Task<string> CreateDirectoryAsync(
        [Description("Directory path to create.")] string path,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        await harness.FileSystem.CreateDirectoryAsync(path, ct);
        ActionLog.Record("file_create_directory", $"path={path}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Created directory: {path}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "file_delete"), Description("Delete a file or directory.")]
    public async Task<string> DeleteAsync(
        [Description("File or directory path to delete.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        var existed = await harness.FileSystem.ExistsAsync(path, ct);
        await harness.FileSystem.DeleteAsync(path, ct);
        ActionLog.Record("file_delete", $"path={path}", sw.ElapsedMilliseconds, existed);
        return existed
            ? McpResponse.Confirm($"Deleted '{path}'.", sw.ElapsedMilliseconds)
            : McpResponse.Confirm($"'{path}' did not exist (no action taken).", sw.ElapsedMilliseconds);
    }
}
