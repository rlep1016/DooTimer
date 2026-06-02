using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DooTimer.Converters;
using DooTimer.Helpers;

namespace DooTimer.Views.Pages;

public partial class AboutPage : System.Windows.Controls.UserControl
{
    private readonly Func<string> _exportCsv;

    // 路径将在初始化时传入
    private readonly string _projectDir;
    private readonly string _configPath;
    private readonly string _usagePath;
    private readonly string _logPath;
    private readonly string _dataDir;
    private readonly string _logDir;
    private readonly string _exportDir;

    public AboutPage(
        Func<string> exportCsv,
        string projectDir,
        string configPath,
        string usagePath,
        string logPath)
    {
        InitializeComponent();
        _exportCsv = exportCsv;
        _projectDir = projectDir;
        _configPath = configPath;
        _usagePath = usagePath;
        _logPath = logPath;
        _dataDir = Path.GetDirectoryName(usagePath) ?? "";
        _logDir = Path.GetDirectoryName(logPath) ?? "";
        _exportDir = Path.Combine(_dataDir, "exports");
    }

    public void RefreshHealth()
    {
        HealthList.Children.Clear();

        AddHealthItem("配置文件", _configPath,
            "保存每日上限、检测关键词和提醒规则。");
        AddHealthItem("数据文件", _usagePath,
            "保存每天累计用时和会话记录。");
        AddHealthItem("导出文件", Path.Combine(_exportDir, "dootimer_usage.csv"),
            "CSV 表格导出结果，导出后生成。");
        AddHealthItem("日志文件", _logPath,
            "记录启动、停止和异常信息。");
    }

    private void AddHealthItem(string name, string path, string detail)
    {
        var exists = File.Exists(path);
        var statusText = exists ? "正常" : "缺失";
        var statusColor = exists ? AppColors.GreenBrush : AppColors.YellowBrush;
        var statusBg = exists ? AppColors.GreenSoftBrush : AppColors.YellowSoftBrush;

        var border = new Border
        {
            Background = AppColors.SurfaceAltBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 5, 0, 5)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        headerStack.Children.Add(new TextBlock
        {
            Text = $"{name} · {FormatHelper.FormatFileSize(path)}",
            Foreground = AppColors.TextBrush,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var statusBorder = new Border
        {
            Background = statusBg,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(10, 0, 0, 0)
        };
        statusBorder.Child = new TextBlock
        {
            Text = statusText,
            Foreground = statusColor,
            FontSize = 12,
            FontWeight = FontWeights.Bold
        };
        headerStack.Children.Add(statusBorder);
        Grid.SetColumn(headerStack, 0);
        grid.Children.Add(headerStack);

        var detailText = new TextBlock
        {
            Text = $"{detail}\n{path}",
            Foreground = AppColors.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        };
        Grid.SetColumn(detailText, 0);
        Grid.SetRow(detailText, 1);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(headerStack, 0);
        Grid.SetRow(detailText, 1);
        grid.Children.Add(detailText);

        border.Child = grid;
        HealthList.Children.Add(border);
    }

    private void OpenProjectDir_Click(object sender, RoutedEventArgs e) => FileHelper.OpenPath(_projectDir);
    private void OpenConfig_Click(object sender, RoutedEventArgs e) => FileHelper.OpenPath(_configPath);
    private void OpenData_Click(object sender, RoutedEventArgs e) => FileHelper.OpenPath(_usagePath);
    private void OpenLog_Click(object sender, RoutedEventArgs e) => FileHelper.OpenPath(_logPath);
    private void OpenDataDir_Click(object sender, RoutedEventArgs e) => FileHelper.OpenPath(_dataDir);
    private void OpenLogDir_Click(object sender, RoutedEventArgs e) => FileHelper.OpenPath(_logDir);
    private void OpenExportDir_Click(object sender, RoutedEventArgs e) => FileHelper.OpenPath(_exportDir);

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _exportCsv();
            FileHelper.OpenPath(path);
        }
        catch
        {
            // 忽略导出错误
        }
    }
}
