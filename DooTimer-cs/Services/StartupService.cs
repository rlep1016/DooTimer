using System.Runtime.InteropServices;

namespace DooTimer.Services;

public class StartupService
{
    private const string ShortcutName = "DooTimer.lnk";

    public static bool IsStartupEnabled()
    {
        return File.Exists(GetShortcutPath());
    }

    public static void EnableStartup()
    {
        var shortcutPath = GetShortcutPath();
        var dir = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var target = GetStartupTarget();
        CreateShortcut(shortcutPath, target.TargetPath, target.Arguments);
    }

    public static void DisableStartup()
    {
        var path = GetShortcutPath();
        if (File.Exists(path))
            File.Delete(path);
    }

    public static string GetStartupTargetSummary()
    {
        var target = GetStartupTarget();
        if (!string.IsNullOrEmpty(target.Arguments))
            return $"{target.TargetPath} {target.Arguments}";
        return target.TargetPath;
    }

    private static string GetShortcutPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "Startup", ShortcutName);
    }

    private static (string TargetPath, string Arguments) GetStartupTarget()
    {
        // 检查是否已打包为 exe
        var exePath = Path.Combine(AppContext.BaseDirectory, "DooTimer.exe");
        if (File.Exists(exePath))
            return (exePath, "--minimized");

        // 检查 dist 目录
        var distExe = Path.Combine(AppContext.BaseDirectory, "dist", "DooTimer", "DooTimer.exe");
        if (File.Exists(distExe))
            return (distExe, "--minimized");

        var currentExe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(currentExe) && currentExe.EndsWith(".exe"))
            return (currentExe, "--minimized");

        return ("DooTimer.exe", "--minimized");
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments)
    {
        // 使用 COM WScript.Shell 创建快捷方式
        Type? shellType = null;
        try
        {
            shellType = Type.GetTypeFromProgID("WScript.Shell");
        }
        catch
        {
            // 如果 COM 不可用，创建一个简单的 .url 文件作为后备
            File.WriteAllText(shortcutPath.Replace(".lnk", ".url"),
                $"[InternetShortcut]\nURL=file:///{targetPath}\n");
            return;
        }

        if (shellType == null) return;

        dynamic? shell = null;
        dynamic? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            shortcut = shell?.CreateShortcut(shortcutPath);
            if (shortcut == null) return;

            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? "";
            shortcut.WindowStyle = 7;
            shortcut.Description = "DooTimer 抖音使用时间提醒";

            shortcut.IconLocation = targetPath; // 用 exe 自身的图标

            shortcut.Save();
        }
        catch
        {
            // 忽略 COM 错误
        }
        finally
        {
            if (shortcut != null)
                Marshal.ReleaseComObject(shortcut);
            if (shell != null)
                Marshal.ReleaseComObject(shell);
        }
    }
}
