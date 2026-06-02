using DooTimer.Services;

namespace DooTimer.Tests;

public class UsageStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _usagePath;

    public UsageStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DooTimerUsageTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _usagePath = Path.Combine(_tempDir, "usage.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void NewStorage_CreatesFile()
    {
        var storage = new UsageStorage(_usagePath);
        Assert.True(File.Exists(_usagePath));
    }

    [Fact]
    public void AddSeconds_And_GetTotalSeconds_AreConsistent()
    {
        var storage = new UsageStorage(_usagePath);
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        storage.AddSeconds(today, 120.5);
        storage.FlushDayData();

        var total = storage.GetTotalSeconds(today);
        Assert.Equal(120.5, total);
    }

    [Fact]
    public void AddSeconds_Accumulates()
    {
        var storage = new UsageStorage(_usagePath);
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        storage.AddSeconds(today, 60);
        storage.AddSeconds(today, 30);
        storage.FlushDayData();

        var total = storage.GetTotalSeconds(today);
        Assert.Equal(90, total);
    }

    [Fact]
    public void AddSession_And_FlushSessions_Persists()
    {
        var storage = new UsageStorage(_usagePath);
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var start = DateTime.Now.AddMinutes(-5);
        var end = DateTime.Now;

        storage.AddSession(today, start, end, "client", "douyin.exe", "抖音");
        storage.FlushSessions();

        var summary = storage.GetDaySummary(today);
        Assert.Equal(1, summary.SessionCount);
        Assert.Equal("client", summary.Sessions[0].Source);
        Assert.Equal("douyin.exe", summary.Sessions[0].ProcessName);
    }

    [Fact]
    public void AddSession_TooShort_IsIgnored()
    {
        var storage = new UsageStorage(_usagePath);
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var now = DateTime.Now;

        // 小于 1 秒的会话应被忽略
        storage.AddSession(today, now, now.AddMilliseconds(500), "client", "douyin.exe", "抖音");
        storage.FlushSessions();

        var summary = storage.GetDaySummary(today);
        Assert.Equal(0, summary.SessionCount);
    }

    [Fact]
    public void GetDaySummary_UnknownDay_ReturnsEmpty()
    {
        var storage = new UsageStorage(_usagePath);
        var summary = storage.GetDaySummary("2020-01-01");
        Assert.Equal(0, summary.TotalSeconds);
        Assert.Equal(0, summary.SessionCount);
    }

    [Fact]
    public void GetRecentDays_ReturnsCorrectCount()
    {
        var storage = new UsageStorage(_usagePath);
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        storage.AddSeconds(today, 60);
        storage.FlushDayData();

        var recent = storage.GetRecentDays(today, 7);
        Assert.Equal(7, recent.Count);
        // 倒数第一个（今天）应该有数据
        Assert.True(recent[^1].TotalSeconds > 0);
    }

    [Fact]
    public void Reminders_CanBeSetAndRetrieved()
    {
        var storage = new UsageStorage(_usagePath);
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        storage.SetReminder(today, "percent_80", "2024-01-01T12:00:00");
        var reminder = storage.GetReminder(today, "percent_80");

        Assert.Equal("2024-01-01T12:00:00", reminder);
    }

    [Fact]
    public void GetReminder_UnknownKey_ReturnsNull()
    {
        var storage = new UsageStorage(_usagePath);
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        var reminder = storage.GetReminder(today, "nonexistent");
        Assert.Null(reminder);
    }
}
