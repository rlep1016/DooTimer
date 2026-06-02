using System.Diagnostics;
using DooTimer.Helpers;
using DooTimer.Models;

namespace DooTimer.Services;

public class UsageTracker : IDisposable
{
    private const double MaxElapsedSeconds = 5.0;
    private const double IdlePollIntervalSeconds = 0.5;

    private AppConfig _config;
    private readonly ForegroundMonitor _monitor;
    private readonly UsageStorage _storage;
    private readonly Notifier _notifier;
    private readonly System.Timers.Timer _timer;
    private readonly object _snapshotLock = new();

    private DateTime _lastTick;
    private string _todayKey;
    private double _todayUsedSeconds;
    private double _pendingSeconds;
    private DateTime _lastStorageFlush;
    private DateTime? _activeSessionStartedAt;
    private ForegroundTarget? _activeTarget;

    // 提醒状态内存缓存（避免每次读文件）
    private bool _reminderPercentSent;
    private bool _reminderLimitSent;
    private string? _reminderLastOverLimit;

    // 统计模式下休息提醒累计
    private double _restAccumulatedSeconds;

    // 线程安全的快照
    private DashboardSnapshot _snapshot;

    public UsageTracker(AppConfig config, ForegroundMonitor monitor, UsageStorage storage, Notifier notifier)
    {
        _config = config;
        _monitor = monitor;
        _storage = storage;
        _notifier = notifier;
        _todayKey = TodayKey();
        _todayUsedSeconds = storage.GetTotalSeconds(_todayKey);
        _snapshot = CreateSnapshot(ForegroundTarget.Empty);
        _lastTick = DateTime.Now;
        _lastStorageFlush = DateTime.Now;

        LoadReminderState();

        // 启动日志
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DooTimer", "logs");
            Directory.CreateDirectory(logDir);
            var startupLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [STARTUP] mode={config.Mode} forceClose={config.ForceCloseEnabled} clientProcesses=[{string.Join(",", config.ClientProcessNames)}] browserProcesses=[{string.Join(",", config.BrowserProcessNames)}] keywords=[{string.Join(",", config.TitleKeywords)}]";
            File.AppendAllText(Path.Combine(logDir, "dootimer.log"), startupLine + Environment.NewLine);
        }
        catch { }

        _timer = new System.Timers.Timer(config.PollIntervalSeconds * 1000);
        _timer.AutoReset = true;
        _timer.Elapsed += OnTick;
    }

    public DashboardSnapshot Snapshot
    {
        get
        {
            lock (_snapshotLock)
                return _snapshot;
        }
    }

    public double TodayUsedSeconds
    {
        get
        {
            lock (_snapshotLock)
                return _todayUsedSeconds;
        }
    }

    public int PendingSessionCount => _storage.PendingSessionCount;

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    public void UpdateConfig(AppConfig config)
    {
        LogDebug("CONFIG", "UpdateConfig: mode={0}, forceClose={1}, limit={2}min",
            config.Mode, config.ForceCloseEnabled, config.DailyLimitMinutes);
        _config = config;
        _monitor.UpdateConfig(config);
        _timer.Interval = config.PollIntervalSeconds * 1000;
        _restAccumulatedSeconds = 0;
    }

    public void Flush()
    {
        FinishActiveSession(DateTime.Now);
        _storage.FlushSessions();
    }

    public UsageSummary GetDataSummary(string today)
    {
        var summary = _storage.GetDaySummary(today);
        lock (_snapshotLock)
        {
            if (today == _todayKey)
                summary.TotalSeconds = _todayUsedSeconds;
        }
        summary.RecentDays = _storage.GetRecentDays(today, 7);
        if (summary.RecentDays.Count > 0 && summary.RecentDays[^1].Day == today)
            summary.RecentDays[^1].TotalSeconds = summary.TotalSeconds;
        summary.PreviousWeekTotalSeconds = _storage.GetPreviousWeekTotal(today);
        summary.HourlyBreakdown = _storage.GetHourlyBreakdown(today).Select((seconds, h) => new HourlyData
        {
            Hour = h,
            Label = $"{h}:00",
            TotalSeconds = seconds
        }).ToList();
        return summary;
    }

    public string ExportCsv(string outputPath)
    {
        return _storage.ExportCsv(outputPath);
    }

    private void OnTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var now = DateTime.Now;
        var elapsed = Math.Max(0, (now - _lastTick).TotalSeconds);

        // 休眠/唤醒保护
        if (elapsed > MaxElapsedSeconds)
        {
            FinishActiveSession(now);
            FlushPendingSeconds();
            _pendingSeconds = 0;
            _restAccumulatedSeconds = 0;
            _lastTick = now;

            var target = _monitor.Inspect();
            UpdateStatus(target, now);
            return;
        }

        _lastTick = now;

        var target2 = _monitor.Inspect();

        if (target2.IsDouyin)
        {
            StartOrContinueSession(target2, now);
            AddUsageSeconds(now, elapsed);
            CheckReminders(now);
            CheckRestReminder(now);
        }
        else
        {
            FlushPendingSeconds();
            FinishActiveSession(now);
            _restAccumulatedSeconds = 0;
        }

        UpdateStatus(target2, now);

        // 自适应轮询
        var interval = target2.IsDouyin
            ? _config.PollIntervalSeconds * 1000
            : IdlePollIntervalSeconds * 1000;

        if (Math.Abs(_timer.Interval - interval) > 1)
            _timer.Interval = interval;
    }

    private void AddUsageSeconds(DateTime now, double seconds)
    {
        RolloverDayIfNeeded(now);
        var clamped = Math.Max(0, seconds);
        lock (_snapshotLock)
        {
            _todayUsedSeconds = Math.Round(_todayUsedSeconds + clamped, 3);
        }
        _pendingSeconds = Math.Round(_pendingSeconds + clamped, 3);

        // 统计模式休息提醒累计
        if (_config.Mode == "track" && _config.RestReminderMinutes > 0)
        {
            _restAccumulatedSeconds = Math.Round(_restAccumulatedSeconds + clamped, 3);
        }

        if ((now - _lastStorageFlush).TotalSeconds >= 30.0)
            FlushStorage();
    }

    private void FlushPendingSeconds()
    {
        if (_pendingSeconds <= 0) return;

        var seconds = _pendingSeconds;
        _pendingSeconds = 0;
        _storage.AddSeconds(_todayKey, seconds);
        _lastStorageFlush = DateTime.Now;
    }

    private void FlushStorage()
    {
        FlushPendingSeconds();
        _storage.FlushSessions();
    }

    private void RolloverDayIfNeeded(DateTime now)
    {
        var today = TodayKey(now);
        if (today == _todayKey) return;

        FlushPendingSeconds();
        _storage.FlushDayData(); // 跨天前确保旧日期的缓存写入磁盘
        lock (_snapshotLock)
        {
            _todayKey = today;
            _todayUsedSeconds = _storage.GetTotalSeconds(today);
        }
        _pendingSeconds = 0;
        _restAccumulatedSeconds = 0;
        LoadReminderState();
    }

    private void StartOrContinueSession(ForegroundTarget target, DateTime now)
    {
        if (_activeSessionStartedAt == null)
        {
            _activeSessionStartedAt = now;
            _activeTarget = target;
            return;
        }

        if (_activeTarget != null &&
            (_activeTarget.Source != target.Source ||
             _activeTarget.ProcessName != target.ProcessName ||
             _activeTarget.WindowTitle != target.WindowTitle))
        {
            FinishActiveSession(now);
            _activeSessionStartedAt = now;
            _activeTarget = target;
        }
    }

    private void FinishActiveSession(DateTime endedAt)
    {
        if (_activeSessionStartedAt == null || _activeTarget == null) return;

        _storage.AddSession(
            TodayKey(_activeSessionStartedAt.Value),
            _activeSessionStartedAt.Value,
            endedAt,
            _activeTarget.Source,
            _activeTarget.ProcessName,
            _activeTarget.WindowTitle);

        _activeSessionStartedAt = null;
        _activeTarget = null;
    }

    private DateTime _lastReminderLog;

    private void CheckReminders(DateTime now)
    {
        // 每 30 秒输出一次状态诊断日志
        if ((now - _lastReminderLog).TotalSeconds >= 30)
        {
            _lastReminderLog = now;
            LogDebug("DIAG", "CheckReminders running: mode={0}, forceClose={1}, used={2}s, limit={3}s, percentSent={4}, limitSent={5}, lastOverLimit={6}",
                _config.Mode, _config.ForceCloseEnabled,
                _todayUsedSeconds, _config.DailyLimitSeconds,
                _reminderPercentSent, _reminderLimitSent, _reminderLastOverLimit ?? "null");
        }

        // 统计模式不提醒
        if (_config.Mode == "track") return;

        var today = TodayKey(now);
        double usedSeconds;
        lock (_snapshotLock)
        {
            usedSeconds = today == _todayKey ? _todayUsedSeconds : _storage.GetTotalSeconds(today);
        }

        var limitSeconds = _config.DailyLimitSeconds;

        // 百分比提醒
        if (usedSeconds >= _config.RemindAtSeconds && !_reminderPercentSent)
        {
            _reminderPercentSent = true;
            _storage.SetReminder(today, "percent", now.ToString("yyyy-MM-ddTHH:mm:ss"));
            _notifier.Notify("DooTimer 提醒",
                $"今天刷抖音已经达到 {_config.RemindAtPercent}%：{FormatHelper.FormatSeconds(usedSeconds)}。");
        }

        // 上限提醒
        if (usedSeconds >= limitSeconds && !_reminderLimitSent)
        {
            var timestamp = now.ToString("yyyy-MM-ddTHH:mm:ss");
            _reminderLimitSent = true;
            _reminderLastOverLimit = timestamp;
            _storage.SetReminder(today, "limit", timestamp);
            _storage.SetReminder(today, "last_over_limit", timestamp);
            _notifier.Notify("DooTimer 今日额度已用完",
                $"今天的 {_config.DailyLimitMinutes} 分钟抖音时间已经用完了。先停一下吧。");

            // 强制关闭抖音客户端（如果开启了设置）
            if (_config.ForceCloseEnabled)
            {
                LogDebug("REMINDER", "首次达到额度上限，触发强制关闭。usedSeconds={0}, limitSeconds={1}",
                    usedSeconds, limitSeconds);
                KillDouyinProcesses();
            }
            else
            {
                LogDebug("REMINDER", "首次达到额度上限，但 forceCloseEnabled=false，跳过强制关闭。");
            }

            return;
        }

        // 超限重复提醒
        if (usedSeconds >= limitSeconds && _reminderLastOverLimit != null)
        {
            if (!DateTime.TryParse(_reminderLastOverLimit, out var lastTime))
            {
                // 数据损坏，重置状态
                _reminderLastOverLimit = null;
                return;
            }
            if ((now - lastTime).TotalSeconds >= _config.OverLimitReminderSeconds)
            {
                var timestamp = now.ToString("yyyy-MM-ddTHH:mm:ss");
                _reminderLastOverLimit = timestamp;
                _storage.SetReminder(today, "last_over_limit", timestamp);
                _notifier.Notify("DooTimer 超时提醒",
                    $"你今天已经刷了 {FormatHelper.FormatSeconds(usedSeconds)}，超过设定上限了。");

                // 强制关闭抖音客户端（每次重复提醒时都检查，防止用户重新打开）
                if (_config.ForceCloseEnabled)
                {
                    LogDebug("REMINDER", "超限重复提醒触发强制关闭。usedSeconds={0}, limitSeconds={1}, elapsedSinceLastKill={2}s",
                        usedSeconds, limitSeconds, (now - lastTime).TotalSeconds);
                    KillDouyinProcesses();
                }
            }
        }
    }

    /// <summary>统计模式下连续刷 N 分钟后提醒休息。</summary>
    private void CheckRestReminder(DateTime now)
    {
        if (_config.Mode != "track" || _config.RestReminderMinutes <= 0) return;

        if (_restAccumulatedSeconds >= _config.RestReminderSeconds)
        {
            _restAccumulatedSeconds = 0;
            _notifier.Notify("DooTimer 休息提醒",
                $"你已经连续刷了 {_config.RestReminderMinutes} 分钟抖音，起来活动一下吧。");
        }
    }

    /// <summary>强制关闭抖音客户端进程 + 浏览器中显示抖音的窗口。</summary>
    private void KillDouyinProcesses()
    {
        LogDebug("KILL", "KillDouyinProcesses called. forceCloseEnabled={0}, clientNames=[{1}]",
            _config.ForceCloseEnabled, string.Join(",", _config.ClientProcessNames));

        // 1. 枚举所有进程，杀光所有名称含配置关键词的进程
        //    用 Contains 而非精确匹配，因为抖音可能有 douyin.exe、douyin_tray.exe 等多进程
        foreach (var processName in _config.ClientProcessNames)
        {
            try
            {
                var cleanName = processName.Replace(".exe", "").ToLowerInvariant();
                LogDebug("KILL", "  Scanning ALL processes for keyword: '{0}'", cleanName);
                int killed = 0;
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        var name = proc.ProcessName.ToLowerInvariant();
                        if (!name.Contains(cleanName)) continue;

                        LogDebug("KILL", "  Killing process: Id={0}, Name={1}, MainWindowTitle='{2}'",
                            proc.Id, proc.ProcessName, proc.MainWindowTitle);
                        proc.Kill();
                        proc.WaitForExit(3000);
                        LogDebug("KILL", "  Process killed successfully: Id={0}", proc.Id);
                        killed++;
                    }
                    catch (Exception ex)
                    {
                        LogDebug("KILL", "  Kill FAILED for Id={0} ({1}): {2}",
                            proc.Id, proc.ProcessName, ex.Message);
                    }
                }
                LogDebug("KILL", "  Killed {0} process(es) matching '{1}'", killed, cleanName);
            }
            catch (Exception ex)
            {
                LogDebug("KILL", "  Error scanning processes for '{0}': {1}", processName, ex.Message);
            }
        }

        // 2. 浏览器标签页不强制关闭（风险太大，WM_CLOSE 会关掉整个浏览器窗口，丢失所有标签页）
        LogDebug("KILL", "  Browser tabs not closed (too risky — would close entire browser).");
    }

    private void UpdateStatus(ForegroundTarget target, DateTime now)
    {
        RolloverDayIfNeeded(now);
        var snapshot = CreateSnapshot(target);
        lock (_snapshotLock)
        {
            _snapshot = snapshot;
        }
    }

    private DashboardSnapshot CreateSnapshot(ForegroundTarget target)
    {
        double usedSeconds;
        lock (_snapshotLock)
        {
            usedSeconds = _todayUsedSeconds;
        }
        return new DashboardSnapshot(
            target.IsDouyin,
            target.Source,
            target.ProcessName,
            target.WindowTitle,
            usedSeconds,
            _config.DailyLimitSeconds,
            _config.DailyLimitMinutes,
            _config.Mode == "track"
        );
    }

    private void LoadReminderState()
    {
        var today = _todayKey;
        _reminderPercentSent = _storage.GetReminder(today, "percent") != null;
        _reminderLimitSent = _storage.GetReminder(today, "limit") != null;
        _reminderLastOverLimit = _storage.GetReminder(today, "last_over_limit");
    }

    private static string TodayKey(DateTime? dt = null) =>
        (dt ?? DateTime.Now).ToString("yyyy-MM-dd");

    private static void LogDebug(string tag, string format, params object?[] args)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DooTimer", "logs");
            Directory.CreateDirectory(logDir);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{tag}] {string.Format(format, args)}";
            File.AppendAllText(Path.Combine(logDir, "dootimer.log"), line + Environment.NewLine);
        }
        catch { }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        FinishActiveSession(DateTime.Now);
        FlushStorage();
        _storage.FlushDayData();
    }
}
