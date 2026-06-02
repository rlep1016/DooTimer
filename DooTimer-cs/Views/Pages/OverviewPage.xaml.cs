using System.Windows.Controls;
using DooTimer.Models;

namespace DooTimer.Views.Pages;

public partial class OverviewPage : System.Windows.Controls.UserControl
{
    public OverviewPage()
    {
        InitializeComponent();
    }

    public void Refresh(DashboardSnapshot snapshot)
    {
        DataContext = new OverviewViewModel(snapshot);
    }
}

public class OverviewViewModel
{
    public bool IsDouyin { get; }
    public string Source { get; }
    public string ProcessName { get; }
    public string WindowTitle { get; }
    public double UsedSeconds { get; }
    public int LimitMinutes { get; }
    public int LimitSeconds { get; }
    public bool IsTrackMode { get; }
    public double UsedPercent => IsTrackMode ? 0 : (LimitSeconds > 0 ? Math.Min(UsedSeconds / LimitSeconds, 1.0) : 0);
    public string RemainingText
    {
        get
        {
            if (IsTrackMode) return "不限时";
            var remaining = Math.Max(0, LimitSeconds - UsedSeconds);
            return $"剩余：{Helpers.FormatHelper.FormatSeconds(remaining)}";
        }
    }
    public string LimitText => IsTrackMode ? "不限时" : $"上限：{LimitMinutes} 分钟";

    public OverviewViewModel(DashboardSnapshot snapshot)
    {
        IsDouyin = snapshot.IsDouyin;
        Source = Helpers.FormatHelper.SourceText(snapshot.Source);
        ProcessName = snapshot.ProcessName;
        WindowTitle = snapshot.WindowTitle;
        UsedSeconds = snapshot.UsedSeconds;
        LimitMinutes = snapshot.LimitMinutes;
        LimitSeconds = snapshot.LimitSeconds;
        IsTrackMode = snapshot.IsTrackMode;
    }
}
