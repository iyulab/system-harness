using System.Text.Json;
using SystemHarness.Mcp;

namespace SystemHarness.Tests.Mcp;

[Trait("Category", "CI")]
public class McpResponseTests
{
    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Ok_ReturnsStructuredEnvelope()
    {
        var json = McpResponse.Ok(new { name = "test", value = 42 }, ms: 5);
        var root = Parse(json);

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("test", root.GetProperty("data").GetProperty("name").GetString());
        Assert.Equal(42, root.GetProperty("data").GetProperty("value").GetInt32());
        Assert.Equal(5, root.GetProperty("meta").GetProperty("ms").GetInt64());
        Assert.NotNull(root.GetProperty("meta").GetProperty("ts").GetString());
    }

    [Fact]
    public void Items_ReturnsItemsAndCount()
    {
        var items = new[] { new { id = 1 }, new { id = 2 }, new { id = 3 } };
        var json = McpResponse.Items(items);
        var data = Parse(json).GetProperty("data");

        Assert.Equal(3, data.GetProperty("count").GetInt32());
        Assert.Equal(3, data.GetProperty("items").GetArrayLength());
        Assert.Equal(2, data.GetProperty("items")[1].GetProperty("id").GetInt32());
    }

    [Fact]
    public void Content_ReturnsContentAndFormat()
    {
        var json = McpResponse.Content("# Hello", "markdown");
        var data = Parse(json).GetProperty("data");

        Assert.Equal("# Hello", data.GetProperty("content").GetString());
        Assert.Equal("markdown", data.GetProperty("format").GetString());
    }

    [Fact]
    public void Content_DefaultsToTextFormat()
    {
        var json = McpResponse.Content("plain text");
        var data = Parse(json).GetProperty("data");

        Assert.Equal("text", data.GetProperty("format").GetString());
    }

    [Fact]
    public void Confirm_ReturnsMessage()
    {
        var json = McpResponse.Confirm("File saved.", ms: 10);
        var root = Parse(json);

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("File saved.", root.GetProperty("data").GetProperty("message").GetString());
    }

    [Fact]
    public void Check_ReturnsBooleanResult()
    {
        var json = McpResponse.Check(true, "Process is running.");
        var data = Parse(json).GetProperty("data");

        Assert.True(data.GetProperty("result").GetBoolean());
        Assert.Equal("Process is running.", data.GetProperty("detail").GetString());
    }

    [Fact]
    public void Check_NullDetailOmitted()
    {
        var json = McpResponse.Check(false);
        var data = Parse(json).GetProperty("data");

        Assert.False(data.GetProperty("result").GetBoolean());
        // null detail should be omitted
        Assert.False(data.TryGetProperty("detail", out var detail) &&
                     detail.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public void Error_ReturnsErrorEnvelope()
    {
        var json = McpResponse.Error("file_not_found", "No such file: test.docx");
        var root = Parse(json);

        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal("file_not_found", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("No such file: test.docx", root.GetProperty("error").GetProperty("message").GetString());
        // data should be absent on error
        Assert.False(root.TryGetProperty("data", out var d) && d.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public void Ok_NullMs_OmitsMs()
    {
        var json = McpResponse.Ok(new { x = 1 });
        var meta = Parse(json).GetProperty("meta");

        Assert.NotNull(meta.GetProperty("ts").GetString());
        // ms should be omitted when null
        Assert.False(meta.TryGetProperty("ms", out var ms) && ms.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public void Ok_CamelCasePropertyNames()
    {
        var json = McpResponse.Ok(new { SomeName = "value" });
        var data = Parse(json).GetProperty("data");

        // Property should be camelCase
        Assert.True(data.TryGetProperty("someName", out _));
    }

    [Fact]
    public void Ok_EmojiSurrogatePairs_RoundTripCorrectly()
    {
        var original = "Hello 😀🎉🚀 World";
        var json = McpResponse.Ok(new { text = original });
        var data = Parse(json).GetProperty("data");
        var text = data.GetProperty("text").GetString();
        Assert.Equal(original, text);
    }

    [Fact]
    public void Ok_SupplementaryPlaneCharacters_RoundTripCorrectly()
    {
        var original = "𝄞 𝐀𝐁𝐂";
        var json = McpResponse.Ok(new { text = original });
        var data = Parse(json).GetProperty("data");
        var text = data.GetProperty("text").GetString();
        Assert.Equal(original, text);
    }

    [Fact]
    public void Ok_MixedUnicodeScripts_AllPreserved()
    {
        var original = "한글 日本語 العربية café";
        var json = McpResponse.Ok(new { text = original });
        var data = Parse(json).GetProperty("data");
        var text = data.GetProperty("text").GetString();
        Assert.Equal(original, text);
    }

    // --- Edge cases (cycle 239) ---

    [Fact]
    public void Items_EmptyList_ReturnsZeroCount()
    {
        var json = McpResponse.Items(Array.Empty<object>());
        var data = Parse(json).GetProperty("data");

        Assert.Equal(0, data.GetProperty("count").GetInt32());
        Assert.Equal(0, data.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void Ok_Timestamp_IsIso8601()
    {
        var json = McpResponse.Ok(new { x = 1 });
        var ts = Parse(json).GetProperty("meta").GetProperty("ts").GetString()!;

        // ISO 8601 format: yyyy-MM-ddTHH:mm:ss.fffffffZ
        Assert.True(DateTimeOffset.TryParse(ts, out var parsed));
        Assert.Equal(DateTimeKind.Utc, parsed.DateTime.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : parsed.DateTime.Kind);
    }

    [Fact]
    public void Error_SpecialCharactersInMessage()
    {
        var json = McpResponse.Error("parse_error", "Unexpected token '<' at line 1, column 1");
        var error = Parse(json).GetProperty("error");

        Assert.Equal("Unexpected token '<' at line 1, column 1", error.GetProperty("message").GetString());
    }

    [Fact]
    public void Ok_NestedObjects_Serialized()
    {
        var json = McpResponse.Ok(new
        {
            window = new { title = "Test", bounds = new { x = 10, y = 20, width = 800, height = 600 } }
        });

        var data = Parse(json).GetProperty("data");
        var bounds = data.GetProperty("window").GetProperty("bounds");
        Assert.Equal(800, bounds.GetProperty("width").GetInt32());
    }

    [Fact]
    public void Check_False_WithNoDetail()
    {
        var json = McpResponse.Check(false);
        var root = Parse(json);
        Assert.True(root.GetProperty("ok").GetBoolean()); // Check returns ok:true (it's a response, not an error)
        Assert.False(root.GetProperty("data").GetProperty("result").GetBoolean());
    }
}
