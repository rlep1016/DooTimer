using System.Globalization;
using System.Windows.Data;
using DooTimer.Helpers;

namespace DooTimer.Converters;

public class SecondsToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double seconds)
            return FormatHelper.FormatSeconds(seconds);
        return "0 分 0 秒";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class SecondsToCompactConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double seconds)
            return FormatHelper.FormatCompactSeconds(seconds);
        return "0:00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PercentToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
            return $"{(int)(percent * 100)}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class IsDouyinToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDouyin)
            return isDouyin ? AppColors.Red : AppColors.Green;
        return AppColors.Green;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PercentToProgressColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            if (percent >= 1) return AppColors.RedBrush;
            if (percent >= 0.8) return AppColors.YellowBrush;
            return AppColors.GreenBrush;
        }
        return AppColors.GreenBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public static class AppColors
{
    // 亮色模式
    private const string LightBg = "#f5f7fb";
    private const string LightSurface = "#ffffff";
    private const string LightSurfaceAlt = "#eef2f7";
    private const string LightLine = "#d9e1ea";
    private const string LightText = "#111827";
    private const string LightMuted = "#667085";
    private const string LightMuted2 = "#98a2b3";

    // 暗色模式
    private const string DarkBg = "#1a1a2e";
    private const string DarkSurface = "#252540";
    private const string DarkSurfaceAlt = "#2d2d4a";
    private const string DarkLine = "#3a3a5c";
    private const string DarkText = "#e8e8f0";
    private const string DarkMuted = "#9898b8";
    private const string DarkMuted2 = "#707090";

    // 当前使用的色值（对外暴露为常量风格）
    public const string Bg = "#f5f7fb";
    public const string Surface = "#ffffff";
    public const string SurfaceAlt = "#eef2f7";
    public const string Line = "#d9e1ea";
    public const string Text = "#111827";
    public const string Muted = "#667085";
    public const string Muted2 = "#98a2b3";
    public const string Blue = "#1a73e8";
    public const string BlueHover = "#1558b0";
    public const string Green = "#0f9f6e";
    public const string Red = "#dc2626";
    public const string Yellow = "#b7791f";

    // 可动态切换的 Brush（ApplyTheme 会替换整个实例）
    public static System.Windows.Media.SolidColorBrush BgBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(LightBg));
    public static System.Windows.Media.SolidColorBrush SurfaceBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(LightSurface));
    public static System.Windows.Media.SolidColorBrush SurfaceAltBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(LightSurfaceAlt));
    public static System.Windows.Media.SolidColorBrush LineBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(LightLine));
    public static System.Windows.Media.SolidColorBrush TextBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(LightText));
    public static System.Windows.Media.SolidColorBrush MutedBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(LightMuted));
    public static System.Windows.Media.SolidColorBrush Muted2Brush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(LightMuted2));
    public static System.Windows.Media.SolidColorBrush BlueBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Blue));
    public static System.Windows.Media.SolidColorBrush BlueHoverBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(BlueHover));
    public static System.Windows.Media.SolidColorBrush GreenBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Green));
    public static System.Windows.Media.SolidColorBrush GreenSoftBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e7f6ef"));
    public static System.Windows.Media.SolidColorBrush RedBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Red));
    public static System.Windows.Media.SolidColorBrush RedSoftBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#fee2e2"));
    public static System.Windows.Media.SolidColorBrush YellowBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Yellow));
    public static System.Windows.Media.SolidColorBrush YellowSoftBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#fff7df"));

    private static string _currentTheme = "light";

    public static string CurrentTheme => _currentTheme;

    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }

    public static string ResolveTheme(string theme)
    {
        if (theme == "system")
            return IsSystemDarkMode() ? "dark" : "light";
        return theme == "dark" ? "dark" : "light";
    }

    public static void ApplyTheme(string theme)
    {
        _currentTheme = theme;
        var isDark = theme == "dark";

        static System.Windows.Media.Color C(string hex) =>
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);

        // 创建主题对应的新画笔，不修改旧的（避免 Frozen 异常）
        var bg = new System.Windows.Media.SolidColorBrush(C(isDark ? DarkBg : LightBg));
        var surface = new System.Windows.Media.SolidColorBrush(C(isDark ? DarkSurface : LightSurface));
        var surfaceAlt = new System.Windows.Media.SolidColorBrush(C(isDark ? DarkSurfaceAlt : LightSurfaceAlt));
        var line = new System.Windows.Media.SolidColorBrush(C(isDark ? DarkLine : LightLine));
        var text = new System.Windows.Media.SolidColorBrush(C(isDark ? DarkText : LightText));
        var muted = new System.Windows.Media.SolidColorBrush(C(isDark ? DarkMuted : LightMuted));
        var muted2 = new System.Windows.Media.SolidColorBrush(C(isDark ? DarkMuted2 : LightMuted2));
        var greenSoft = new System.Windows.Media.SolidColorBrush(C(isDark ? "#1a3a2e" : "#e7f6ef"));
        var redSoft = new System.Windows.Media.SolidColorBrush(C(isDark ? "#3a1a1a" : "#fee2e2"));
        var yellowSoft = new System.Windows.Media.SolidColorBrush(C(isDark ? "#3a3520" : "#fff7df"));
        var blue = new System.Windows.Media.SolidColorBrush(C(Blue));
        var blueHover = new System.Windows.Media.SolidColorBrush(C(BlueHover));
        var green = new System.Windows.Media.SolidColorBrush(C(Green));
        var red = new System.Windows.Media.SolidColorBrush(C(Red));
        var yellow = new System.Windows.Media.SolidColorBrush(C(Yellow));

        // 更新静态字段（代码中用 AppColors.XxxBrush 的地方）
        BgBrush = bg; SurfaceBrush = surface; SurfaceAltBrush = surfaceAlt;
        LineBrush = line; TextBrush = text; MutedBrush = muted; Muted2Brush = muted2;
        BlueBrush = blue; BlueHoverBrush = blueHover; GreenBrush = green; RedBrush = red; YellowBrush = yellow;
        GreenSoftBrush = greenSoft; RedSoftBrush = redSoft; YellowSoftBrush = yellowSoft;

        // 更新资源字典（XAML 中用 {DynamicResource XxxBrush} 的地方）
        var resources = System.Windows.Application.Current.Resources;
        resources["BgBrush"] = bg;
        resources["SurfaceBrush"] = surface;
        resources["SurfaceAltBrush"] = surfaceAlt;
        resources["LineBrush"] = line;
        resources["TextBrush"] = text;
        resources["MutedBrush"] = muted;
        resources["Muted2Brush"] = muted2;
        resources["BlueBrush"] = blue;
        resources["BlueHoverBrush"] = blueHover;
        resources["GreenBrush"] = green;
        resources["RedBrush"] = red;
        resources["YellowBrush"] = yellow;
        resources["GreenSoftBrush"] = greenSoft;
        resources["RedSoftBrush"] = redSoft;
        resources["YellowSoftBrush"] = yellowSoft;
    }
}
