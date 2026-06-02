namespace DooTimer.Services;

public class Notifier
{
    private Action<string, string>? _notificationAction;

    /// <summary>注入外部通知方法（如托盘气泡），设置后优先使用，不弹 MessageBox。</summary>
    public void SetNotificationAction(Action<string, string>? action)
    {
        _notificationAction = action;
    }

    public void Notify(string title, string message)
    {
        if (_notificationAction != null)
        {
            _notificationAction(title, message);
            return;
        }

        // 回退：Windows 消息框
        var thread = new Thread(() =>
        {
            _ = MessageBoxW(IntPtr.Zero, message, title, 0x00000000 | 0x00000030 | 0x00040000);
        })
        {
            IsBackground = true
        };
        thread.Start();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
