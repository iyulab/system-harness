namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class FileSystemModelTests
{
    [Fact]
    public void FileEntry_RequiredProperties()
    {
        var entry = new FileEntry { Path = @"C:\temp\file.txt", Name = "file.txt" };
        Assert.Equal(@"C:\temp\file.txt", entry.Path);
        Assert.Equal("file.txt", entry.Name);
        Assert.False(entry.IsDirectory);
        Assert.Equal(0, entry.Size);
        Assert.Null(entry.LastModified);
    }

    [Fact]
    public void FileEntry_File()
    {
        var modified = DateTimeOffset.UtcNow;
        var entry = new FileEntry
        {
            Path = @"C:\data\report.pdf",
            Name = "report.pdf",
            IsDirectory = false,
            Size = 1_048_576,
            LastModified = modified,
        };

        Assert.Equal(1_048_576, entry.Size);
        Assert.Equal(modified, entry.LastModified);
        Assert.False(entry.IsDirectory);
    }

    [Fact]
    public void FileEntry_Directory()
    {
        var entry = new FileEntry
        {
            Path = @"C:\data\subdir",
            Name = "subdir",
            IsDirectory = true,
            Size = 0,
        };

        Assert.True(entry.IsDirectory);
        Assert.Equal(0, entry.Size);
    }

    [Fact]
    public void FileEntry_IsSealed()
    {
        Assert.True(typeof(FileEntry).IsSealed);
    }

    [Fact]
    public void FileEntry_LargeFileSize()
    {
        var entry = new FileEntry
        {
            Path = @"C:\big\video.mp4",
            Name = "video.mp4",
            Size = 10_737_418_240L, // 10 GB
        };

        Assert.Equal(10_737_418_240L, entry.Size);
    }

    [Fact]
    public void FileEntry_UnixStylePath()
    {
        var entry = new FileEntry
        {
            Path = "/home/user/file.txt",
            Name = "file.txt",
        };

        Assert.Equal("/home/user/file.txt", entry.Path);
    }
}
