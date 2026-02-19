namespace SystemHarness.Tests.Input;

[Trait("Category", "CI")]
public class KeyEnumTests
{
    [Theory]
    [InlineData(Key.VolumeMute)]
    [InlineData(Key.VolumeDown)]
    [InlineData(Key.VolumeUp)]
    [InlineData(Key.MediaNext)]
    [InlineData(Key.MediaPrev)]
    [InlineData(Key.MediaStop)]
    [InlineData(Key.MediaPlayPause)]
    [InlineData(Key.BrowserBack)]
    [InlineData(Key.BrowserForward)]
    [InlineData(Key.BrowserRefresh)]
    [InlineData(Key.BrowserStop)]
    [InlineData(Key.BrowserSearch)]
    [InlineData(Key.BrowserFavorites)]
    [InlineData(Key.BrowserHome)]
    [InlineData(Key.Sleep)]
    [InlineData(Key.LaunchMail)]
    [InlineData(Key.LaunchApp1)]
    [InlineData(Key.LaunchApp2)]
    public void MediaBrowserSystemKeys_AreDefined(Key key)
    {
        Assert.True(Enum.IsDefined(key));
    }

    [Fact]
    public void AllKeyValues_AreUnique()
    {
        var values = Enum.GetValues<Key>();
        var distinct = values.Distinct().Count();
        Assert.Equal(values.Length, distinct);
    }

    [Fact]
    public void KeyCount_IncludesNewMediaBrowserSystemKeys()
    {
        var count = Enum.GetValues<Key>().Length;
        Assert.True(count >= 83, $"Expected at least 83 keys, got {count}");
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Z)]
    [InlineData(Key.D0)]
    [InlineData(Key.D9)]
    [InlineData(Key.F1)]
    [InlineData(Key.F12)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    [InlineData(Key.Tab)]
    [InlineData(Key.Space)]
    [InlineData(Key.Backspace)]
    [InlineData(Key.Delete)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    [InlineData(Key.PageUp)]
    [InlineData(Key.PageDown)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Ctrl)]
    [InlineData(Key.Alt)]
    [InlineData(Key.Shift)]
    [InlineData(Key.Win)]
    [InlineData(Key.CapsLock)]
    [InlineData(Key.NumLock)]
    [InlineData(Key.PrintScreen)]
    [InlineData(Key.Insert)]
    public void CoreKeys_AreDefined(Key key)
    {
        Assert.True(Enum.IsDefined(key));
    }
}
