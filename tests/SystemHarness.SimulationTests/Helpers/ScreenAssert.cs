namespace SystemHarness.SimulationTests.Helpers;

/// <summary>
/// Assertion helpers for screenshot validation in simulation tests.
/// </summary>
public static class ScreenAssert
{
    /// <summary>
    /// Asserts that a screenshot has valid dimensions and non-trivial content.
    /// </summary>
    public static void IsValidScreenshot(Screenshot screenshot, int? minWidth = null, int? minHeight = null)
    {
        Assert.NotNull(screenshot);
        Assert.NotNull(screenshot.Bytes);
        Assert.True(screenshot.Bytes.Length > 0, "Screenshot bytes should not be empty");
        Assert.True(screenshot.Width > 0, "Screenshot width should be positive");
        Assert.True(screenshot.Height > 0, "Screenshot height should be positive");

        if (minWidth.HasValue)
            Assert.True(screenshot.Width >= minWidth.Value, $"Screenshot width {screenshot.Width} < minimum {minWidth}");
        if (minHeight.HasValue)
            Assert.True(screenshot.Height >= minHeight.Value, $"Screenshot height {screenshot.Height} < minimum {minHeight}");
    }

    /// <summary>
    /// Asserts that a screenshot is not entirely black (all zeros in pixel data).
    /// </summary>
    public static void IsNotBlack(Screenshot screenshot)
    {
        Assert.NotNull(screenshot.Bytes);

        // Check if more than 1% of bytes are non-zero (rough heuristic)
        var nonZeroCount = screenshot.Bytes.Count(b => b != 0);
        var ratio = (double)nonZeroCount / screenshot.Bytes.Length;

        Assert.True(ratio > 0.01, $"Screenshot appears to be mostly black ({ratio:P1} non-zero bytes)");
    }

    /// <summary>
    /// Asserts that two screenshots are different (content-wise).
    /// </summary>
    public static void AreDifferent(Screenshot a, Screenshot b)
    {
        Assert.NotNull(a.Bytes);
        Assert.NotNull(b.Bytes);

        // If lengths differ, they're definitely different
        if (a.Bytes.Length != b.Bytes.Length) return;

        // Compare first 1000 bytes for difference
        var sampleSize = Math.Min(1000, a.Bytes.Length);
        var diffCount = 0;
        for (var i = 0; i < sampleSize; i++)
        {
            if (a.Bytes[i] != b.Bytes[i]) diffCount++;
        }

        Assert.True(diffCount > 0, "Screenshots appear to be identical");
    }
}
