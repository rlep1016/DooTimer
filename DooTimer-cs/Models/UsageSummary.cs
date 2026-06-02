namespace DooTimer.Models;

public sealed class UsageSummary
{
    public double TotalSeconds { get; set; }
    public int SessionCount { get; set; }
    public List<SessionRecord> Sessions { get; set; } = [];
    public Dictionary<string, string> Reminders { get; set; } = [];
    public List<DaySnapshot> RecentDays { get; set; } = [];
    public double PreviousWeekTotalSeconds { get; set; }
    public List<HourlyData> HourlyBreakdown { get; set; } = [];
}

public sealed class HourlyData
{
    public int Hour { get; set; }
    public string Label { get; set; } = "";
    public double TotalSeconds { get; set; }
}

public sealed class SessionRecord
{
    public string StartedAt { get; set; } = "";
    public string EndedAt { get; set; } = "";
    public double Seconds { get; set; }
    public string Source { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
}

public sealed class DaySnapshot
{
    public string Day { get; set; } = "";
    public string Label { get; set; } = "";
    public double TotalSeconds { get; set; }
    public int SessionCount { get; set; }
}
