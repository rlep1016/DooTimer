using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using DooTimer.Converters;
using DooTimer.Helpers;
using DooTimer.Models;
using DooTimer.Services;
using DooTimer.Views.Pages;

namespace DooTimer.Views;

public partial class MainWindow : Window
{
    private readonly UsageTracker _tracker;
    private readonly ConfigService _configService;
    private readonly Func<UsageSummary> _getDataSummary;
    private readonly Func<string> _exportCsv;
    private readonly Action _stopApp;

    private readonly OverviewPage _overviewPage;
    private readonly SettingsPage _settingsPage;
    private readonly DataPage _dataPage;
    private readonly AboutPage _aboutPage;

    private readonly Dictionary<string, System.Windows.Controls.Button> _navButtons = [];

    private string _currentPage = "overview";
    private readonly System.Windows.Threading.DispatcherTimer _uiTimer;
    private readonly uint _wakeMsgId;

    private readonly TrayService _trayService;

    // 任务栏覆盖标签
    private readonly Window _overlay;
    private readonly System.Windows.Controls.TextBlock _overlayTextTop;
    private readonly System.Windows.Controls.TextBlock _overlayTextBottom;
    private readonly System.Windows.Threading.DispatcherTimer _overlayTimer;

    // 路径
    private readonly string _baseDir;
    private readonly string _configPath;
    private readonly string _usagePath;
    private readonly string _logPath;
    private readonly string _dataDir;
    private readonly string _exportDir;

    public MainWindow(
        UsageTracker tracker,
        ConfigService configService,
        Func<UsageSummary> getDataSummary,
        Func<string> exportCsv,
        TrayService trayService,
        Action stopApp)
    {
        InitializeComponent();

        _tracker = tracker;
        _configService = configService;
        _getDataSummary = getDataSummary;
        _exportCsv = exportCsv;
        _trayService = trayService;
        _stopApp = stopApp;

        _baseDir = AppContext.BaseDirectory;
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DooTimer");
        _configPath = Path.Combine(appDataDir, "config.json");
        _usagePath = Path.Combine(appDataDir, "data", "usage.json");
        _logPath = Path.Combine(appDataDir, "logs", "dootimer.log");
        _dataDir = Path.Combine(appDataDir, "data");
        _exportDir = Path.Combine(_dataDir, "exports");

        // 创建页面
        _overviewPage = new OverviewPage();

        var config = configService.Load();
        _settingsPage = new SettingsPage(
            config,
            OnSaveSettings,
            StartupService.IsStartupEnabled,
            (enabled) => { if (enabled) StartupService.EnableStartup(); else StartupService.DisableStartup(); },
            StartupService.GetStartupTargetSummary);

        _dataPage = new DataPage(getDataSummary, exportCsv);
        _aboutPage = new AboutPage(exportCsv, _baseDir, _configPath, _usagePath, _logPath);

        _wakeMsgId = NativeMethods.RegisterWindowMessage("DooTimerWakeMainWindow");

        // 创建任务栏覆盖标签（WPF 置顶窗口，透明背景，融入任务栏）
        var isDark = AppColors.IsSystemDarkMode();
        var mutedFg = isDark
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 190))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 110));

        _overlayTextTop = new System.Windows.Controls.TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = mutedFg,
            TextAlignment = TextAlignment.Right,
        };
        _overlayTextBottom = new System.Windows.Controls.TextBlock
        {
            FontSize = 10,
            Foreground = mutedFg,
            TextAlignment = TextAlignment.Right,
        };
        _overlay = new Window
        {
            Content = new System.Windows.Controls.Border
            {
                Background = null,
                Padding = new Thickness(0, 0, 8, 0),
                Child = new System.Windows.Controls.StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { _overlayTextTop, _overlayTextBottom }
                },
            },
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.Manual,
        };
        _overlay.Show();
        // 设置 WS_EX_TOOLWINDOW 样式，让覆盖窗口不出现在 Alt+Tab 切换列表中
        var overlayHandle = new WindowInteropHelper(_overlay).Handle;
        var exStyle = NativeMethods.GetWindowLongPtr(overlayHandle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(overlayHandle, NativeMethods.GWL_EXSTYLE,
            new IntPtr(exStyle.ToInt64() | NativeMethods.WS_EX_TOOLWINDOW));
        _overlayTextTop.Text = "今日抖音";
        _overlayTextBottom.Text = "";
        PositionOverlay();

        // 每 30ms 强制置顶，确保不被任务栏盖住（低间隔减少闪烁）
        _overlayTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _overlayTimer.Tick += (_, _) =>
        {
            if (!_overlay.IsVisible) return;

            // 强制置顶
            var h = new WindowInteropHelper(_overlay);
            NativeMethods.SetWindowPos(h.Handle, new IntPtr(-1),
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            // 自适应位置：通知区域变化时跟随调整
            var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero) return;
            var trayNotify = NativeMethods.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (trayNotify != IntPtr.Zero)
            {
                NativeMethods.GetWindowRect(trayNotify, out var trayRect);
                double newLeft = trayRect.Left - 210 - 4;
                if (Math.Abs(_overlay.Left - newLeft) > 2)
                    _overlay.Left = newLeft;
            }
        };
        _overlayTimer.Start();

        // 响应第二个实例发来的唤醒消息
        SourceInitialized += (_, _) =>
        {
            ((HwndSource)PresentationSource.FromVisual(this)).AddHook(WndProc);
        };

        // 导航按钮映射
        _navButtons["overview"] = BtnOverview;
        _navButtons["settings"] = BtnSettings;
        _navButtons["data"] = BtnData;
        _navButtons["about"] = BtnAbout;

        // 设置窗口图标
        Icon = TrayService.LoadIcon().ToImageSource();

        ShowPage("overview");

        // UI 定时刷新（100ms）
        _uiTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _uiTimer.Tick += OnUiTick;
        _uiTimer.Start();
    }

    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnUiTick(object? sender, EventArgs e)
    {
        var snapshot = _tracker.Snapshot;

        // 更新侧边栏状态
        if (snapshot.IsDouyin)
        {
            SideStatusText.Text = snapshot.IsTrackMode ? "正在刷抖音" : "正在检测到抖音";
            SideStatusText.Foreground = AppColors.RedBrush;
            SideStatus.Background = AppColors.RedSoftBrush;
        }
        else
        {
            SideStatusText.Text = snapshot.IsTrackMode ? "统计中" : "未检测到抖音";
            SideStatusText.Foreground = AppColors.GreenBrush;
            SideStatus.Background = AppColors.GreenSoftBrush;
        }

        // 更新当前页面
        if (_currentPage == "overview")
            _overviewPage.Refresh(snapshot);
        else if (_currentPage == "data")
            _dataPage.RefreshWithLimit(snapshot.LimitSeconds, snapshot.IsTrackMode);

        // 更新任务栏标签文字（两行：上=用时，下=剩余）
        var used = FormatHelper.FormatSeconds(snapshot.UsedSeconds);
        var isDark = AppColors.IsSystemDarkMode();
        var normalFg = isDark
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 190))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 110));

        if (snapshot.IsDouyin && !snapshot.IsTrackMode)
        {
            var remaining = Math.Max(0, snapshot.LimitSeconds - snapshot.UsedSeconds);
            var activeClr = snapshot.UsedSeconds >= snapshot.LimitSeconds
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 40, 40))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 140, 80));

            _overlayTextTop.Text = $"抖音 {used}";
            _overlayTextTop.Foreground = activeClr;
            _overlayTextBottom.Text = $"剩余 {FormatHelper.FormatSeconds(remaining)}";
            _overlayTextBottom.Foreground = activeClr;
        }
        else
        {
            _overlayTextTop.Text = $"今日抖音";
            _overlayTextTop.Foreground = normalFg;
            _overlayTextBottom.Text = used;
            _overlayTextBottom.Foreground = normalFg;
        }

        // 托盘 hover 文字
        var usedCompact = FormatHelper.FormatCompactSeconds(snapshot.UsedSeconds);
        _trayService.UpdateTooltipText(snapshot.IsDouyin
            ? $"DooTimer · {usedCompact}"
            : $"DooTimer · 今日已用 {usedCompact}");
    }

    private void NavClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string page)
            ShowPage(page);
    }

    private void ShowPage(string page)
    {
        _currentPage = page;

        var titles = new Dictionary<string, (string title, string subtitle)>
        {
            ["overview"] = ("总览", "实时查看检测状态、今日用时和剩余额度。"),
            ["settings"] = ("设置", "调整检测规则和提醒阈值，保存后立即生效。"),
            ["data"] = ("数据", "查看今天的累计数据和最近使用记录。"),
            ["about"] = ("关于", "查看版本、打开文件位置，并快速定位日志与数据。")
        };

        if (titles.TryGetValue(page, out var info))
        {
            HeaderTitle.Text = info.title;
            HeaderSubtitle.Text = info.subtitle;
        }

        // 更新导航按钮样式
        foreach (var (name, btn) in _navButtons)
        {
            if (name == page)
                btn.Style = (Style)FindResource("ActiveSidebarButton");
            else
                btn.Style = (Style)FindResource("SidebarButton");
        }

        // 切换页面
        PageContent.Content = page switch
        {
            "settings" => _settingsPage,
            "data" => _dataPage,
            "about" => _aboutPage,
            _ => _overviewPage
        };

        // 刷新页面
        if (page == "settings")
            _settingsPage.RefreshStartupControls();
        else if (page == "data")
            _dataPage.RefreshWithLimit(_tracker.Snapshot.LimitSeconds, _tracker.Snapshot.IsTrackMode);
        else if (page == "about")
            _aboutPage.RefreshHealth();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _wakeMsgId)
        {
            ShowWindow();
            handled = true;
            return (IntPtr)1;
        }
        return IntPtr.Zero;
    }

    private void OnSaveSettings(AppConfig newConfig)
    {
        _configService.Save(newConfig);
        _tracker.UpdateConfig(newConfig);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _stopApp();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // 隐藏而不是关闭
        e.Cancel = true;
        Hide();
    }

    private IntPtr _shrunkenTaskList; // MSTaskSwWClass 句柄，退出时恢复
    private int _taskListOrigWidth;

    private void PositionOverlay()
    {
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero) return;
        NativeMethods.GetWindowRect(taskbar, out var tbRect);

        // 找到通知区域
        var trayNotify = NativeMethods.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        int trayLeft = tbRect.Right - 200;
        if (trayNotify != IntPtr.Zero)
        {
            NativeMethods.GetWindowRect(trayNotify, out var trayRect);
            trayLeft = trayRect.Left;
        }

        int overlayWidth = 210;
        _overlay.Left = trayLeft - overlayWidth - 4;
        _overlay.Top = tbRect.Top;
        _overlay.Height = (tbRect.Bottom - tbRect.Top);
        _overlay.Width = overlayWidth;

        // TrafficMonitor 风格：缩小 MSTaskSwWClass 来腾空间
        var reBar = NativeMethods.FindWindowEx(taskbar, IntPtr.Zero, "ReBarWindow32", null);
        if (reBar == IntPtr.Zero)
            reBar = NativeMethods.FindWindowEx(taskbar, IntPtr.Zero, "WorkerW", null);

        if (reBar != IntPtr.Zero)
        {
            var taskList = NativeMethods.FindWindowEx(reBar, IntPtr.Zero, "MSTaskSwWClass", null);
            if (taskList == IntPtr.Zero)
                taskList = NativeMethods.FindWindowEx(reBar, IntPtr.Zero, "MSTaskListWClass", null);

            if (taskList != IntPtr.Zero)
            {
                NativeMethods.GetWindowRect(taskList, out var tlRect);
                NativeMethods.GetWindowRect(reBar, out var rbRect);
                _shrunkenTaskList = taskList;
                _taskListOrigWidth = tlRect.Right - tlRect.Left;

                // 缩小 MSTaskSwWClass，腾出空间给标签
                int newWidth = _taskListOrigWidth - overlayWidth;
                if (newWidth > 100)
                {
                    NativeMethods.SetWindowPos(taskList, IntPtr.Zero,
                        0, 0,
                        newWidth, tlRect.Bottom - tlRect.Top,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER);
                }
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiTimer.Stop();
        _overlayTimer.Stop();
        _overlay.Close();

        // 恢复 MSTaskSwWClass 原始大小
        if (_shrunkenTaskList != IntPtr.Zero && _taskListOrigWidth > 0)
        {
            NativeMethods.GetWindowRect(_shrunkenTaskList, out var tlRect);
            NativeMethods.SetWindowPos(_shrunkenTaskList, IntPtr.Zero,
                0, 0,
                _taskListOrigWidth, tlRect.Bottom - tlRect.Top,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER);
        }

        base.OnClosed(e);
    }
}

// 扩展方法：System.Drawing.Icon → ImageSource
public static class IconExtensions
{
    public static ImageSource ToImageSource(this System.Drawing.Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }
}
