namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests file management workflows using Shell + FileSystem combination.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Integration")]
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
            await FileSystem.CreateDirectoryAsync(Path.Combine(baseDir, "sub1"), TestContext.Current.CancellationToken);
            await FileSystem.CreateDirectoryAsync(Path.Combine(baseDir, "sub2"), TestContext.Current.CancellationToken);

            // Create files
            await FileSystem.WriteAsync(Path.Combine(baseDir, "root.txt"), "root file", TestContext.Current.CancellationToken);
            await FileSystem.WriteAsync(Path.Combine(baseDir, "sub1", "file1.txt"), "file 1 content", TestContext.Current.CancellationToken);
            await FileSystem.WriteAsync(Path.Combine(baseDir, "sub2", "file2.txt"), "file 2 content", TestContext.Current.CancellationToken);

            // List and verify
            var entries = await FileSystem.ListAsync(baseDir, ct: TestContext.Current.CancellationToken);
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
            await FileSystem.CreateDirectoryAsync(baseDir, TestContext.Current.CancellationToken);
            await FileSystem.WriteAsync(srcFile, "original content", TestContext.Current.CancellationToken);

            await FileSystem.CopyAsync(srcFile, dstFile, TestContext.Current.CancellationToken);

            var srcContent = await FileSystem.ReadAsync(srcFile, TestContext.Current.CancellationToken);
            var dstContent = await FileSystem.ReadAsync(dstFile, TestContext.Current.CancellationToken);

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
            await FileSystem.CreateDirectoryAsync(baseDir, TestContext.Current.CancellationToken);

            // Create 20 files
            for (var i = 0; i < 20; i++)
            {
                await FileSystem.WriteAsync(Path.Combine(baseDir, $"file_{i:D3}.txt"), $"Content of file {i}", TestContext.Current.CancellationToken);
            }

            // List and verify count
            var entries = await FileSystem.ListAsync(baseDir, ct: TestContext.Current.CancellationToken);
            Assert.Equal(20, entries.Count);

            // Read a few and verify content
            var content5 = await FileSystem.ReadAsync(Path.Combine(baseDir, "file_005.txt"), TestContext.Current.CancellationToken);
            Assert.Equal("Content of file 5", content5);

            // Delete some
            for (var i = 0; i < 5; i++)
            {
                await FileSystem.DeleteAsync(Path.Combine(baseDir, $"file_{i:D3}.txt"), TestContext.Current.CancellationToken);
            }

            entries = await FileSystem.ListAsync(baseDir, ct: TestContext.Current.CancellationToken);
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
            await FileSystem.CreateDirectoryAsync(baseDir, TestContext.Current.CancellationToken);
            await FileSystem.WriteAsync(Path.Combine(baseDir, "test.txt"), "hello world", TestContext.Current.CancellationToken);

            // Use shell to verify the file exists
            var result = await Shell.RunAsync("cmd.exe", $"/c dir \"{baseDir}\"", ct: TestContext.Current.CancellationToken);
            Assert.True(result.Success);
            Assert.Contains("test.txt", result.StdOut);
        }
        finally
        {
            try { Directory.Delete(baseDir, true); } catch { }
        }
    }
}
