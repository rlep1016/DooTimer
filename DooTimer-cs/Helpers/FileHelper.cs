using System.Diagnostics;

namespace DooTimer.Helpers;

public static class FileHelper
{
    public static void OpenPath(string path)
    {
        // 如果是目录，确保存在
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = false
            });
            return;
        }

        // 如果是文件，确保目录存在
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(path))
        {
            if (path.EndsWith(".json"))
                File.WriteAllText(path, "{\"days\": {}}\n");
            else
                File.WriteAllText(path, "");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = false
        });
    }
}
