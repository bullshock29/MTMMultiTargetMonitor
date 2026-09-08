using System.Diagnostics;
using System.IO;

namespace MultiTargetMonitor.Services;

public static class RdpLauncher
{
    public static void Launch(string rdpPath)
    {
        if (!File.Exists(rdpPath))
            throw new FileNotFoundException("Save the connection first.", rdpPath);

        Process.Start(new ProcessStartInfo("mstsc.exe", $"\"{rdpPath}\"")
        {
            UseShellExecute = true,
        });
    }
}
