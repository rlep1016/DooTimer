using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DooTimer.Helpers;
using DooTimer.Models;
using DooTimer.Services;
using DooTimer.Converters;

namespace DooTimer.Views.Pages;

public partial class SettingsPage : System.Windows.Controls.UserControl
{
    private readonly Action<AppConfig> _saveSettings;
    private readonly Func<bool> _isStartupEnabled;
    private readonly Action<bool> _setStartupEnabled;
    private readonly Func<string> _getStartupTargetSummary;
    private readonly AppConfig _config;

    public SettingsPage(
        AppConfig config,
        Action<AppConfig> saveSettings,
        Func<bool> isStartupEnabled,
        Action<bool> setStartupEnabled,
        Func<string> getStartupTargetSummary)
    {
        InitializeComponent();
        _config = config;
        _saveSettings = saveSettings;
        _isStartupEnabled = isStartupEnabled;
        _setStartupEnabled = setStartupEnabled;
        _getStartupTargetSummary = getStartupTargetSummary;

        // 填充当前配置
        DailyLimitEntry.Text = config.DailyLimitMinutes.ToString();
        RemindPercentEntry.Text = config.RemindAtPercent.ToString();
        OverLimitEntry.Text = config.OverLimitReminderMinutes.ToString();
        PollIntervalEntry.Text = config.PollIntervalSeconds.ToString("0.##");
        ClientNamesEntry.Text = string.Join(", ", config.ClientProcessNames);
        BrowserNamesEntry.Text = string.Join(", ", config.BrowserProcessNames);
        KeywordsEntry.Text = string.Join(", ", config.TitleKeywords);
        ForceCloseCheckBox.IsChecked = config.ForceCloseEnabled;
        ModeComboBox.SelectedIndex = config.Mode == "track" ? 1 : 0;
        RestReminderEntry.Text = config.RestReminderMinutes.ToString();
        ThemeComboBox.SelectedIndex = config.Theme switch
        {
            "dark" => 1,
            "system" => 2,
            _ => 0
        };

        // 统计模式下禁用限制相关设置
        ModeComboBox.SelectionChanged += (_, _) => UpdateLimitSettingsEnabled();
        UpdateLimitSettingsEnabled();

        RefreshStartupControls();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var newConfig = new AppConfig
            {
                DailyLimitMinutes = int.Parse(DailyLimitEntry.Text),
                RemindAtPercent = int.Parse(RemindPercentEntry.Text),
                OverLimitReminderMinutes = int.Parse(OverLimitEntry.Text),
                PollIntervalSeconds = double.Parse(PollIntervalEntry.Text),
                ClientProcessNames = SplitValues(ClientNamesEntry.Text),
                BrowserProcessNames = SplitValues(BrowserNamesEntry.Text),
                TitleKeywords = SplitValues(KeywordsEntry.Text),
                ForceCloseEnabled = ForceCloseCheckBox.IsChecked ?? false,
                Mode = ModeComboBox.SelectedIndex == 1 ? "track" : "limit",
                RestReminderMinutes = int.Parse(RestReminderEntry.Text),
                Theme = ThemeComboBox.SelectedIndex switch { 1 => "dark", 2 => "system", _ => "light" }
            };

            _saveSettings(newConfig);

            // 立即应用主题
            var resolved = DooTimer.Converters.AppColors.ResolveTheme(newConfig.Theme);
            DooTimer.Converters.AppColors.ApplyTheme(resolved);

            ShowSaveMessage("已保存，新的设置已生效", AppColors.GreenBrush);
        }
        catch (FormatException ex)
        {
            ShowSaveMessage($"请检查设置格式：{ex.Message}", AppColors.RedBrush);
        }
        catch (ConfigException ex)
        {
            ShowSaveMessage(ex.Message, AppColors.RedBrush);
        }
    }

    private void EnableStartup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _setStartupEnabled(true);
            ShowSaveMessage("开机启动已开启", AppColors.GreenBrush);
            RefreshStartupControls();
        }
        catch (Exception ex)
        {
            ShowSaveMessage($"开机启动设置失败：{ex.Message}", AppColors.RedBrush);
        }
    }

    private void DisableStartup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _setStartupEnabled(false);
            ShowSaveMessage("开机启动已关闭", AppColors.GreenBrush);
            RefreshStartupControls();
        }
        catch (Exception ex)
        {
            ShowSaveMessage($"开机启动设置失败：{ex.Message}", AppColors.RedBrush);
        }
    }

    public void RefreshStartupControls()
    {
        try
        {
            var enabled = _isStartupEnabled();
            var target = _getStartupTargetSummary();

            if (enabled)
            {
                StartupStatusText.Text = "已开启开机启动";
                StartupStatusLabel.Background = AppColors.GreenSoftBrush;
                StartupStatusText.Foreground = AppColors.GreenBrush;
                EnableStartupBtn.IsEnabled = false;
                DisableStartupBtn.IsEnabled = true;
            }
            else
            {
                StartupStatusText.Text = "未开启开机启动";
                StartupStatusLabel.Background = AppColors.SurfaceAltBrush;
                StartupStatusText.Foreground = AppColors.MutedBrush;
                EnableStartupBtn.IsEnabled = true;
                DisableStartupBtn.IsEnabled = false;
            }
            StartupTargetLabel.Text = $"启动目标：{target}";
        }
        catch
        {
            StartupStatusText.Text = "状态读取失败";
        }
    }

    private void UpdateLimitSettingsEnabled()
    {
        var isTrack = ModeComboBox.SelectedIndex == 1;
        DailyLimitEntry.IsEnabled = !isTrack;
        RemindPercentEntry.IsEnabled = !isTrack;
        OverLimitEntry.IsEnabled = !isTrack;
        ForceCloseCheckBox.IsEnabled = !isTrack;
        RestReminderEntry.IsEnabled = isTrack;
        TrackModeHint.Visibility = isTrack ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowSaveMessage(string text, System.Windows.Media.Brush color)
    {
        SaveMessage.Text = text;
        SaveMessage.Foreground = color;
    }

    private static List<string> SplitValues(string text)
    {
        return text.Replace("\n", ",").Split(',')
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .ToList();
    }
}
