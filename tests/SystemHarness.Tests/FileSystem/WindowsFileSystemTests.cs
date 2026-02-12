using SystemHarness.Windows;

namespace SystemHarness.Tests.FileSystem;

[Trait("Category", "CI")]
public class WindowsFileSystemTests : IDisposable
{
    private readonly WindowsFileSystem _fs = new();
    private readonly string _tempDir;

    public WindowsFileSystemTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SystemHarness.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_AndReadAsync_RoundTrip()
    {
        var path = Path.Combine(_tempDir, "test.txt");

        await _fs.WriteAsync(path, "hello world");
        var content = await _fs.ReadAsync(path);

        Assert.Equal("hello world", content);
    }

    [Fact]
    public async Task WriteBytesAsync_AndReadBytesAsync_RoundTrip()
    {
        var path = Path.Combine(_tempDir, "test.bin");
        var data = new byte[] { 1, 2, 3, 4, 5 };

        await _fs.WriteBytesAsync(path, data);
        var result = await _fs.ReadBytesAsync(path);

        Assert.Equal(data, result);
    }

    [Fact]
    public async Task ExistsAsync_ExistingFile_ReturnsTrue()
    {
        var path = Path.Combine(_tempDir, "exists.txt");
        await _fs.WriteAsync(path, "content");

        Assert.True(await _fs.ExistsAsync(path));
    }

    [Fact]
    public async Task ExistsAsync_NonExistentFile_ReturnsFalse()
    {
        Assert.False(await _fs.ExistsAsync(Path.Combine(_tempDir, "nope.txt")));
    }

    [Fact]
    public async Task ExistsAsync_Directory_ReturnsTrue()
    {
        Assert.True(await _fs.ExistsAsync(_tempDir));
    }

    [Fact]
    public async Task DeleteAsync_File_RemovesIt()
    {
        var path = Path.Combine(_tempDir, "delete.txt");
        await _fs.WriteAsync(path, "bye");

        await _fs.DeleteAsync(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteAsync_Directory_RemovesRecursively()
    {
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        await _fs.WriteAsync(Path.Combine(subDir, "file.txt"), "content");

        await _fs.DeleteAsync(subDir);

        Assert.False(Directory.Exists(subDir));
    }

    [Fact]
    public async Task CopyAsync_CreatesNewFile()
    {
        var src = Path.Combine(_tempDir, "source.txt");
        var dst = Path.Combine(_tempDir, "copy.txt");
        await _fs.WriteAsync(src, "original");

        await _fs.CopyAsync(src, dst);

        Assert.True(File.Exists(dst));
        Assert.Equal("original", await _fs.ReadAsync(dst));
        Assert.True(File.Exists(src)); // Source still exists
    }

    [Fact]
    public async Task MoveAsync_RelocatesFile()
    {
        var src = Path.Combine(_tempDir, "move-src.txt");
        var dst = Path.Combine(_tempDir, "move-dst.txt");
        await _fs.WriteAsync(src, "moving");

        await _fs.MoveAsync(src, dst);

        Assert.False(File.Exists(src));
        Assert.True(File.Exists(dst));
        Assert.Equal("moving", await _fs.ReadAsync(dst));
    }

    [Fact]
    public async Task ListAsync_ReturnsFilesAndDirectories()
    {
        await _fs.WriteAsync(Path.Combine(_tempDir, "a.txt"), "a");
        await _fs.WriteAsync(Path.Combine(_tempDir, "b.txt"), "b");
        await _fs.CreateDirectoryAsync(Path.Combine(_tempDir, "subdir"));

        var entries = await _fs.ListAsync(_tempDir);

        Assert.True(entries.Count >= 3);
        Assert.Contains(entries, e => e.Name == "a.txt" && !e.IsDirectory);
        Assert.Contains(entries, e => e.Name == "b.txt" && !e.IsDirectory);
        Assert.Contains(entries, e => e.Name == "subdir" && e.IsDirectory);
    }

    [Fact]
    public async Task ListAsync_WithPattern_FiltersResults()
    {
        await _fs.WriteAsync(Path.Combine(_tempDir, "data.csv"), "csv");
        await _fs.WriteAsync(Path.Combine(_tempDir, "data.txt"), "txt");

        var entries = await _fs.ListAsync(_tempDir, "*.csv");

        Assert.Single(entries);
        Assert.Equal("data.csv", entries[0].Name);
    }

    [Fact]
    public async Task CreateDirectoryAsync_CreatesNestedDirs()
    {
        var nested = Path.Combine(_tempDir, "a", "b", "c");

        await _fs.CreateDirectoryAsync(nested);

        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public async Task WriteAsync_CreatesParentDirectories()
    {
        var path = Path.Combine(_tempDir, "nested", "dir", "file.txt");

        await _fs.WriteAsync(path, "auto-dir");

        Assert.Equal("auto-dir", await _fs.ReadAsync(path));
    }

    [Fact]
    public async Task ListAsync_NonExistentDir_ThrowsHarnessException()
    {
        await Assert.ThrowsAsync<HarnessException>(() =>
            _fs.ListAsync(Path.Combine(_tempDir, "nonexistent")));
    }

    [Fact]
    public async Task FileEntry_HasSizeAndLastModified()
    {
        var path = Path.Combine(_tempDir, "sized.txt");
        await _fs.WriteAsync(path, "12345");

        var entries = await _fs.ListAsync(_tempDir, "sized.txt");

        Assert.Single(entries);
        Assert.True(entries[0].Size > 0);
        Assert.NotNull(entries[0].LastModified);
    }

    [Fact]
    public async Task WriteAsync_EmojiContent_PreservesSurrogatePairs()
    {
        var path = Path.Combine(_tempDir, "emoji.txt");
        var content = "Hello 😀🎉 World 🚀";

        await _fs.WriteAsync(path, content);
        var result = await _fs.ReadAsync(path);

        Assert.Equal(content, result);
    }

    [Fact]
    public async Task WriteAsync_CjkAndMixedUnicode_Preserves()
    {
        var path = Path.Combine(_tempDir, "unicode.txt");
        var content = "한글 日本語 中文 العربية café résumé";

        await _fs.WriteAsync(path, content);
        var result = await _fs.ReadAsync(path);

        Assert.Equal(content, result);
    }

    [Fact]
    public async Task WriteAsync_SupplementaryPlaneCharacters_Preserves()
    {
        var path = Path.Combine(_tempDir, "supplementary.txt");
        // Musical symbols (U+1D11E), math symbols (U+1D400) — supplementary plane
        var content = "𝄞 𝐀𝐁𝐂 🏳️‍🌈";

        await _fs.WriteAsync(path, content);
        var result = await _fs.ReadAsync(path);

        Assert.Equal(content, result);
    }
}
