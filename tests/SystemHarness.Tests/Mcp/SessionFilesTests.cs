using SystemHarness.Mcp;

namespace SystemHarness.Tests.Mcp;

[Trait("Category", "CI")]
public class SessionFilesTests
{
    [Fact]
    public void NewPath_IsInsideTheSessionDirectory()
    {
        var path = SessionFiles.NewPath("screen", "png");

        Assert.True(SessionFiles.Contains(path));
        Assert.Equal(SessionFiles.Root, Path.GetDirectoryName(path));
        Assert.EndsWith(".png", path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"..\..\Windows\evil")]
    [InlineData("../../escape")]
    [InlineData(@"C:\abs")]
    public void NewPath_CallerLabelCannotLeaveTheDirectory(string label)
    {
        var path = SessionFiles.NewPath($"bookmark-{label}", "png");

        Assert.Equal(SessionFiles.Root, Path.GetDirectoryName(path));
    }

    [Fact]
    public void NewPath_TwoCallsInTheSameSecond_DoNotCollide()
    {
        Assert.NotEqual(SessionFiles.NewPath("observe", "png"), SessionFiles.NewPath("observe", "png"));
    }

    [Fact]
    public void Contains_OutsidePaths_AreNotProtected()
    {
        Assert.False(SessionFiles.Contains(Path.Combine(Path.GetTempPath(), "other.txt")));
        Assert.False(SessionFiles.Contains(SessionFiles.Root + @"-sibling\x.txt"));
        Assert.True(SessionFiles.Contains(Path.Combine(SessionFiles.Root, "sub", "..", "x.json")));
    }

    [Fact]
    public void ConfirmationRequests_AreWrittenToTheSessionDirectory()
    {
        var request = ConfirmationManager.Create("action", "reason");

        Assert.True(SessionFiles.Contains(request.FilePath));
        File.Delete(request.FilePath);
    }
}
