namespace DooTimer.Models;

public sealed record DashboardSnapshot(
    bool IsDouyin,
    string Source,
    string ProcessName,
    string WindowTitle,
    double UsedSeconds,
    int LimitSeconds,
    int LimitMinutes,
    bool IsTrackMode
);
