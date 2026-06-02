using DooTimer.Helpers;

namespace DooTimer.Tests;

public class FormatHelperTests
{
    [Theory]
    [InlineData(0, "0 分 0 秒")]
    [InlineData(30, "0 分 30 秒")]
    [InlineData(65, "1 分 5 秒")]
    [InlineData(3600, "1 小时 0 分钟")]
    [InlineData(3661, "1 小时 1 分钟")]
    [InlineData(7200, "2 小时 0 分钟")]
    [InlineData(90061, "25 小时 1 分钟")]
    [InlineData(-10, "0 分 0 秒")] // 负数处理
    public void FormatSeconds_ReturnsCorrectFormat(double seconds, string expected)
    {
        var result = FormatHelper.FormatSeconds(seconds);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65, "1:05")]
    [InlineData(3661, "1:01:01")]
    [InlineData(7200, "2:00:00")]
    public void FormatCompactSeconds_ReturnsCorrectFormat(double seconds, string expected)
    {
        var result = FormatHelper.FormatCompactSeconds(seconds);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("client", "客户端")]
    [InlineData("web", "网页")]
    [InlineData("", "未检测到")]
    [InlineData("unknown", "未检测到")]
    public void SourceText_ReturnsCorrectText(string source, string expected)
    {
        var result = FormatHelper.SourceText(source);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(3.0, "3")]
    [InlineData(3.14, "3.14")]
    [InlineData(0.0, "0")]
    [InlineData(1.5, "1.5")]
    public void FormatDecimal_ReturnsCorrectFormat(double value, string expected)
    {
        var result = FormatHelper.FormatDecimal(value);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatFileSize_NonExistentFile_ReturnsWeiShengCheng()
    {
        var result = FormatHelper.FormatFileSize(@"Z:\nonexistent\file.txt");
        Assert.Equal("未生成", result);
    }
}
