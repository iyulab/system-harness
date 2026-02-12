namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class TemplateMatchResultModelTests
{
    [Fact]
    public void RequiredProperties()
    {
        var result = new TemplateMatchResult
        {
            X = 100, Y = 200, Width = 50, Height = 30, Confidence = 0.95,
        };

        Assert.Equal(100, result.X);
        Assert.Equal(200, result.Y);
        Assert.Equal(50, result.Width);
        Assert.Equal(30, result.Height);
        Assert.Equal(0.95, result.Confidence);
    }

    [Fact]
    public void CenterX_ComputedFromXAndWidth()
    {
        var result = new TemplateMatchResult
        {
            X = 100, Y = 200, Width = 50, Height = 30, Confidence = 0.9,
        };

        Assert.Equal(125, result.CenterX); // 100 + 50/2
    }

    [Fact]
    public void CenterY_ComputedFromYAndHeight()
    {
        var result = new TemplateMatchResult
        {
            X = 100, Y = 200, Width = 50, Height = 30, Confidence = 0.9,
        };

        Assert.Equal(215, result.CenterY); // 200 + 30/2
    }

    [Fact]
    public void RecordEquality()
    {
        var a = new TemplateMatchResult { X = 10, Y = 20, Width = 30, Height = 40, Confidence = 0.85 };
        var b = new TemplateMatchResult { X = 10, Y = 20, Width = 30, Height = 40, Confidence = 0.85 };
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordInequality()
    {
        var a = new TemplateMatchResult { X = 10, Y = 20, Width = 30, Height = 40, Confidence = 0.85 };
        var b = new TemplateMatchResult { X = 10, Y = 20, Width = 30, Height = 40, Confidence = 0.90 };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void IsSealed()
    {
        Assert.True(typeof(TemplateMatchResult).IsSealed);
    }

    [Fact]
    public void ZeroSize_CenterEqualsOrigin()
    {
        var result = new TemplateMatchResult
        {
            X = 50, Y = 50, Width = 0, Height = 0, Confidence = 1.0,
        };

        Assert.Equal(50, result.CenterX);
        Assert.Equal(50, result.CenterY);
    }
}
