namespace WinGetScout.Tests;

public sealed class UiTextTests
{
    [Theory]
    [InlineData ("A界Z", 2, "A")]
    [InlineData ("A界Z", 3, "A界")]
    [InlineData ("A👩‍💻Z", 3, "A👩‍💻")]
    [InlineData ("e\u0301Z", 1, "e\u0301")]
    [InlineData ("A\tZ", 1, "A\t")]
    [InlineData ("🇺🇸Z", 1, "")]
    public void ClippingKeepsWholeGraphemesWithinTheTerminalWidth (string text, int width, string expected)
    {
        Assert.Equal (expected, UiText.TakeColumns (text, width));
    }
}
