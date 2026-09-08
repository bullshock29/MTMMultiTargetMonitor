using System.IO;
using System.Runtime.InteropServices;

namespace MultiTargetMonitor.Services;

/// <summary>
/// A GUI app has no stdout. When started from a console with <c>--list-monitors</c> we attach to the
/// parent console so diagnostic output is visible.
/// </summary>
internal static class NativeConsole
{
    private const int AttachParentProcess = -1;

    public static void Attach()
    {
        if (!AttachConsole(AttachParentProcess))
            AllocConsole();

        var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdout);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
}
