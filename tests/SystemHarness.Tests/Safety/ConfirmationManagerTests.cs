using SystemHarness.Mcp;

namespace SystemHarness.Tests.Safety;

[Trait("Category", "CI")]
public class ConfirmationManagerTests
{
    [Fact]
    public void Create_ReturnsRequestWithPendingStatus()
    {
        var request = ConfirmationManager.Create("delete_file", "Removing important file");

        Assert.NotEmpty(request.Id);
        Assert.Equal("delete_file", request.Action);
        Assert.Equal("Removing important file", request.Reason);
        Assert.Equal(ConfirmationStatus.Pending, request.Status);
        Assert.True(File.Exists(request.FilePath));
        Assert.Null(request.ResolvedAt);

        // Cleanup
        File.Delete(request.FilePath);
    }

    [Fact]
    public void Approve_ChangesStatusToApproved()
    {
        var request = ConfirmationManager.Create("format_disk", "Dangerous operation");
        var resolved = ConfirmationManager.Approve(request.Id);

        Assert.Equal(ConfirmationStatus.Approved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);

        // Verify file updated
        var fileContent = File.ReadAllText(resolved.FilePath);
        Assert.Contains("approved", fileContent);

        File.Delete(resolved.FilePath);
    }

    [Fact]
    public void Deny_ChangesStatusToDenied()
    {
        var request = ConfirmationManager.Create("drop_table", "Irreversible");
        var resolved = ConfirmationManager.Deny(request.Id);

        Assert.Equal(ConfirmationStatus.Denied, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);

        File.Delete(resolved.FilePath);
    }

    [Fact]
    public void Check_ReturnsPendingForNewRequest()
    {
        var request = ConfirmationManager.Create("test_action", "test_reason");
        var checked_ = ConfirmationManager.Check(request.Id);

        Assert.Equal(ConfirmationStatus.Pending, checked_.Status);

        File.Delete(request.FilePath);
    }

    [Fact]
    public void Check_InvalidId_ThrowsHarnessException()
    {
        Assert.Throws<HarnessException>(() =>
            ConfirmationManager.Check("nonexistent_id"));
    }

    [Fact]
    public void ListPending_ReturnsOnlyPendingRequests()
    {
        var r1 = ConfirmationManager.Create("action_1", "reason_1");
        var r2 = ConfirmationManager.Create("action_2", "reason_2");
        ConfirmationManager.Approve(r1.Id);

        var pending = ConfirmationManager.ListPending();

        Assert.Contains(pending, p => p.Id == r2.Id);
        Assert.DoesNotContain(pending, p => p.Id == r1.Id);

        File.Delete(r1.FilePath);
        File.Delete(r2.FilePath);
    }

    [Fact]
    public void Approve_InvalidId_ThrowsHarnessException()
    {
        Assert.Throws<HarnessException>(() =>
            ConfirmationManager.Approve("nonexistent"));
    }

    [Fact]
    public void Deny_InvalidId_ThrowsHarnessException()
    {
        Assert.Throws<HarnessException>(() =>
            ConfirmationManager.Deny("nonexistent"));
    }

    [Fact]
    public void Create_MultipleRequests_HaveUniqueIds()
    {
        var r1 = ConfirmationManager.Create("a", "r1");
        var r2 = ConfirmationManager.Create("b", "r2");
        var r3 = ConfirmationManager.Create("c", "r3");

        Assert.NotEqual(r1.Id, r2.Id);
        Assert.NotEqual(r2.Id, r3.Id);
        Assert.NotEqual(r1.Id, r3.Id);

        File.Delete(r1.FilePath);
        File.Delete(r2.FilePath);
        File.Delete(r3.FilePath);
    }

    [Fact]
    public void Create_CreatedAt_IsRecentUtc()
    {
        var before = DateTime.UtcNow;
        var request = ConfirmationManager.Create("timing_test", "timing");
        var after = DateTime.UtcNow;

        Assert.InRange(request.CreatedAt, before, after);

        File.Delete(request.FilePath);
    }

    [Fact]
    public void Create_FileContent_IsValidJson()
    {
        var request = ConfirmationManager.Create("json_test", "verify_json");
        var content = File.ReadAllText(request.FilePath);
        var doc = System.Text.Json.JsonDocument.Parse(content);

        Assert.Equal(request.Id, doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("json_test", doc.RootElement.GetProperty("action").GetString());
        Assert.Equal("pending", doc.RootElement.GetProperty("status").GetString());

        File.Delete(request.FilePath);
    }

    [Fact]
    public void Approve_ThenCheck_ReturnsApproved()
    {
        var request = ConfirmationManager.Create("approve_check", "verify");
        ConfirmationManager.Approve(request.Id);
        var checked_ = ConfirmationManager.Check(request.Id);

        Assert.Equal(ConfirmationStatus.Approved, checked_.Status);

        File.Delete(request.FilePath);
    }

    [Fact]
    public void Deny_ThenCheck_ReturnsDenied()
    {
        var request = ConfirmationManager.Create("deny_check", "verify");
        ConfirmationManager.Deny(request.Id);
        var checked_ = ConfirmationManager.Check(request.Id);

        Assert.Equal(ConfirmationStatus.Denied, checked_.Status);

        File.Delete(request.FilePath);
    }
}
