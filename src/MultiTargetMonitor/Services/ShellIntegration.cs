using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MultiTargetMonitor.Services;

/// <summary>
/// Registers the app with Windows Explorer for .rdp files. Everything is written under
/// <c>HKEY_CURRENT_USER</c>, so no elevation is needed and it is fully reversible.
/// </summary>
public static class ShellIntegration
{
    // Registry token for the executable — not a path.
    private const string AppToken = "MultiTargetMonitor.exe";
    private const string FriendlyName = "Multi Target Monitor";
    private const string VerbLabel = "Edit monitors with Multi Target Monitor";

    private const string ApplicationsKey = $@"Software\Classes\Applications\{AppToken}";
    private const string OpenWithListKey = $@"Software\Classes\SystemFileAssociations\.rdp\OpenWithList\{AppToken}";
    private const string ContextVerbKey = @"Software\Classes\SystemFileAssociations\.rdp\shell\MultiTargetMonitorEdit";

    private static string ExePath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("Cannot determine executable path.");

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{ApplicationsKey}\shell\open\command");
        return key?.GetValue(null) is string command
               && command.Contains(ExePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void Register()
    {
        string command = $"\"{ExePath}\" \"%1\"";

        using (var app = Registry.CurrentUser.CreateSubKey(ApplicationsKey))
        {
            app.SetValue("FriendlyAppName", FriendlyName);
            using (var cmd = app.CreateSubKey(@"shell\open\command"))
                cmd.SetValue(null, command);
            using (var types = app.CreateSubKey("SupportedTypes"))
                types.SetValue(".rdp", string.Empty);
        }

        // Make it show up in the "Open with" list for .rdp explicitly.
        using (Registry.CurrentUser.CreateSubKey(OpenWithListKey)) { }

        // Classic right-click verb (Win11: under "Show more options").
        using (var verb = Registry.CurrentUser.CreateSubKey(ContextVerbKey))
        {
            verb.SetValue(null, VerbLabel);
            verb.SetValue("Icon", $"\"{ExePath}\",0");
            using var cmd = verb.CreateSubKey("command");
            cmd.SetValue(null, command);
        }

        NotifyShell();
    }

    public static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(ApplicationsKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(OpenWithListKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(ContextVerbKey, throwOnMissingSubKey: false);
        NotifyShell();
    }

    /// <summary>
    /// Opens the Windows "how do you want to open this?" dialog for a chosen .rdp file, with the
    /// "always use this app" checkbox. Windows does not allow an app to set itself as the default
    /// silently, so this is the closest we can get.
    /// </summary>
    public static void ShowOpenWithDialog(IntPtr owner, string rdpFilePath)
    {
        var info = new OPENASINFO
        {
            pcszFile = rdpFilePath,
            pcszClass = null,
            oaifInFlags = OAIF.ALLOW_REGISTRATION | OAIF.REGISTER_EXT | OAIF.EXEC,
        };
        SHOpenWithDialog(owner, ref info);
    }

    private static void NotifyShell() => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    // ---- native ---------------------------------------------------------

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [Flags]
    private enum OAIF : uint
    {
        ALLOW_REGISTRATION = 0x01,
        REGISTER_EXT = 0x02,
        EXEC = 0x04,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENASINFO
    {
        public string pcszFile;
        public string? pcszClass;
        public OAIF oaifInFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHOpenWithDialog(IntPtr hwndParent, ref OPENASINFO poainfo);
}
