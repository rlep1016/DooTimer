using System.Windows;
using System.Windows.Input;
using DooTimer.Converters;
using DooTimer.Helpers;
using DooTimer.Models;

namespace DooTimer.Views.Pages;

public partial class DataPage : System.Windows.Controls.UserControl
{
    private readonly Func<UsageSummary> _getDataSummary;
    private readonly Func<string> _exportCsv;

    // 缓存上次图表数据，避免每 100ms 全量重建
    private string _lastChartKey = "";

    public DataPage(Func<UsageSummary> getDataSummary, Func<string> exportCsv)
    {
        InitializeComponent();
        _getDataSummary = getDataSummary;
        _exportCsv = exportCsv;
    }

    public void RefreshWithLimit(int limitSeconds, bool isTrackMode)
    {
        var summary = _getDataSummary();
        DataTotalLabel.Text = FormatHelper.FormatSeconds(summary.TotalSeconds);
        DataSessionsLabel.Text = $"{summary.SessionCount} 次";

        if (isTrackMode)
            DataRemainingLabel.Text = "不限时";
        else
            DataRemainingLabel.Text = FormatHelper.FormatSeconds(Math.Max(0, limitSeconds - summary.TotalSeconds));

        // 图表数据变化慢（秒级），用 key 跳过无效重建
        var key = BuildChartKey(summary, isTrackMode, limitSeconds);
        if (key == _lastChartKey) return;
        _lastChartKey = key;

        RefreshWeekChart(summary.RecentDays, isTrackMode ? -1 : limitSeconds);
        RefreshSessions(summary.Sessions);
        RefreshExtraStats(summary);
        RefreshHourlyChart(summary.HourlyBreakdown);
    }

    private static string BuildChartKey(UsageSummary s, bool isTrackMode, int limitSeconds)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(isTrackMode); sb.Append('|');
        sb.Append(limitSeconds); sb.Append('|');
        sb.Append(s.SessionCount); sb.Append('|');
        sb.Append(s.TotalSeconds.ToString("F1")); sb.Append('|');
        foreach (var d in s.RecentDays)
            sb.Append(d.Day).Append(':').Append(d.TotalSeconds.ToString("F1")).Append(',');
        sb.Append('|');
        foreach (var h in s.HourlyBreakdown)
            sb.Append(h.TotalSeconds.ToString("F1")).Append(',');
        return sb.ToString();
    }

    private void RefreshExtraStats(UsageSummary summary)
    {
        // 较上周同期变化
        var thisWeek = summary.RecentDays.Sum(d => d.TotalSeconds);
        var prevWeek = summary.PreviousWeekTotalSeconds;

        if (prevWeek > 0)
        {
            var diff = thisWeek - prevWeek;
            var pct = Math.Abs(diff / prevWeek * 100);
            var arrow = diff > 0 ? "↑" : diff < 0 ? "↓" : "→";
            var color = diff > 0 ? AppColors.RedBrush : diff < 0 ? AppColors.GreenBrush : AppColors.MutedBrush;
            WeekCompareLabel.Text = $"{arrow} {pct:F0}%";
            WeekCompareLabel.Foreground = color;
        }
        else if (thisWeek > 0)
        {
            WeekCompareLabel.Text = "新增";
            WeekCompareLabel.Foreground = AppColors.BlueBrush;
        }
        else
        {
            WeekCompareLabel.Text = "无数据";
            WeekCompareLabel.Foreground = AppColors.MutedBrush;
        }

        // 今日最长单次
        var longest = summary.Sessions.Count > 0 ? summary.Sessions.Max(s => s.Seconds) : 0;
        LongestSessionLabel.Text = FormatHelper.FormatSeconds(longest);

        // 今日平均每次
        if (summary.Sessions.Count > 0)
        {
            var avg = summary.Sessions.Average(s => s.Seconds);
            AvgSessionLabel.Text = FormatHelper.FormatSeconds(avg);
        }
        else
        {
            AvgSessionLabel.Text = "暂无记录";
        }
    }

    private void RefreshWeekChart(List<DaySnapshot> days, int limitSeconds)
    {
        WeekChart.Children.Clear();
        WeekChart.ColumnDefinitions.Clear();

        if (days.Count == 0)
        {
            WeekChart.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "还没有可以展示的趋势数据。",
                Foreground = AppColors.MutedBrush
            });
            return;
        }

        var totalSeconds = days.Sum(d => d.TotalSeconds);
        var average = days.Count > 0 ? totalSeconds / days.Count : 0;
        WeekSummaryLabel.Text = $"本周累计 {FormatHelper.FormatSeconds(totalSeconds)} · 日均 {FormatHelper.FormatSeconds(average)}";

        for (int i = 0; i < days.Count; i++)
            WeekChart.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());

        var maxSeconds = Math.Max(days.Max(d => d.TotalSeconds), 1.0);

        for (int i = 0; i < days.Count; i++)
        {
            var item = days[i];
            var ratio = Math.Min(item.TotalSeconds / maxSeconds, 1.0);
            var barHeight = item.TotalSeconds > 0 ? Math.Max(8, 92 * ratio) : 8;

            // 颜色：零值透明，否则按时长比例从绿渐变到红
            System.Windows.Media.Brush color;
            if (item.TotalSeconds <= 0)
            {
                color = System.Windows.Media.Brushes.Transparent;
            }
            else
            {
                // 统一蓝色调，ratio 越高越深
                color = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(56, (byte)(139 - (int)(ratio * 39)), (byte)(155 + (int)(ratio * 100))));
            }

            var cell = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(4, 0, 4, 0) };
            cell.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            cell.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            cell.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            // 背景条
            var barBg = new System.Windows.Controls.Border
            {
                Background = AppColors.SurfaceAltBrush,
                CornerRadius = new System.Windows.CornerRadius(8),
                Width = 24,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Height = 92
            };
            System.Windows.Controls.Grid.SetRow(barBg, 0);
            cell.Children.Add(barBg);

            // 前景条
            var bar = new System.Windows.Controls.Border
            {
                Background = color,
                CornerRadius = new System.Windows.CornerRadius(8),
                Width = 24,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Height = barHeight
            };
            System.Windows.Controls.Grid.SetRow(bar, 0);
            cell.Children.Add(bar);

            var timeLabel = new System.Windows.Controls.TextBlock
            {
                Text = FormatHelper.FormatCompactSeconds(item.TotalSeconds),
                Foreground = item.TotalSeconds > 0 ? AppColors.TextBrush : AppColors.Muted2Brush,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 4, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(timeLabel, 1);
            cell.Children.Add(timeLabel);

            var dateLabel = new System.Windows.Controls.TextBlock
            {
                Text = item.Label,
                Foreground = AppColors.MutedBrush,
                FontSize = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 2, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(dateLabel, 2);
            cell.Children.Add(dateLabel);

            System.Windows.Controls.Grid.SetColumn(cell, i);
            WeekChart.Children.Add(cell);
        }
    }

    private void RefreshHourlyChart(List<HourlyData> hours)
    {
        HourlyChart.Children.Clear();
        HourlyChart.ColumnDefinitions.Clear();

        if (hours.Count == 0)
        {
            HourlyChart.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "还没有今天的记录数据。",
                Foreground = AppColors.MutedBrush
            });
            return;
        }

        var maxSeconds = hours.Max(h => h.TotalSeconds);
        if (maxSeconds <= 0) maxSeconds = 60; // 最小一个刻度

        for (int i = 0; i < 24; i++)
            HourlyChart.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
            {
                Width = new System.Windows.GridLength(40)
            });

        // 汇总信息
        var total = hours.Sum(h => h.TotalSeconds);
        var peakHour = hours.OrderByDescending(h => h.TotalSeconds).First();
        HourlySummaryLabel.Text = $"今日累计 {FormatHelper.FormatSeconds(total)} · 最活跃时段 {peakHour.Hour}:00 - {peakHour.Hour + 1}:00（{FormatHelper.FormatSeconds(peakHour.TotalSeconds)}）";

        for (int i = 0; i < 24; i++)
        {
            var item = hours[i];
            var ratio = maxSeconds > 0 ? Math.Min(item.TotalSeconds / maxSeconds, 1.0) : 0;
            var barHeight = item.TotalSeconds > 0 ? Math.Max(6, 120 * ratio) : 6;

            var cell = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(2, 0, 2, 0) };
            cell.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            cell.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            cell.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            // 背景条
            var barBg = new System.Windows.Controls.Border
            {
                Background = AppColors.SurfaceAltBrush,
                CornerRadius = new System.Windows.CornerRadius(6),
                Width = 28,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Height = 120
            };
            System.Windows.Controls.Grid.SetRow(barBg, 0);
            cell.Children.Add(barBg);

            // 前景条 - 值为 0 时透明，否则按时长从绿渐变到红
            System.Windows.Media.Brush color;
            if (item.TotalSeconds <= 0)
            {
                color = System.Windows.Media.Brushes.Transparent;
            }
            else
            {
                // 统一蓝色调，ratio 越高越深
                color = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(56, (byte)(139 - (int)(ratio * 39)), (byte)(155 + (int)(ratio * 100))));
            }

            var bar = new System.Windows.Controls.Border
            {
                Background = color,
                CornerRadius = new System.Windows.CornerRadius(6),
                Width = 28,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Height = barHeight
            };
            System.Windows.Controls.Grid.SetRow(bar, 0);
            cell.Children.Add(bar);

            // 时间
            var timeLabel = new System.Windows.Controls.TextBlock
            {
                Text = item.TotalSeconds > 0 ? FormatHelper.FormatCompactSeconds(item.TotalSeconds) : "0:00",
                Foreground = item.TotalSeconds > 0 ? AppColors.TextBrush : AppColors.Muted2Brush,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 2, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(timeLabel, 1);
            cell.Children.Add(timeLabel);

            // 小时
            var hourLabel = new System.Windows.Controls.TextBlock
            {
                Text = i < 10 ? $"0{i}" : $"{i}",
                Foreground = AppColors.MutedBrush,
                FontSize = 10,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 1, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(hourLabel, 2);
            cell.Children.Add(hourLabel);

            System.Windows.Controls.Grid.SetColumn(cell, i);
            HourlyChart.Children.Add(cell);
        }
    }

    private void RefreshSessions(List<SessionRecord> sessions)
    {
        SessionsList.Children.Clear();

        var recent = sessions.OrderByDescending(s => s.StartedAt).Take(8).Reverse().ToList();

        if (recent.Count == 0)
        {
            SessionsList.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "今天还没有完整记录。检测到抖音并切走后，会在这里出现一条记录。",
                Foreground = AppColors.MutedBrush,
                Margin = new System.Windows.Thickness(0, 10, 0, 0)
            });
            return;
        }

        foreach (var session in recent)
        {
            var row = new System.Windows.Controls.Border
            {
                Background = AppColors.SurfaceAltBrush,
                CornerRadius = new System.Windows.CornerRadius(8),
                Padding = new System.Windows.Thickness(14, 10, 14, 10),
                Margin = new System.Windows.Thickness(0, 5, 0, 5)
            };

            var stack = new System.Windows.Controls.StackPanel();

            var started = session.StartedAt.Length >= 19 ? session.StartedAt[11..19] : session.StartedAt;
            var ended = session.EndedAt.Length >= 19 ? session.EndedAt[11..19] : session.EndedAt;
            var source = FormatHelper.SourceText(session.Source);
            var seconds = FormatHelper.FormatSeconds(session.Seconds);

            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = $"{started} - {ended} · {source} · {seconds}",
                Foreground = AppColors.TextBrush
            });

            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = string.IsNullOrEmpty(session.WindowTitle) ? "-" : session.WindowTitle,
                Foreground = AppColors.MutedBrush,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                Margin = new System.Windows.Thickness(0, 2, 0, 0)
            });

            row.Child = stack;
            SessionsList.Children.Add(row);
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _exportCsv();
            ExportMessage.Text = $"已导出：{path}";
            ExportMessage.Foreground = AppColors.GreenBrush;
            FileHelper.OpenPath(path);
        }
        catch (Exception ex)
        {
            ExportMessage.Text = $"导出失败：{ex.Message}";
            ExportMessage.Foreground = AppColors.RedBrush;
        }
    }

    /// <summary>鼠标滚轮在水平分布图上时，控制横向滚动（反向）。</summary>
    private void HourlyScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is System.Windows.Controls.ScrollViewer sv)
        {
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset + e.Delta);
            e.Handled = true;
        }
    }
}
