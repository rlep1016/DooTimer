using System.Text.Json;
using System.Text.Json.Serialization;
using DooTimer.Models;

namespace DooTimer.Services;

public class UsageStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly object _lock = new();
    private readonly List<PendingSession> _pendingSessions = [];

    // 内存缓存：避免每秒写盘。AddSeconds 只更新缓存，定期批量写入。
    private string? _cachedDay;
    private double _cachedTotalSeconds;
    private bool _dirty;

    public UsageStorage(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(path))
            WriteData(new UsageData());
    }

    public void AddSeconds(string day, double seconds)
    {
        lock (_lock)
        {
            if (day != _cachedDay)
            {
                // 切换日期前，先把旧日期的缓存写入磁盘
                if (_dirty && _cachedDay != null)
                    FlushCachedDayLocked();
                _cachedDay = day;
                _cachedTotalSeconds = ReadTotalFromFileLocked(day);
            }
            _cachedTotalSeconds = Math.Round(_cachedTotalSeconds + seconds, 3);
            _dirty = true;
        }
    }

    private double ReadTotalFromFileLocked(string day)
    {
        var data = ReadData();
        if (data.Days.TryGetValue(day, out var dayData))
            return dayData.TotalSeconds;
        return 0;
    }

    /// <summary>
    /// 将缓存中的 TotalSeconds 写入磁盘。已在锁内时调用此方法。
    /// </summary>
    private void FlushCachedDayLocked()
    {
        if (!_dirty || _cachedDay == null) return;
        var data = ReadData();
        var dayData = EnsureDay(data, _cachedDay);
        dayData.TotalSeconds = Math.Round(_cachedTotalSeconds, 3);
        WriteData(data);
        _dirty = false;
    }

    /// <summary>
    /// 强制将缓存的当天数据写入磁盘（公开方法，外部调用）。
    /// </summary>
    public void FlushDayData()
    {
        lock (_lock)
        {
            FlushCachedDayLocked();
        }
    }

    public void AddSession(string day, DateTime startedAt, DateTime endedAt,
        string source, string processName, string windowTitle)
    {
        var seconds = Math.Max(0, (endedAt - startedAt).TotalSeconds);
        if (seconds < 1) return;

        _pendingSessions.Add(new PendingSession
        {
            Day = day,
            StartedAt = startedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            EndedAt = endedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            Seconds = Math.Round(seconds, 3),
            Source = source,
            ProcessName = processName,
            WindowTitle = windowTitle.Length > 160 ? windowTitle[..160] : windowTitle
        });
    }

    public void FlushSessions()
    {
        lock (_lock)
        {
            // 有脏缓存或有待写入的会话时才工作
            if (_pendingSessions.Count == 0 && !_dirty) return;

            var data = ReadData();

            // 合并缓存的 TotalSeconds
            if (_dirty && _cachedDay != null)
            {
                var cacheDayData = EnsureDay(data, _cachedDay);
                cacheDayData.TotalSeconds = Math.Round(_cachedTotalSeconds, 3);
                _dirty = false;
            }

            foreach (var session in _pendingSessions)
            {
                var dayData = EnsureDay(data, session.Day);
                dayData.Sessions.Add(new SessionData
                {
                    StartedAt = session.StartedAt,
                    EndedAt = session.EndedAt,
                    Seconds = session.Seconds,
                    Source = session.Source,
                    ProcessName = session.ProcessName,
                    WindowTitle = session.WindowTitle
                });
            }
            WriteData(data);
            _pendingSessions.Clear();
        }
    }

    public int PendingSessionCount => _pendingSessions.Count;

    public double GetTotalSeconds(string day)
    {
        lock (_lock)
        {
            if (day == _cachedDay && _dirty)
                return _cachedTotalSeconds;
            return ReadTotalFromFileLocked(day);
        }
    }

    public UsageSummary GetDaySummary(string day)
    {
        lock (_lock)
        {
            var data = ReadData();
            if (!data.Days.TryGetValue(day, out var dayData))
                return new UsageSummary();

            // 当天使用缓存中的 TotalSeconds（比文件更新）
            var totalSeconds = (day == _cachedDay && _dirty) ? _cachedTotalSeconds : dayData.TotalSeconds;

            return new UsageSummary
            {
                TotalSeconds = totalSeconds,
                SessionCount = dayData.Sessions.Count,
                Sessions = dayData.Sessions.Select(s => new SessionRecord
                {
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    Seconds = s.Seconds,
                    Source = s.Source,
                    ProcessName = s.ProcessName,
                    WindowTitle = s.WindowTitle
                }).ToList(),
                Reminders = dayData.Reminders ?? []
            };
        }
    }

    public List<DaySnapshot> GetRecentDays(string endDay, int days = 7)
    {
        if (!DateOnly.TryParse(endDay, out var endDate))
            endDate = DateOnly.FromDateTime(DateTime.Today);

        days = Math.Max(1, days);
        lock (_lock)
        {
            var data = ReadData();
            var result = new List<DaySnapshot>();
            for (var offset = days - 1; offset >= 0; offset--)
            {
                var currentDate = endDate.AddDays(-offset);
                var key = currentDate.ToString("yyyy-MM-dd");
                data.Days.TryGetValue(key, out var dayData);

                result.Add(new DaySnapshot
                {
                    Day = key,
                    Label = $"{currentDate.Month}/{currentDate.Day}",
                    TotalSeconds = dayData?.TotalSeconds ?? 0,
                    SessionCount = dayData?.Sessions.Count ?? 0
                });
            }
            return result;
        }
    }

    public double[] GetHourlyBreakdown(string day)
    {
        var buckets = new double[24];
        List<SessionData> sessions;
        lock (_lock)
        {
            var data = ReadData();
            if (!data.Days.TryGetValue(day, out var dayData))
                return buckets;
            sessions = dayData.Sessions.ToList();
        }

        foreach (var s in sessions)
        {
            if (!DateTime.TryParse(s.StartedAt, out var start)) continue;
            if (!DateTime.TryParse(s.EndedAt, out var end)) continue;
            if (end <= start) continue;

            var cursor = start;
            while (cursor < end)
            {
                var hour = cursor.Hour;
                var nextBoundary = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0).AddHours(1);
                if (nextBoundary > end) nextBoundary = end;

                var segSeconds = (nextBoundary - cursor).TotalSeconds;
                buckets[hour] = Math.Round(buckets[hour] + segSeconds, 3);

                cursor = nextBoundary;
            }
        }

        return buckets;
    }

    public double GetPreviousWeekTotal(string today, int days = 7)
    {
        if (!DateOnly.TryParse(today, out var todayDate))
            return 0;

        var prevEnd = todayDate.AddDays(-days);
        var prevStart = todayDate.AddDays(-days * 2 + 1);

        double total = 0;
        lock (_lock)
        {
            var data = ReadData();
            for (var d = prevStart; d <= prevEnd; d = d.AddDays(1))
            {
                var key = d.ToString("yyyy-MM-dd");
                if (data.Days.TryGetValue(key, out var dayData))
                    total += dayData.TotalSeconds;
            }
        }
        return total;
    }

    public string ExportCsv(string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        List<SessionRecord> allSessions;
        lock (_lock)
        {
            var data = ReadData();
            allSessions = data.Days
                .OrderBy(kv => kv.Key)
                .SelectMany(kv => kv.Value.Sessions.Select(s => new SessionRecord
                {
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    Seconds = s.Seconds,
                    Source = s.Source,
                    ProcessName = s.ProcessName,
                    WindowTitle = s.WindowTitle
                }))
                .ToList();
        }

        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);
        // 写入 UTF-8 BOM 方便 Excel 打开
        writer.BaseStream.Write(new byte[] { 0xEF, 0xBB, 0xBF });
        writer.WriteLine("date,started_at,ended_at,seconds,minutes,source,process_name,window_title");
        foreach (var s in allSessions)
        {
            var date = s.StartedAt.Length >= 10 ? s.StartedAt[..10] : "";
            var minutes = Math.Round(s.Seconds / 60.0, 3);
            writer.WriteLine($"\"{date}\",\"{s.StartedAt}\",\"{s.EndedAt}\",{s.Seconds},{minutes},\"{s.Source}\",\"{s.ProcessName}\",\"{s.WindowTitle}\"");
        }

        return outputPath;
    }

    public string? GetReminder(string day, string key)
    {
        lock (_lock)
        {
            var data = ReadData();
            if (data.Days.TryGetValue(day, out var dayData)
                && dayData.Reminders != null
                && dayData.Reminders.TryGetValue(key, out var value))
                return value;
            return null;
        }
    }

    public void SetReminder(string day, string key, string value)
    {
        lock (_lock)
        {
            var data = ReadData();

            // 合并缓存的 TotalSeconds（如果有脏数据，合并后再写）
            if (_dirty && _cachedDay != null)
            {
                var cacheDayData = EnsureDay(data, _cachedDay);
                cacheDayData.TotalSeconds = Math.Round(_cachedTotalSeconds, 3);
                _dirty = false;
            }

            var dayData = EnsureDay(data, day);
            dayData.Reminders ??= [];
            dayData.Reminders[key] = value;
            WriteData(data);
        }
    }

    private DayData EnsureDay(UsageData data, string day)
    {
        if (!data.Days.TryGetValue(day, out var dayData))
        {
            dayData = new DayData();
            data.Days[day] = dayData;
        }
        return dayData;
    }

    private UsageData ReadData()
    {
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<UsageData>(json) ?? new UsageData();
        }
        catch (FileNotFoundException)
        {
            var fresh = new UsageData();
            WriteData(fresh);
            return fresh;
        }
        catch (JsonException)
        {
            BackupInvalidFile();
            var fresh = new UsageData();
            WriteData(fresh);
            return fresh;
        }
    }

    private void WriteData(UsageData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_path, json);
    }

    private void BackupInvalidFile()
    {
        var backupPath = _path.Replace(".json", ".invalid.json");
        if (File.Exists(backupPath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            backupPath = _path.Replace(".json", $".{timestamp}.invalid.json");
        }
        File.Move(_path, backupPath);
    }

    // 内部数据类
    private class UsageData
    {
        [JsonPropertyName("days")]
        public Dictionary<string, DayData> Days { get; set; } = [];
    }

    private class DayData
    {
        [JsonPropertyName("total_seconds")]
        public double TotalSeconds { get; set; }
        [JsonPropertyName("sessions")]
        public List<SessionData> Sessions { get; set; } = [];
        [JsonPropertyName("reminders")]
        public Dictionary<string, string>? Reminders { get; set; }
    }

    private class SessionData
    {
        [JsonPropertyName("started_at")]
        public string StartedAt { get; set; } = "";
        [JsonPropertyName("ended_at")]
        public string EndedAt { get; set; } = "";
        [JsonPropertyName("seconds")]
        public double Seconds { get; set; }
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";
        [JsonPropertyName("process_name")]
        public string ProcessName { get; set; } = "";
        [JsonPropertyName("window_title")]
        public string WindowTitle { get; set; } = "";
    }

    private class PendingSession
    {
        public string Day { get; set; } = "";
        public string StartedAt { get; set; } = "";
        public string EndedAt { get; set; } = "";
        public double Seconds { get; set; }
        public string Source { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string WindowTitle { get; set; } = "";
    }
}
