using SystemHarness.Apps.Office;

namespace SystemHarness.Tests.Office;

[Trait("Category", "CI")]
public class CellAddressTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(1, "B")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void ColumnToLetter(int index, string expected)
    {
        Assert.Equal(expected, CellAddress.ColumnToLetter(index));
    }

    [Theory]
    [InlineData("A", 0)]
    [InlineData("B", 1)]
    [InlineData("Z", 25)]
    [InlineData("AA", 26)]
    [InlineData("AB", 27)]
    [InlineData("AZ", 51)]
    [InlineData("BA", 52)]
    [InlineData("ZZ", 701)]
    [InlineData("AAA", 702)]
    public void LetterToColumn(string letter, int expected)
    {
        Assert.Equal(expected, CellAddress.LetterToColumn(letter));
    }

    [Theory]
    [InlineData("a", 0)]
    [InlineData("aa", 26)]
    public void LetterToColumn_CaseInsensitive(string letter, int expected)
    {
        Assert.Equal(expected, CellAddress.LetterToColumn(letter));
    }

    [Theory]
    [InlineData(0, 0, "A1")]
    [InlineData(0, 1, "B1")]
    [InlineData(4, 2, "C5")]
    [InlineData(99, 25, "Z100")]
    public void FromIndices(int row, int col, string expected)
    {
        Assert.Equal(expected, CellAddress.FromIndices(row, col));
    }

    [Theory]
    [InlineData("A1", 0, 0)]
    [InlineData("B1", 0, 1)]
    [InlineData("C5", 4, 2)]
    [InlineData("Z100", 99, 25)]
    [InlineData("AA1", 0, 26)]
    public void Parse(string reference, int expectedRow, int expectedCol)
    {
        var (row, col) = CellAddress.Parse(reference);
        Assert.Equal(expectedRow, row);
        Assert.Equal(expectedCol, col);
    }

    [Theory]
    [InlineData("A1:C5", 0, 0, 4, 2)]
    [InlineData("B2:D10", 1, 1, 9, 3)]
    public void ParseRange(string range, int startRow, int startCol, int endRow, int endCol)
    {
        var result = CellAddress.ParseRange(range);
        Assert.Equal(startRow, result.StartRow);
        Assert.Equal(startCol, result.StartColumn);
        Assert.Equal(endRow, result.EndRow);
        Assert.Equal(endCol, result.EndColumn);
    }

    [Fact]
    public void ToRange()
    {
        Assert.Equal("A1:C5", CellAddress.ToRange(0, 0, 4, 2));
    }

    [Fact]
    public void RoundTrip_ColumnIndex()
    {
        for (int i = 0; i < 1000; i++)
        {
            var letter = CellAddress.ColumnToLetter(i);
            var back = CellAddress.LetterToColumn(letter);
            Assert.Equal(i, back);
        }
    }

    [Fact]
    public void RoundTrip_CellReference()
    {
        var reference = CellAddress.FromIndices(42, 100);
        var (row, col) = CellAddress.Parse(reference);
        Assert.Equal(42, row);
        Assert.Equal(100, col);
    }

    [Fact]
    public void InvalidColumn_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CellAddress.ColumnToLetter(-1));
    }

    [Fact]
    public void InvalidReference_Throws()
    {
        Assert.Throws<ArgumentException>(() => CellAddress.Parse("123"));
        Assert.Throws<ArgumentException>(() => CellAddress.Parse(""));
    }

    [Fact]
    public void InvalidRange_Throws()
    {
        Assert.Throws<ArgumentException>(() => CellAddress.ParseRange("A1"));
        Assert.Throws<ArgumentException>(() => CellAddress.ParseRange(""));
    }
}
