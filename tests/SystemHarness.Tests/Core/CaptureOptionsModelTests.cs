namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class CaptureOptionsModelTests
{
    [Fact]
    public void CaptureOptions_Defaults()
    {
        var opts = new CaptureOptions();

        Assert.Equal(ImageFormat.Jpeg, opts.Format);
        Assert.Equal(80, opts.Quality);
        Assert.Equal(1024, opts.TargetWidth);
        Assert.Equal(768, opts.TargetHeight);
        Assert.True(opts.IncludeCursor);
    }

    [Fact]
    public void CaptureOptions_CustomValues()
    {
        var opts = new CaptureOptions
        {
            Format = ImageFormat.Png,
            Quality = 100,
            TargetWidth = 1920,
            TargetHeight = 1080,
            IncludeCursor = false,
        };

        Assert.Equal(ImageFormat.Png, opts.Format);
        Assert.Equal(100, opts.Quality);
        Assert.Equal(1920, opts.TargetWidth);
        Assert.Equal(1080, opts.TargetHeight);
        Assert.False(opts.IncludeCursor);
    }

    [Fact]
    public void CaptureOptions_NullDimensions_MeansNoResize()
    {
        var opts = new CaptureOptions
        {
            TargetWidth = null,
            TargetHeight = null,
        };

        Assert.Null(opts.TargetWidth);
        Assert.Null(opts.TargetHeight);
    }

    [Fact]
    public void CaptureOptions_IsSealed()
    {
        Assert.True(typeof(CaptureOptions).IsSealed);
    }
}
