using System.IO;
using System.Windows;
using MultiTargetMonitor.Services;

namespace MultiTargetMonitor;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => a.Equals("--list-monitors", StringComparison.OrdinalIgnoreCase)))
        {
            RunListMonitors();
            Shutdown(0);
            return;
        }

        Settings = AppSettings.Load();
        ThemeManager.Initialize(ThemeManager.Parse(Settings.Theme));

        var window = new MainWindow();

        var fileArg = e.Args.FirstOrDefault(a =>
            a.EndsWith(".rdp", StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (fileArg is not null)
            window.LoadFile(Path.GetFullPath(fileArg));

        window.Show();
    }

    private static void RunListMonitors()
    {
        NativeConsole.Attach();
        Console.WriteLine();
        Console.WriteLine("Monitors as enumerated by EnumDisplayMonitors.");
        Console.WriteLine("The index is the value to use in 'selectedmonitors' — compare with: mstsc.exe /l");
        Console.WriteLine();
        Console.WriteLine("  idx  resolution      position         primary  device");
        Console.WriteLine("  ---  --------------  ---------------  -------  ------------------");

        foreach (var m in MonitorEnumerator.Enumerate())
        {
            Console.WriteLine(
                $"  {m.Id,3}  {m.Width,5} x {m.Height,-5}  ({m.Left,5},{m.Top,5})  {(m.IsPrimary ? "  yes  " : "       ")}  {m.Device}");
        }

        Console.WriteLine();
    }
}
