using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DooTimer.Helpers;
using DooTimer.Models;

namespace DooTimer.Services;

public class ForegroundMonitor
{
    private AppConfig _config;
    private readonly ConcurrentDictionary<int, string> _nameCache = new();

    public ForegroundMonitor(AppConfig config)
    {
        _config = config;
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
    }

    public ForegroundTarget Inspect()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return ForegroundTarget.Empty;

        var title = GetWindowTitle(hwnd);
        var processName = GetProcessName(hwnd);

        // .NET Process.ProcessName 不带 .exe，配置文件中的进程名可能带 .exe
        // 所以两边都兼容：douyin 匹配 douyin.exe，chrome 匹配 chrome.exe
        var processNameWithExe = processName + ".exe";

        if (_config.ClientProcessNames.Contains(processName) ||
            _config.ClientProcessNames.Contains(processNameWithExe))
            return new ForegroundTarget(true, "client", processName, title);

        if ((_config.BrowserProcessNames.Contains(processName) ||
             _config.BrowserProcessNames.Contains(processNameWithExe))
            && TitleMatches(title))
            return new ForegroundTarget(true, "web", processName, title);

        return new ForegroundTarget(false, "other", processName, title);
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private string GetProcessName(IntPtr hwnd)
    {
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return "";

        if (_nameCache.TryGetValue((int)pid, out var cachedName))
            return cachedName;

        // 缓存未命中，先单独查这个进程
        try
        {
            var name = Process.GetProcessById((int)pid).ProcessName.Trim().ToLowerInvariant();
            _nameCache[(int)pid] = name;
            return name;
        }
        catch
        {
            return "";
        }
    }

    private bool TitleMatches(string title)
    {
        if (string.IsNullOrEmpty(title)) return false;
        var lowered = title.ToLowerInvariant();
        return _config.TitleKeywords.Any(kw =>
            lowered.Contains(kw.ToLowerInvariant()));
    }
}
