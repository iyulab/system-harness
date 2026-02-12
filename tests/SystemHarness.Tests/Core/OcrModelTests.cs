namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class OcrModelTests
{
    // --- OcrWord ---

    [Fact]
    public void OcrWord_RequiredProperties()
    {
        var word = new OcrWord { Text = "hello" };
        Assert.Equal("hello", word.Text);
        Assert.Null(word.Confidence);
        Assert.Equal(default, word.BoundingRect);
    }

    [Fact]
    public void OcrWord_WithConfidence()
    {
        var word = new OcrWord
        {
            Text = "world",
            Confidence = 0.95,
            BoundingRect = new Rectangle(10, 20, 50, 15),
        };

        Assert.Equal("world", word.Text);
        Assert.Equal(0.95, word.Confidence);
        Assert.Equal(new Rectangle(10, 20, 50, 15), word.BoundingRect);
    }

    // --- OcrLine ---

    [Fact]
    public void OcrLine_RequiredProperties()
    {
        var line = new OcrLine
        {
            Text = "hello world",
            Words = [new OcrWord { Text = "hello" }, new OcrWord { Text = "world" }],
        };

        Assert.Equal("hello world", line.Text);
        Assert.Equal(2, line.Words.Count);
        Assert.Equal(default, line.BoundingRect);
    }

    [Fact]
    public void OcrLine_WithBoundingRect()
    {
        var line = new OcrLine
        {
            Text = "test line",
            Words = [],
            BoundingRect = new Rectangle(0, 0, 200, 20),
        };

        Assert.Equal(new Rectangle(0, 0, 200, 20), line.BoundingRect);
    }

    // --- OcrResult ---

    [Fact]
    public void OcrResult_RequiredProperties()
    {
        var result = new OcrResult
        {
            Text = "OCR text",
            Lines = [new OcrLine { Text = "OCR text", Words = [] }],
        };

        Assert.Equal("OCR text", result.Text);
        Assert.Single(result.Lines);
        Assert.Null(result.Language);
    }

    [Fact]
    public void OcrResult_WithLanguage()
    {
        var result = new OcrResult
        {
            Text = "한글 텍스트",
            Lines = [new OcrLine { Text = "한글 텍스트", Words = [] }],
            Language = "ko-KR",
        };

        Assert.Equal("ko-KR", result.Language);
    }

    [Fact]
    public void OcrResult_MultiLine()
    {
        var result = new OcrResult
        {
            Text = "line one\nline two",
            Lines =
            [
                new OcrLine { Text = "line one", Words = [new OcrWord { Text = "line" }, new OcrWord { Text = "one" }] },
                new OcrLine { Text = "line two", Words = [new OcrWord { Text = "line" }, new OcrWord { Text = "two" }] },
            ],
        };

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(2, result.Lines[0].Words.Count);
        Assert.Equal("one", result.Lines[0].Words[1].Text);
    }

    // --- OcrOptions ---

    [Fact]
    public void OcrOptions_DefaultLanguage()
    {
        var options = new OcrOptions();
        Assert.Equal("en-US", options.Language);
    }

    [Fact]
    public void OcrOptions_CustomLanguage()
    {
        var options = new OcrOptions { Language = "ja-JP" };
        Assert.Equal("ja-JP", options.Language);
    }
}
