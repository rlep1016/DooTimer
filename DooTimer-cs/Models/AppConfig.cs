using System.Text.Json.Serialization;

namespace DooTimer.Models;

public sealed record AppConfig
{
    public int DailyLimitMinutes { get; init; } = 60;
    public int RemindAtPercent { get; init; } = 80;
    public int OverLimitReminderMinutes { get; init; } = 5;
    public double PollIntervalSeconds { get; init; } = 0.06;
    public List<string> ClientProcessNames { get; init; } = ["douyin.exe"];
    public List<string> BrowserProcessNames { get; init; } = ["chrome.exe", "msedge.exe", "firefox.exe", "brave.exe"];
    public List<string> TitleKeywords { get; init; } = ["抖音", "Douyin", "douyin.com"];
    public bool ForceCloseEnabled { get; init; } = false;
    public string Mode { get; init; } = "limit"; // "limit"=限制模式 "track"=统计模式
    public string Theme { get; init; } = "light"; // "light"=亮色 "dark"=暗色 "system"=随系统
    public int RestReminderMinutes { get; init; } = 0; // 统计模式下每 N 分钟提醒休息，0=关闭

    [JsonIgnore]
    public int DailyLimitSeconds => DailyLimitMinutes * 60;
    [JsonIgnore]
    public int RemindAtSeconds => (int)(DailyLimitSeconds * (RemindAtPercent / 100.0));
    [JsonIgnore]
    public int OverLimitReminderSeconds => OverLimitReminderMinutes * 60;
    [JsonIgnore]
    public int RestReminderSeconds => RestReminderMinutes * 60;
}
