using System.Runtime.InteropServices;
using MultiTargetMonitor.Models;

namespace MultiTargetMonitor.Services;

/// <summary>
/// Enumerates monitors via the Win32 <c>EnumDisplayMonitors</c> API and assigns each a 0-based
/// index. That index is what the .rdp <c>selectedmonitors</c> field expects.
/// </summary>
public static class MonitorEnumerator
{
    public static List<RdpMonitor> Enumerate()
    {
        var result = new List<RdpMonitor>();
        int index = 0;

        bool Callback(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data)
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                bool primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                result.Add(new RdpMonitor(
                    Id: index,
                    Device: mi.szDevice ?? $"\\\\.\\DISPLAY{index + 1}",
                    IsPrimary: primary,
                    Left: mi.rcMonitor.left,
                    Top: mi.rcMonitor.top,
                    Width: mi.rcMonitor.right - mi.rcMonitor.left,
                    Height: mi.rcMonitor.bottom - mi.rcMonitor.top));
                index++;
            }
            return true; // keep enumerating
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return result;
    }

    private const int MONITORINFOF_PRIMARY = 0x1;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
}
