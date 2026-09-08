using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MultiTargetMonitor.Services;

/// <summary>Matches the OS title bar to the app theme (Windows 10 20H1+ / Windows 11).</summary>
public static class WindowChrome
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void ApplyTheme(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        int flag = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref flag, sizeof(int));

        // Nudge the frame so the title bar repaints immediately.
        if (window.WindowState == WindowState.Normal)
        {
            var before = window.Width;
            window.Width = before + 1;
            window.Width = before;
        }
    }
}
