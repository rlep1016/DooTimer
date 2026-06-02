using System.Text.Json;
using DooTimer.Models;

namespace DooTimer.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Dictionary<string, object?> DefaultConfig = new()
    {
        ["daily_limit_minutes"] = 60,
        ["remind_at_percent"] = 80,
        ["over_limit_reminder_minutes"] = 5,
        ["poll_interval_seconds"] = 0.06,
        ["client_process_names"] = new[] { "douyin.exe" },
        ["browser_process_names"] = new[] { "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe" },
        ["title_keywords"] = new[] { "抖音", "Douyin", "douyin.com" }
    };

    private readonly string _path;

    public ConfigService(string path)
    {
        _path = path;
    }

    public AppConfig Load()
    {
        if (!File.Exists(_path))
        {
            Save(new AppConfig());
            return new AppConfig();
        }

        Dictionary<string, JsonElement> raw;
        try
        {
            var json = File.ReadAllText(_path);
            raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
        }
        catch (JsonException)
        {
            var backupPath = _path.Replace(".json", ".invalid.json");
            if (File.Exists(backupPath))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                backupPath = _path.Replace(".json", $".{timestamp}.invalid.json");
            }
            File.Move(_path, backupPath);
            Save(new AppConfig());
            throw new ConfigException($"配置文件格式错误，已备份并重建默认配置。");
        }

        return BuildConfig(raw);
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var dict = new Dictionary<string, object?>
        {
            ["daily_limit_minutes"] = config.DailyLimitMinutes,
            ["remind_at_percent"] = config.RemindAtPercent,
            ["over_limit_reminder_minutes"] = config.OverLimitReminderMinutes,
            ["poll_interval_seconds"] = config.PollIntervalSeconds,
            ["client_process_names"] = config.ClientProcessNames,
            ["browser_process_names"] = config.BrowserProcessNames,
            ["title_keywords"] = config.TitleKeywords,
            ["force_close_enabled"] = config.ForceCloseEnabled,
            ["mode"] = config.Mode,
            ["theme"] = config.Theme,
            ["rest_reminder_minutes"] = config.RestReminderMinutes
        };

        var json = JsonSerializer.Serialize(dict, JsonOptions);
        File.WriteAllText(_path, json);
    }

    private static AppConfig BuildConfig(Dictionary<string, JsonElement> raw)
    {
        var dailyLimitMinutes = GetInt(raw, "daily_limit_minutes", 60);
        var remindAtPercent = GetInt(raw, "remind_at_percent", 80);
        var overLimitReminderMinutes = GetInt(raw, "over_limit_reminder_minutes", 5);
        var pollIntervalSeconds = GetDouble(raw, "poll_interval_seconds", 0.06);
        var clientProcessNames = GetStringList(raw, "client_process_names", ["douyin.exe"]);
        var browserProcessNames = GetStringList(raw, "browser_process_names",
            ["chrome.exe", "msedge.exe", "firefox.exe", "brave.exe"]);
        var titleKeywords = GetStringList(raw, "title_keywords", ["抖音", "Douyin", "douyin.com"]);
        var forceCloseEnabled = GetBool(raw, "force_close_enabled", false);
        var mode = GetString(raw, "mode", "limit");
        var theme = GetString(raw, "theme", "light");
        var restReminderMinutes = GetInt(raw, "rest_reminder_minutes", 0);

        // 校验
        if (dailyLimitMinutes < 1 || dailyLimitMinutes > 1440)
            throw new ConfigException("每日上限需要在 1 到 1440 分钟之间。");
        if (remindAtPercent < 1 || remindAtPercent > 100)
            throw new ConfigException("提前提醒百分比需要在 1 到 100 之间。");
        if (overLimitReminderMinutes < 1 || overLimitReminderMinutes > 240)
            throw new ConfigException("超时重复提醒需要在 1 到 240 分钟之间。");
        if (pollIntervalSeconds < 0.05 || pollIntervalSeconds > 10)
            throw new ConfigException("检测间隔需要在 0.05 到 10 秒之间。");
        if (clientProcessNames.Count == 0)
            throw new ConfigException("客户端进程名至少需要填写一个。");
        if (browserProcessNames.Count == 0)
            throw new ConfigException("浏览器进程名至少需要填写一个。");
        if (titleKeywords.Count == 0)
            throw new ConfigException("网页标题关键词至少需要填写一个。");
        if (mode != "limit" && mode != "track")
            throw new ConfigException("模式只允许 limit 或 track。");
        if (theme != "light" && theme != "dark" && theme != "system")
            throw new ConfigException("主题只允许 light、dark 或 system。");
        if (restReminderMinutes < 0 || restReminderMinutes > 240)
            throw new ConfigException("休息提醒间隔需要在 0 到 240 分钟之间。");

        return new AppConfig
        {
            DailyLimitMinutes = dailyLimitMinutes,
            RemindAtPercent = remindAtPercent,
            OverLimitReminderMinutes = overLimitReminderMinutes,
            PollIntervalSeconds = pollIntervalSeconds,
            ClientProcessNames = clientProcessNames,
            BrowserProcessNames = browserProcessNames,
            TitleKeywords = titleKeywords,
            ForceCloseEnabled = forceCloseEnabled,
            Mode = mode,
            Theme = theme,
            RestReminderMinutes = restReminderMinutes
        };
    }

    private static int GetInt(Dictionary<string, JsonElement> raw, string key, int defaultValue)
    {
        if (raw.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetInt32();
        return defaultValue;
    }

    private static double GetDouble(Dictionary<string, JsonElement> raw, string key, double defaultValue)
    {
        if (raw.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDouble();
        return defaultValue;
    }

    private static bool GetBool(Dictionary<string, JsonElement> raw, string key, bool defaultValue)
    {
        if (raw.TryGetValue(key, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False))
            return el.GetBoolean();
        return defaultValue;
    }

    private static string GetString(Dictionary<string, JsonElement> raw, string key, string defaultValue)
    {
        if (raw.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString() ?? defaultValue;
        return defaultValue;
    }

    private static List<string> GetStringList(Dictionary<string, JsonElement> raw, string key, List<string> defaultValue)
    {
        if (!raw.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array)
            return defaultValue;

        var result = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var val = item.GetString()?.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(val))
                    result.Add(val);
            }
        }
        return result.Count > 0 ? result : defaultValue;
    }
}

public class ConfigException : Exception
{
    public ConfigException(string message) : base(message) { }
}
