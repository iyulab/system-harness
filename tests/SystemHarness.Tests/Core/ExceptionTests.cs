namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class ExceptionTests
{
    [Fact]
    public void HarnessException_MessageOnly()
    {
        var ex = new HarnessException("test error");
        Assert.Equal("test error", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void HarnessException_WithInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new HarnessException("outer", inner);

        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void HarnessException_InheritsFromException()
    {
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(HarnessException)));
    }

    [Fact]
    public void CommandPolicyException_InheritsFromHarnessException()
    {
        var ex = new CommandPolicyException("blocked");
        Assert.IsAssignableFrom<HarnessException>(ex);
        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void CommandPolicyException_BlockedCommand_Property()
    {
        var ex = new CommandPolicyException("blocked") { BlockedCommand = "rm -rf /" };

        Assert.Equal("blocked", ex.Message);
        Assert.Equal("rm -rf /", ex.BlockedCommand);
    }

    [Fact]
    public void CommandPolicyException_BlockedCommand_DefaultNull()
    {
        var ex = new CommandPolicyException("no command");
        Assert.Null(ex.BlockedCommand);
    }

    [Fact]
    public void CommandPolicyException_IsSealed()
    {
        Assert.True(typeof(CommandPolicyException).IsSealed);
    }
}
