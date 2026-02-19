using System.Text.Json;
using SystemHarness.Mcp.Dispatch;

namespace SystemHarness.Tests.Mcp;

[Trait("Category", "CI")]
public class CommandRegistryTests
{
    private static CommandDescriptor MakeCommand(
        string name,
        string category,
        bool isMutation = false,
        IReadOnlyList<ParamDescriptor>? parameters = null) => new()
    {
        Name = name,
        Category = category,
        Description = $"Description for {name}",
        IsMutation = isMutation,
        Parameters = parameters ?? [],
        Handler = (_, _) => Task.FromResult("ok")
    };

    private static ParamDescriptor MakeParam(
        string name,
        string type = "string",
        bool required = true,
        string? defaultValue = null) => new()
    {
        Name = name,
        TypeName = type,
        Description = $"Param {name}",
        IsRequired = required,
        DefaultValue = defaultValue
    };

    // --- Count / Register ---

    [Fact]
    public void NewRegistry_HasZeroCount()
    {
        var registry = new CommandRegistry();
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Register_SingleCommand_IncrementsCount()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.list", "File"));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Register_MultipleCommands_TracksCount()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.list", "File"));
        registry.Register(MakeCommand("file.read", "File"));
        registry.Register(MakeCommand("mouse.click", "Input"));
        Assert.Equal(3, registry.Count);
    }

    [Fact]
    public void Register_DuplicateName_OverwritesPrevious()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.list", "File"));
        registry.Register(MakeCommand("file.list", "FileSystem", isMutation: true));

        Assert.Equal(1, registry.Count);
        var cmd = registry.Find("file.list");
        Assert.NotNull(cmd);
        Assert.Equal("FileSystem", cmd.Category);
        Assert.True(cmd.IsMutation);
    }

    // --- Find ---

    [Fact]
    public void Find_ExistingCommand_ReturnsDescriptor()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.read", "File"));

        var cmd = registry.Find("file.read");

        Assert.NotNull(cmd);
        Assert.Equal("file.read", cmd.Name);
        Assert.Equal("File", cmd.Category);
    }

    [Fact]
    public void Find_CaseInsensitive_ReturnsMatch()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("File.Read", "File"));

        Assert.NotNull(registry.Find("file.read"));
        Assert.NotNull(registry.Find("FILE.READ"));
    }

    [Fact]
    public void Find_NonExistent_ReturnsNull()
    {
        var registry = new CommandRegistry();
        Assert.Null(registry.Find("nonexistent"));
    }

    // --- GetCategories ---

    [Fact]
    public void GetCategories_EmptyRegistry_ReturnsEmpty()
    {
        var registry = new CommandRegistry();
        Assert.Empty(registry.GetCategories());
    }

    [Fact]
    public void GetCategories_ReturnsDistinctSorted()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("z.cmd", "Zebra"));
        registry.Register(MakeCommand("a.cmd", "Alpha"));
        registry.Register(MakeCommand("m.cmd", "Middle"));

        var cats = registry.GetCategories();

        Assert.Equal(3, cats.Count);
        Assert.Equal("Alpha", cats[0]);
        Assert.Equal("Middle", cats[1]);
        Assert.Equal("Zebra", cats[2]);
    }

    [Fact]
    public void GetCategories_SameCategory_NotDuplicated()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.read", "File"));
        registry.Register(MakeCommand("file.write", "File"));

        Assert.Single(registry.GetCategories());
    }

    // --- GetByCategory ---

    [Fact]
    public void GetByCategory_ExistingCategory_ReturnsCommands()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.read", "File"));
        registry.Register(MakeCommand("file.write", "File"));
        registry.Register(MakeCommand("mouse.click", "Input"));

        var fileCmds = registry.GetByCategory("File");

        Assert.Equal(2, fileCmds.Count);
    }

    [Fact]
    public void GetByCategory_CaseInsensitive_Works()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.read", "File"));

        Assert.Single(registry.GetByCategory("file"));
        Assert.Single(registry.GetByCategory("FILE"));
    }

    [Fact]
    public void GetByCategory_Unknown_ReturnsEmpty()
    {
        var registry = new CommandRegistry();
        Assert.Empty(registry.GetByCategory("nonexistent"));
    }

    // --- FormatCategoryList ---

    [Fact]
    public void FormatCategoryList_IncludesCountsAndMutationInfo()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.read", "File"));
        registry.Register(MakeCommand("file.write", "File", isMutation: true));

        var output = registry.FormatCategoryList();

        // Should be a JSON envelope (McpResponse.Content)
        var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        var content = doc.RootElement.GetProperty("data").GetProperty("content").GetString()!;

        Assert.Contains("2 commands in 1 categories", content);
        Assert.Contains("File (2)", content);
        Assert.Contains("1 read, 1 mutation", content);
    }

    [Fact]
    public void FormatCategoryList_EmptyRegistry_ShowsZero()
    {
        var registry = new CommandRegistry();
        var output = registry.FormatCategoryList();

        var doc = JsonDocument.Parse(output);
        var content = doc.RootElement.GetProperty("data").GetProperty("content").GetString()!;

        Assert.Contains("0 commands in 0 categories", content);
    }

    // --- FormatCategory ---

    [Fact]
    public void FormatCategory_ExistingCategory_ListsCommands()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.read", "File"));
        registry.Register(MakeCommand("file.write", "File", isMutation: true));

        var output = registry.FormatCategory("File");

        var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        var content = doc.RootElement.GetProperty("data").GetProperty("content").GetString()!;

        Assert.Contains("[get] file.read", content);
        Assert.Contains("[do] file.write", content);
    }

    [Fact]
    public void FormatCategory_UnknownCategory_ReturnsError()
    {
        var registry = new CommandRegistry();
        var output = registry.FormatCategory("nonexistent");

        var doc = JsonDocument.Parse(output);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    // --- FormatCommand ---

    [Fact]
    public void FormatCommand_NoParams_ShowsNoParameters()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("system.info", "System"));

        var output = registry.FormatCommand("system.info");

        var doc = JsonDocument.Parse(output);
        var content = doc.RootElement.GetProperty("data").GetProperty("content").GetString()!;

        Assert.Contains("system.info [get]", content);
        Assert.Contains("No parameters.", content);
    }

    [Fact]
    public void FormatCommand_WithParams_ShowsParameterDetails()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.read", "File", parameters:
        [
            MakeParam("path", "string", required: true),
            MakeParam("encoding", "string", required: false, defaultValue: "utf-8")
        ]));

        var output = registry.FormatCommand("file.read");

        var doc = JsonDocument.Parse(output);
        var content = doc.RootElement.GetProperty("data").GetProperty("content").GetString()!;

        Assert.Contains("path (string, required)", content);
        Assert.Contains("encoding (string, optional, default=utf-8)", content);
    }

    [Fact]
    public void FormatCommand_MutationCommand_ShowsDo()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("file.delete", "File", isMutation: true));

        var output = registry.FormatCommand("file.delete");

        var doc = JsonDocument.Parse(output);
        var content = doc.RootElement.GetProperty("data").GetProperty("content").GetString()!;

        Assert.Contains("file.delete [do]", content);
        Assert.Contains("do(\"file.delete\")", content);
    }

    [Fact]
    public void FormatCommand_UnknownCommand_ReturnsError()
    {
        var registry = new CommandRegistry();
        var output = registry.FormatCommand("nonexistent");

        var doc = JsonDocument.Parse(output);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void FormatCommand_OptionalParamWithNullDefault_ShowsNull()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("test.cmd", "Test", parameters:
        [
            MakeParam("optional_param", "int", required: false, defaultValue: null)
        ]));

        var output = registry.FormatCommand("test.cmd");

        var doc = JsonDocument.Parse(output);
        var content = doc.RootElement.GetProperty("data").GetProperty("content").GetString()!;

        Assert.Contains("optional, default=null", content);
    }
}
