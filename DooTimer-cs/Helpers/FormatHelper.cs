namespace DooTimer.Helpers;

public static class FormatHelper
{
    public static string FormatSeconds(double seconds)
    {
        var total = Math.Max(0, (int)seconds);
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var sec = total % 60;

        if (hours > 0)
            return $"{hours} 小时 {minutes} 分钟";
        return $"{minutes} 分 {sec} 秒";
    }

    public static string FormatCompactSeconds(double seconds)
    {
        var total = Math.Max(0, (int)seconds);
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var sec = total % 60;

        if (hours > 0)
            return $"{hours}:{minutes:D2}:{sec:D2}";
        return $"{minutes}:{sec:D2}";
    }

    public static string FormatFileSize(string path)
    {
        if (!File.Exists(path))
            return "未生成";

        var size = new FileInfo(path).Length;
        if (size < 1024)
            return $"{size} B";
        if (size < 1024 * 1024)
            return $"{size / 1024.0:F1} KB";
        return $"{size / (1024.0 * 1024.0):F1} MB";
    }

    public static string SourceText(string source)
    {
        return source switch
        {
            "client" => "客户端",
            "web" => "网页",
            _ => "未检测到"
        };
    }

    public static string FormatDecimal(double value)
    {
        if (value == (int)value)
            return ((int)value).ToString();
        return value.ToString("0.##");
    }
}
