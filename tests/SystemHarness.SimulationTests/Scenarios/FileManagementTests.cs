namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests file management workflows using Shell + FileSystem combination.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Local")]
public class FileManagementTests : SimulationTestBase
{
    public FileManagementTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateDirectoryStructure_AndListFiles()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"sim_test_{Guid.NewGuid():N}");

        try
        {
            // Create directory structure
            await FileSystem.CreateDirectoryAsync(Path.Combine(baseDir, "sub1"));
            await FileSystem.CreateDirectoryAsync(Path.Combine(baseDir, "sub2"));

            // Create files
            await FileSystem.WriteAsync(Path.Combine(baseDir, "root.txt"), "root file");
            await FileSystem.WriteAsync(Path.Combine(baseDir, "sub1", "file1.txt"), "file 1 content");
            await FileSystem.WriteAsync(Path.Combine(baseDir, "sub2", "file2.txt"), "file 2 content");

            // List and verify
            var entries = await FileSystem.ListAsync(baseDir);
            Assert.True(entries.Count >= 3); // root.txt, sub1/, sub2/

            var files = entries.Where(e => !e.IsDirectory).ToList();
            var dirs = entries.Where(e => e.IsDirectory).ToList();

            Assert.Contains(files, f => f.Name == "root.txt");
            Assert.Contains(dirs, d => d.Name == "sub1");
            Assert.Contains(dirs, d => d.Name == "sub2");
        }
        finally
        {
            try { Directory.Delete(baseDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CopyFiles_AndVerifyContent()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"sim_test_{Guid.NewGuid():N}");
        var srcFile = Path.Combine(baseDir, "source.txt");
        var dstFile = Path.Combine(baseDir, "destination.txt");

        try
        {
            await FileSystem.CreateDirectoryAsync(baseDir);
            await FileSystem.WriteAsync(srcFile, "original content");

            await FileSystem.CopyAsync(srcFile, dstFile);

            var srcContent = await FileSystem.ReadAsync(srcFile);
            var dstContent = await FileSystem.ReadAsync(dstFile);

            Assert.Equal(srcContent, dstContent);
        }
        finally
        {
            try { Directory.Delete(baseDir, true); } catch { }
        }
    }

    [Fact]
    public async Task BulkFileOperations()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"sim_test_{Guid.NewGuid():N}");

        try
        {
            await FileSystem.CreateDirectoryAsync(baseDir);

            // Create 20 files
            for (var i = 0; i < 20; i++)
            {
                await FileSystem.WriteAsync(
                    Path.Combine(baseDir, $"file_{i:D3}.txt"),
                    $"Content of file {i}");
            }

            // List and verify count
            var entries = await FileSystem.ListAsync(baseDir);
            Assert.Equal(20, entries.Count);

            // Read a few and verify content
            var content5 = await FileSystem.ReadAsync(Path.Combine(baseDir, "file_005.txt"));
            Assert.Equal("Content of file 5", content5);

            // Delete some
            for (var i = 0; i < 5; i++)
            {
                await FileSystem.DeleteAsync(Path.Combine(baseDir, $"file_{i:D3}.txt"));
            }

            entries = await FileSystem.ListAsync(baseDir);
            Assert.Equal(15, entries.Count);
        }
        finally
        {
            try { Directory.Delete(baseDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ShellAndFileSystem_CombinedWorkflow()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"sim_test_{Guid.NewGuid():N}");

        try
        {
            await FileSystem.CreateDirectoryAsync(baseDir);
            await FileSystem.WriteAsync(Path.Combine(baseDir, "test.txt"), "hello world");

            // Use shell to verify the file exists
            var result = await Shell.RunAsync("cmd.exe", $"/c dir \"{baseDir}\"");
            Assert.True(result.Success);
            Assert.Contains("test.txt", result.StdOut);
        }
        finally
        {
            try { Directory.Delete(baseDir, true); } catch { }
        }
    }
}
