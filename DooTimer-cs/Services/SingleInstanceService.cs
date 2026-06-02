using System.Runtime.InteropServices;
using DooTimer.Helpers;

namespace DooTimer.Services;

public class AlreadyRunningException : Exception
{
    public AlreadyRunningException(string message) : base(message) { }
}

public class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\DooTimerSingleInstance";
    private const string WakeMessageName = "DooTimerWakeMainWindow";

    private IntPtr _mutexHandle;
    private readonly uint _wakeMessageId;

    public SingleInstanceService()
    {
        _wakeMessageId = NativeMethods.RegisterWindowMessage(WakeMessageName);
    }

    public void Acquire()
    {
        _mutexHandle = CreateMutex(IntPtr.Zero, false, MutexName);
        if (_mutexHandle == IntPtr.Zero)
            throw new InvalidOperationException("无法创建互斥体。");

        if (Marshal.GetLastWin32Error() == 183) // ERROR_ALREADY_EXISTS
        {
            CloseHandle(_mutexHandle);
            _mutexHandle = IntPtr.Zero;
            throw new AlreadyRunningException("DooTimer 已经在运行。");
        }
    }

    public void Dispose()
    {
        if (_mutexHandle != IntPtr.Zero)
        {
            CloseHandle(_mutexHandle);
            _mutexHandle = IntPtr.Zero;
        }
    }

    public static void RequestExistingInstanceToShow()
    {
        var messageId = NativeMethods.RegisterWindowMessage(WakeMessageName);
        NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, messageId, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateMutex(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
