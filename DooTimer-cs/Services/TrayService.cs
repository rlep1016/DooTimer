using DooTimer.Helpers;

namespace DooTimer.Services;

public class TrayService : IDisposable
{
    private readonly Action _showPanel;
    private readonly Action _stopApp;
    private readonly Func<string> _getStatusText;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private readonly string _configPath;
    private readonly string _usagePath;

    public TrayService(
        Func<string> getStatusText,
        Action showPanel,
        Action stopApp,
        string configPath,
        string usagePath)
    {
        _getStatusText = getStatusText;
        _showPanel = showPanel;
        _stopApp = stopApp;
        _configPath = configPath;
        _usagePath = usagePath;
    }

    public void Show()
    {
        if (_notifyIcon != null) return;

        var icon = LoadIcon();

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "DooTimer",
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => _showPanel();

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        var statusItem = new System.Windows.Forms.ToolStripMenuItem(_getStatusText()) { Enabled = false };
        contextMenu.Items.Add(statusItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var openItem = new System.Windows.Forms.ToolStripMenuItem("打开面板");
        openItem.Click += (_, _) => _showPanel();
        contextMenu.Items.Add(openItem);

        var configItem = new System.Windows.Forms.ToolStripMenuItem("打开设置");
        configItem.Click += (_, _) => FileHelper.OpenPath(_configPath);
        contextMenu.Items.Add(configItem);

        var dataItem = new System.Windows.Forms.ToolStripMenuItem("打开数据");
        dataItem.Click += (_, _) => FileHelper.OpenPath(_usagePath);
        contextMenu.Items.Add(dataItem);

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) =>
        {
            _stopApp();
            System.Windows.Application.Current.Shutdown();
        };
        contextMenu.Items.Add(exitItem);

        // 更新状态文本
        contextMenu.Opening += (_, _) =>
        {
            statusItem.Text = _getStatusText();
        };

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    public void ShowBalloonTip(string title, string text)
    {
        if (_notifyIcon == null || _notifyIcon.Visible != true)
        {
            // 托盘图标还没准备好，回退到透明消息窗口
            new Notifier().Notify(title, text);
            return;
        }

        try
        {
            _notifyIcon.ShowBalloonTip(5000, title, text, System.Windows.Forms.ToolTipIcon.Info);
        }
        catch
        {
            // 气泡通知失败时降级到 MessageBox（Win11 上已弃用托盘气泡）
            new Notifier().Notify(title, text);
        }
    }

    /// <summary>实时更新托盘图标的 hover 提示文字。</summary>
    public void UpdateTooltipText(string text)
    {
        if (_notifyIcon != null)
            _notifyIcon.Text = text;
    }

    /// <summary>动态生成带进度指示的托盘图标。</summary>
    public void UpdateProgressIcon(double percent, string timeText, bool isTrackMode)
    {
        if (_notifyIcon == null) return;

        try
        {
            // 根据百分比选颜色
            System.Drawing.Color color;
            if (isTrackMode)
                color = System.Drawing.Color.FromArgb(26, 115, 232); // 蓝色（统计模式）
            else if (percent >= 1.0)
                color = System.Drawing.Color.FromArgb(220, 38, 38);  // 红色（超限）
            else if (percent >= 0.8)
                color = System.Drawing.Color.FromArgb(183, 121, 31); // 黄色（80%）
            else
                color = System.Drawing.Color.FromArgb(15, 159, 110); // 绿色（正常）

            using var bitmap = new System.Drawing.Bitmap(32, 32);
            using var g = System.Drawing.Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 透明背景
            g.Clear(System.Drawing.Color.Transparent);

            // 画圆角矩形背景
            using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(30, 30, 30));
            g.FillEllipse(bgBrush, 1, 1, 30, 30);

            // 画进度弧线
            using var pen = new System.Drawing.Pen(color, 3);
            var sweepAngle = isTrackMode ? 360 : (int)(360 * Math.Min(percent, 1.0));
            if (sweepAngle > 0)
                g.DrawArc(pen, 4, 4, 24, 24, -90, sweepAngle);

            // 画时间文字
            using var font = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Bold);
            using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            var sf = new System.Drawing.StringFormat
            {
                Alignment = System.Drawing.StringAlignment.Center,
                LineAlignment = System.Drawing.StringAlignment.Center
            };
            g.DrawString(timeText, font, textBrush, new System.Drawing.RectangleF(0, 0, 32, 32), sf);

            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            oldIcon?.Dispose();
        }
        catch { }
    }

    /// <summary>加载图标：优先从 exe 自身提取（兼容单文件打包），回退到 .ico 文件。</summary>
    public static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                return System.Drawing.Icon.ExtractAssociatedIcon(exe)
                    ?? System.Drawing.SystemIcons.Application;
        }
        catch { }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "DooTimer.ico");
        try
        {
            if (File.Exists(iconPath))
                return new System.Drawing.Icon(iconPath);
        }
        catch { }

        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
