using CafConverter.Helpers;

namespace CafConverter.Tests;

public sealed class TimeSpanParserTests
{
    [Theory]
    [InlineData("29", 29)]
    [InlineData("01:29.500", 89.5)]
    [InlineData("01:02:03.250", 3723.25)]
    [InlineData("00:00.000", 0)]
    public void ParsesSupportedFormats(string text, double seconds)
    {
        Assert.True(TimeSpanParser.TryParse(text, out var value));
        Assert.Equal(seconds, value.TotalSeconds, 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("00:60")]
    [InlineData("1:60:00")]
    [InlineData("00:01.1234")]
    public void RejectsInvalidFormats(string text) => Assert.False(TimeSpanParser.TryParse(text, out _));
}
