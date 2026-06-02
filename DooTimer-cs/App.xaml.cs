using System.Windows;
using DooTimer.Models;
using DooTimer.Services;
using DooTimer.Views;
using DooTimer.Helpers;

namespace DooTimer;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;
    private UsageTracker? _tracker;
    private TrayService? _trayService;
    private UpdateService? _updateService;
    private MainWindow? _mainWindow;

    private string BaseDir => AppContext.BaseDirectory;
    private string DataDir => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DooTimer");
    private string ConfigPath => System.IO.Path.Combine(DataDir, "config.json");
    private string UsagePath => System.IO.Path.Combine(DataDir, "data", "usage.json");
    private string LogPath => System.IO.Path.Combine(DataDir, "logs", "dootimer.log");
    private string ExportPath => System.IO.Path.Combine(DataDir, "data", "exports", "dootimer_usage.csv");

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        SetupLogging();

        _singleInstance = new SingleInstanceService();
        try
        {
            _singleInstance.Acquire();
        }
        catch (AlreadyRunningException)
        {
            System.Diagnostics.Debug.WriteLine("DooTimer is already running");
            SingleInstanceService.RequestExistingInstanceToShow();
            new Notifier().Notify("DooTimer 已在运行", "已尝试打开正在运行的面板，避免重复计时。");
            Shutdown();
            return;
        }

        try
        {
            StartApp();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DooTimer crashed: {ex}");
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(BaseDir, "logs", "crash.log"), ex.ToString()); }
            catch { }
            try { new Notifier().Notify("DooTimer 发生错误", $"程序遇到错误：{ex.Message}"); }
            catch { }
            Shutdown();
        }
    }

    private void StartApp()
    {
        // 迁移旧配置文件：从 exe 目录 → AppData 目录（v1.x → v2.x）
        var oldConfigPath = System.IO.Path.Combine(BaseDir, "config.json");
        if (System.IO.File.Exists(oldConfigPath) && !System.IO.File.Exists(ConfigPath))
        {
            var dir = System.IO.Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.Copy(oldConfigPath, ConfigPath);
        }

        var configService = new ConfigService(ConfigPath);
        AppConfig config;
        try { config = configService.Load(); }
        catch (ConfigException ex)
        {
            new Notifier().Notify("DooTimer 配置已修复", ex.Message);
            config = configService.Load();
        }

        // 应用主题
        var resolvedTheme = DooTimer.Converters.AppColors.ResolveTheme(config.Theme);
        DooTimer.Converters.AppColors.ApplyTheme(resolvedTheme);

        var storage = new UsageStorage(UsagePath);
        var monitor = new ForegroundMonitor(config);
        var notifier = new Notifier();

        _tracker = new UsageTracker(config, monitor, storage, notifier);

        Func<UsageSummary> getDataSummary = () =>
            _tracker.GetDataSummary(DateTime.Now.ToString("yyyy-MM-dd"));

        Func<string> exportCsv = () => _tracker.ExportCsv(ExportPath);

        _trayService = new TrayService(
            () =>
            {
                var s = _tracker.Snapshot;
                var time = FormatHelper.FormatSeconds(s.UsedSeconds);
                return s.IsTrackMode
                    ? $"今日抖音：{time}"
                    : $"今日抖音：{time} / {s.LimitMinutes} 分钟";
            },
            () => _mainWindow?.Show(),
            () => _tracker?.Stop(),
            ConfigPath,
            UsagePath
        );

        // 注入托盘气泡通知，替代 MessageBox
        notifier.SetNotificationAction((title, msg) => _trayService.ShowBalloonTip(title, msg));

        // 日志回调（供各服务共用）
        Action<string, string> logDebug = (tag, msg) =>
            System.IO.File.AppendAllText(
                LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{tag}] {msg}\n");

        // 初始化更新服务
        _updateService = new UpdateService(logDebug);

        Action stopApp = () => { _tracker?.Stop(); _tracker?.Flush(); };

        _mainWindow = new MainWindow(_tracker, configService, getDataSummary, exportCsv, _trayService, stopApp, _updateService);

        _tracker.Start();
        _trayService.Show();

        // 延迟 3 秒后清理旧安装包 + 检查更新（不阻塞启动）
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);

            // 清理临时目录中的旧安装包
            try
            {
                var updateDir = Path.Combine(Path.GetTempPath(), "DooTimer", "update");
                if (Directory.Exists(updateDir))
                {
                    foreach (var f in Directory.GetFiles(updateDir, "*.exe"))
                    {
                        try { File.Delete(f); }
                        catch { /* 文件可能正在使用 */ }
                    }
                }
            }
            catch { }

            await _updateService.CheckAsync();
        });

        // 开机启动时不显示窗口，只在托盘运行
        var args = Environment.GetCommandLineArgs();
        if (!args.Contains("--minimized"))
            _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tracker?.Dispose();
        _singleInstance?.Dispose();
        _trayService?.Dispose();
        base.OnExit(e);
    }

    private void SetupLogging()
    {
        var logDir = System.IO.Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrEmpty(logDir))
            System.IO.Directory.CreateDirectory(logDir);
    }
}
