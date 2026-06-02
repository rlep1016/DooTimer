using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DooTimer.Converters;
using DooTimer.Helpers;
using DooTimer.Services;

namespace DooTimer.Views.Pages;

public partial class AboutPage : System.Windows.Controls.UserControl
{
    private readonly Func<string> _exportCsv;
    private readonly UpdateService _updateService;

    // 更新 UI 元素
    private Border? _updateBorder;
    private TextBlock? _updateStatusText;
    private System.Windows.Controls.Button? _updateButton;

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
        string logPath,
        UpdateService updateService)
    {
        InitializeComponent();
        _exportCsv = exportCsv;
        _updateService = updateService;
        _projectDir = projectDir;
        _configPath = configPath;
        _usagePath = usagePath;
        _logPath = logPath;
        _dataDir = Path.GetDirectoryName(usagePath) ?? "";
        _logDir = Path.GetDirectoryName(logPath) ?? "";
        _exportDir = Path.Combine(_dataDir, "exports");

        BuildUpdateSection();
        _updateService.StateChanged += () => Dispatcher.Invoke(RefreshUpdateUI);
    }

    private void BuildUpdateSection()
    {
        _updateBorder = new Border
        {
            Background = AppColors.SurfaceBrush,
            CornerRadius = new CornerRadius(14),
            BorderBrush = AppColors.LineBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(22)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "软件更新",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = AppColors.TextBrush,
            Margin = new Thickness(0, 20, 0, 4)
        });

        _updateStatusText = new TextBlock
        {
            Text = "点击按钮检查更新",
            Foreground = AppColors.MutedBrush,
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(_updateStatusText);

        _updateButton = new System.Windows.Controls.Button
        {
            Content = "检查更新",
            Height = 38,
            Style = System.Windows.Application.Current.Resources["RoundedButton"] as Style,
            Background = AppColors.BlueBrush,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 18)
        };
        _updateButton.Click += UpdateButton_Click;
        stack.Children.Add(_updateButton);

        _updateBorder.Child = stack;

        // Content 是 ScrollViewer，需要取出里面的 Grid
        var mainGrid = (Content as ScrollViewer)?.Content as System.Windows.Controls.Grid;
        if (mainGrid != null)
        {
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            foreach (var child in mainGrid.Children.OfType<Border>())
            {
                var row = System.Windows.Controls.Grid.GetRow(child);
                if (row >= 2) System.Windows.Controls.Grid.SetRow(child, row + 1);
            }
            System.Windows.Controls.Grid.SetRow(_updateBorder, 2);
            mainGrid.Children.Add(_updateBorder);
        }
    }

    private void RefreshUpdateUI()
    {
        if (_updateStatusText == null || _updateButton == null) return;

        var info = _updateService.Info;

        switch (info.State)
        {
            case UpdateState.Idle:
                _updateStatusText.Text = "点击按钮检查更新";
                _updateStatusText.Foreground = AppColors.MutedBrush;
                _updateButton.Content = "检查更新";
                _updateButton.Background = AppColors.BlueBrush;
                _updateButton.IsEnabled = true;
                break;

            case UpdateState.Checking:
                _updateStatusText.Text = "正在检查...";
                _updateStatusText.Foreground = AppColors.MutedBrush;
                _updateButton.Content = "检查中...";
                _updateButton.IsEnabled = false;
                break;

            case UpdateState.UpToDate:
                _updateStatusText.Text = info.Message;
                _updateStatusText.Foreground = AppColors.GreenBrush;
                _updateButton.Content = "检查更新";
                _updateButton.IsEnabled = true;
                break;

            case UpdateState.Available:
                _updateStatusText.Text = info.Message;
                _updateStatusText.Foreground = AppColors.BlueBrush;
                _updateButton.Content = "下载更新";
                _updateButton.IsEnabled = true;
                break;

            case UpdateState.Downloading:
                _updateStatusText.Text = info.Message;
                _updateStatusText.Foreground = AppColors.BlueBrush;
                _updateButton.Content = "下载中...";
                _updateButton.IsEnabled = false;
                break;

            case UpdateState.Downloaded:
                _updateStatusText.Text = info.Message;
                _updateStatusText.Foreground = AppColors.GreenBrush;
                _updateButton.Content = "安装更新";
                _updateButton.Background = AppColors.GreenBrush;
                _updateButton.IsEnabled = true;
                break;

            case UpdateState.Error:
                _updateStatusText.Text = info.Message;
                _updateStatusText.Foreground = AppColors.RedBrush;
                _updateButton.Content = "重试";
                _updateButton.IsEnabled = true;
                break;
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var state = _updateService.Info.State;

        switch (state)
        {
            case UpdateState.Idle:
            case UpdateState.UpToDate:
            case UpdateState.Error:
                await _updateService.CheckAsync();
                break;
            case UpdateState.Available:
                await _updateService.DownloadAsync();
                break;
            case UpdateState.Downloaded:
                _updateService.Info.InstallAction?.Invoke();
                break;
        }
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
            Margin = new Thickness(0, 0, 0, 12)
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
