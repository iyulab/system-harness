namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class RectangleTests
{
    [Fact]
    public void CenterX_CalculatesCorrectly()
    {
        var rect = new Rectangle(100, 200, 400, 300);
        Assert.Equal(300, rect.CenterX); // 100 + 400/2
    }

    [Fact]
    public void CenterY_CalculatesCorrectly()
    {
        var rect = new Rectangle(100, 200, 400, 300);
        Assert.Equal(350, rect.CenterY); // 200 + 300/2
    }

    [Fact]
    public void Right_CalculatesCorrectly()
    {
        var rect = new Rectangle(100, 200, 400, 300);
        Assert.Equal(500, rect.Right); // 100 + 400
    }

    [Fact]
    public void Bottom_CalculatesCorrectly()
    {
        var rect = new Rectangle(100, 200, 400, 300);
        Assert.Equal(500, rect.Bottom); // 200 + 300
    }

    [Fact]
    public void Contains_InsidePoint_ReturnsTrue()
    {
        var rect = new Rectangle(10, 20, 100, 200);
        Assert.True(rect.Contains(50, 100));
    }

    [Fact]
    public void Contains_OnTopLeftCorner_ReturnsTrue()
    {
        var rect = new Rectangle(10, 20, 100, 200);
        Assert.True(rect.Contains(10, 20));
    }

    [Fact]
    public void Contains_OnRightEdge_ReturnsFalse()
    {
        var rect = new Rectangle(10, 20, 100, 200);
        Assert.False(rect.Contains(110, 100)); // Right edge is exclusive
    }

    [Fact]
    public void Contains_OnBottomEdge_ReturnsFalse()
    {
        var rect = new Rectangle(10, 20, 100, 200);
        Assert.False(rect.Contains(50, 220)); // Bottom edge is exclusive
    }

    [Fact]
    public void Contains_OutsidePoint_ReturnsFalse()
    {
        var rect = new Rectangle(10, 20, 100, 200);
        Assert.False(rect.Contains(0, 0));
    }

    [Fact]
    public void Intersects_OverlappingRects_ReturnsTrue()
    {
        var a = new Rectangle(0, 0, 100, 100);
        var b = new Rectangle(50, 50, 100, 100);
        Assert.True(a.Intersects(b));
        Assert.True(b.Intersects(a));
    }

    [Fact]
    public void Intersects_NonOverlapping_ReturnsFalse()
    {
        var a = new Rectangle(0, 0, 100, 100);
        var b = new Rectangle(200, 200, 100, 100);
        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void Intersects_TouchingEdges_ReturnsFalse()
    {
        var a = new Rectangle(0, 0, 100, 100);
        var b = new Rectangle(100, 0, 100, 100); // Touching at x=100
        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new Rectangle(10, 20, 100, 200);
        var b = new Rectangle(10, 20, 100, 200);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new Rectangle(10, 20, 100, 200);
        var b = new Rectangle(10, 20, 101, 200);
        Assert.NotEqual(a, b);
    }

    // --- Consolidated from Common/RectangleTests (cycle 232) ---

    [Fact]
    public void Intersects_Contained_ReturnsTrue()
    {
        var outer = new Rectangle(0, 0, 200, 200);
        var inner = new Rectangle(50, 50, 10, 10);
        Assert.True(outer.Intersects(inner));
        Assert.True(inner.Intersects(outer));
    }

    [Fact]
    public void ZeroSize_PropertiesCorrect()
    {
        var rect = new Rectangle(5, 10, 0, 0);
        Assert.Equal(5, rect.Right);
        Assert.Equal(10, rect.Bottom);
        Assert.Equal(5, rect.CenterX);
        Assert.Equal(10, rect.CenterY);
    }
}
