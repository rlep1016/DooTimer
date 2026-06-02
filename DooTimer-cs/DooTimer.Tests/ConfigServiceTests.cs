using System.Text.Json;
using DooTimer.Services;

namespace DooTimer.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DooTimerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_NoConfigFile_ReturnsDefaults()
    {
        var service = new ConfigService(_configPath);
        var config = service.Load();

        Assert.Equal(60, config.DailyLimitMinutes);
        Assert.Equal(80, config.RemindAtPercent);
        Assert.Equal(5, config.OverLimitReminderMinutes);
        Assert.Equal(0.06, config.PollIntervalSeconds);
        Assert.Contains("douyin.exe", config.ClientProcessNames);
        Assert.Equal("limit", config.Mode);
        Assert.Equal("light", config.Theme);
        Assert.False(config.ForceCloseEnabled);
        Assert.Equal(0, config.RestReminderMinutes);
    }

    [Fact]
    public void Load_ValidConfigFile_ReturnsCorrectValues()
    {
        var json = @"{
            ""daily_limit_minutes"": 30,
            ""remind_at_percent"": 50,
            ""over_limit_reminder_minutes"": 10,
            ""poll_interval_seconds"": 0.1,
            ""client_process_names"": [""douyin.exe""],
            ""browser_process_names"": [""chrome.exe""],
            ""title_keywords"": [""抖音""],
            ""force_close_enabled"": true,
            ""mode"": ""track"",
            ""theme"": ""dark"",
            ""rest_reminder_minutes"": 30
        }";
        File.WriteAllText(_configPath, json);

        var service = new ConfigService(_configPath);
        var config = service.Load();

        Assert.Equal(30, config.DailyLimitMinutes);
        Assert.Equal(50, config.RemindAtPercent);
        Assert.Equal(10, config.OverLimitReminderMinutes);
        Assert.Equal(0.1, config.PollIntervalSeconds);
        Assert.True(config.ForceCloseEnabled);
        Assert.Equal("track", config.Mode);
        Assert.Equal("dark", config.Theme);
        Assert.Equal(30, config.RestReminderMinutes);
    }

    [Fact]
    public void Load_InvalidJson_BacksUpAndReturnsDefaults()
    {
        File.WriteAllText(_configPath, "not valid json {{{");

        var service = new ConfigService(_configPath);

        // 格式损坏的 JSON 应抛出 ConfigException
        var ex = Assert.Throws<ConfigException>(() => service.Load());
        Assert.Contains("配置文件格式错误", ex.Message);

        // 应该生成了备份文件
        var backupFiles = Directory.GetFiles(_tempDir, "*.invalid.json");
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public void Load_InvalidValues_ThrowsConfigException()
    {
        // daily_limit_minutes 超出范围
        var json = @"{ ""daily_limit_minutes"": 0 }";
        File.WriteAllText(_configPath, json);

        var service = new ConfigService(_configPath);
        var ex = Assert.Throws<ConfigException>(() => service.Load());
        Assert.Contains("每日上限", ex.Message);
    }

    [Fact]
    public void Load_InvalidMode_ThrowsConfigException()
    {
        var json = @"{ ""mode"": ""invalid"" }";
        File.WriteAllText(_configPath, json);

        var service = new ConfigService(_configPath);
        var ex = Assert.Throws<ConfigException>(() => service.Load());
        Assert.Contains("模式", ex.Message);
    }

    [Fact]
    public void Save_ThenLoad_ReturnsSameValues()
    {
        var service = new ConfigService(_configPath);
        var original = service.Load();

        // 修改配置
        var modified = original with
        {
            DailyLimitMinutes = 45,
            RemindAtPercent = 90,
            Mode = "track",
            Theme = "system",
            ForceCloseEnabled = true,
            RestReminderMinutes = 45
        };

        service.Save(modified);
        var loaded = service.Load();

        Assert.Equal(45, loaded.DailyLimitMinutes);
        Assert.Equal(90, loaded.RemindAtPercent);
        Assert.Equal("track", loaded.Mode);
        Assert.Equal("system", loaded.Theme);
        Assert.True(loaded.ForceCloseEnabled);
        Assert.Equal(45, loaded.RestReminderMinutes);
    }

    [Fact]
    public void AppConfig_ComputedProperties_AreCorrect()
    {
        var config = new DooTimer.Models.AppConfig
        {
            DailyLimitMinutes = 60,
            RemindAtPercent = 80,
            OverLimitReminderMinutes = 5,
            RestReminderMinutes = 30
        };

        Assert.Equal(3600, config.DailyLimitSeconds);
        Assert.Equal(2880, config.RemindAtSeconds);
        Assert.Equal(300, config.OverLimitReminderSeconds);
        Assert.Equal(1800, config.RestReminderSeconds);
    }
}
