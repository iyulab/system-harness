namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IFileSystem"/> using System.IO.
/// </summary>
public sealed class WindowsFileSystem : IFileSystem
{
    public async Task<string> ReadAsync(string path, CancellationToken ct = default)
    {
        return await File.ReadAllTextAsync(path, ct);
    }

    public async Task<byte[]> ReadBytesAsync(string path, CancellationToken ct = default)
    {
        return await File.ReadAllBytesAsync(path, ct);
    }

    public async Task WriteAsync(string path, string content, CancellationToken ct = default)
    {
        EnsureDirectoryExists(path);
        await File.WriteAllTextAsync(path, content, ct);
    }

    public async Task WriteBytesAsync(string path, byte[] data, CancellationToken ct = default)
    {
        EnsureDirectoryExists(path);
        await File.WriteAllBytesAsync(path, data, ct);
    }

    public Task CopyAsync(string source, string destination, CancellationToken ct = default)
    {
        EnsureDirectoryExists(destination);
        File.Copy(source, destination, overwrite: true);
        return Task.CompletedTask;
    }

    public Task MoveAsync(string source, string destination, CancellationToken ct = default)
    {
        EnsureDirectoryExists(destination);
        File.Move(source, destination, overwrite: true);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(path) || Directory.Exists(path));
    }

    public Task<IReadOnlyList<FileEntry>> ListAsync(string path, string? pattern = null, CancellationToken ct = default)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
        {
            throw new HarnessException($"Directory not found: {path}");
        }

        var searchPattern = pattern ?? "*";
        var entries = new List<FileEntry>();

        foreach (var d in dir.EnumerateDirectories(searchPattern))
        {
            entries.Add(new FileEntry
            {
                Path = d.FullName,
                Name = d.Name,
                IsDirectory = true,
                LastModified = d.LastWriteTimeUtc,
            });
        }

        foreach (var f in dir.EnumerateFiles(searchPattern))
        {
            entries.Add(new FileEntry
            {
                Path = f.FullName,
                Name = f.Name,
                IsDirectory = false,
                Size = f.Length,
                LastModified = f.LastWriteTimeUtc,
            });
        }

        return Task.FromResult<IReadOnlyList<FileEntry>>(entries);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct = default)
    {
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task<FileMetadata> GetInfoAsync(string path, CancellationToken ct = default)
    {
        if (Directory.Exists(path))
        {
            var di = new DirectoryInfo(path);
            return Task.FromResult(new FileMetadata
            {
                Path = di.FullName,
                Name = di.Name,
                IsDirectory = true,
                IsReadOnly = di.Attributes.HasFlag(FileAttributes.ReadOnly),
                IsHidden = di.Attributes.HasFlag(FileAttributes.Hidden),
                CreatedTime = di.CreationTimeUtc,
                LastModifiedTime = di.LastWriteTimeUtc,
                LastAccessTime = di.LastAccessTimeUtc,
            });
        }

        if (File.Exists(path))
        {
            var fi = new FileInfo(path);
            return Task.FromResult(new FileMetadata
            {
                Path = fi.FullName,
                Name = fi.Name,
                Extension = fi.Extension,
                IsDirectory = false,
                IsReadOnly = fi.IsReadOnly,
                IsHidden = fi.Attributes.HasFlag(FileAttributes.Hidden),
                Size = fi.Length,
                CreatedTime = fi.CreationTimeUtc,
                LastModifiedTime = fi.LastWriteTimeUtc,
                LastAccessTime = fi.LastAccessTimeUtc,
            });
        }

        throw new HarnessException($"File or directory not found: {path}");
    }

    public Task<IReadOnlyList<FileEntry>> SearchAsync(string path, string pattern, int maxResults = 100, CancellationToken ct = default)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
            throw new HarnessException($"Directory not found: {path}");

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        };

        var entries = dir.EnumerateFileSystemInfos(pattern, options)
            .Take(maxResults)
            .Select(fsi => new FileEntry
            {
                Path = fsi.FullName,
                Name = fsi.Name,
                IsDirectory = fsi is DirectoryInfo,
                Size = fsi is FileInfo fi ? fi.Length : 0,
                LastModified = fsi.LastWriteTimeUtc,
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<FileEntry>>(entries);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
