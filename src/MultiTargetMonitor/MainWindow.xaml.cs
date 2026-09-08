using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using MultiTargetMonitor.Services;
using MultiTargetMonitor.ViewModels;

namespace MultiTargetMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        ThemeManager.Changed += OnThemeChanged;
        Closed += (_, _) => ThemeManager.Changed -= OnThemeChanged;

        UpdateThemeIndicators();
        UpdateShellButtonText();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowChrome.ApplyTheme(this, ThemeManager.IsDarkEffective);
    }

    private void OnThemeChanged()
    {
        WindowChrome.ApplyTheme(this, ThemeManager.IsDarkEffective);
        UpdateThemeIndicators();
    }

    public void LoadFile(string path)
    {
        try
        {
            _vm.LoadFile(path);
            SetStatus($"Opened {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            ShowError("Could not open file", ex);
        }
    }

    // ---- file actions --------------------------------------------

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _vm.NewFile();
        SetStatus("New connection");
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        CloseMenus();
        var dialog = new OpenFileDialog
        {
            Filter = "Remote Desktop files (*.rdp)|*.rdp|All files (*.*)|*.*",
            DefaultExt = ".rdp",
        };
        if (dialog.ShowDialog(this) == true)
            LoadFile(dialog.FileName);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveInternal();

    // ---- theme --------------------------------------------------

    private void ThemeLight_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.Light);

    private void ThemeDark_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.Dark);

    private void ThemeSystem_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.System);

    private void SetTheme(AppTheme theme)
    {
        CloseMenus();
        ThemeManager.Set(theme);
        App.Settings.Theme = theme.ToString();
        App.Settings.Save();
    }

    private void UpdateThemeIndicators()
    {
        foreach (var (button, theme) in new[]
                 {
                     (ThemeLightBtn, AppTheme.Light),
                     (ThemeDarkBtn, AppTheme.Dark),
                     (ThemeSystemBtn, AppTheme.System),
                 })
        {
            bool active = ThemeManager.Current == theme;
            button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            button.Foreground = active
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("PrimaryTextBrush");
        }

        ThemeToggle.Content = ThemeManager.IsDarkEffective ? "◓" : "◒";
    }

    // ---- tools -------------------------------------------------

    private void ToggleRegister_Click(object sender, RoutedEventArgs e)
    {
        CloseMenus();
        try
        {
            if (ShellIntegration.IsRegistered())
            {
                ShellIntegration.Unregister();
                SetStatus("Removed from the Windows \"Open with\" menu.");
            }
            else
            {
                ShellIntegration.Register();
                SetStatus("Added to the Windows \"Open with\" menu for .rdp files.");
            }
        }
        catch (Exception ex)
        {
            ShowError("Could not update Windows integration", ex);
        }
        UpdateShellButtonText();
    }

    private void UpdateShellButtonText()
        => ShellRegisterBtn.Content = ShellIntegration.IsRegistered()
            ? "Remove from Windows \"Open with\" menu"
            : "Add to Windows \"Open with\" menu";

    private void MakeDefault_Click(object sender, RoutedEventArgs e)
    {
        CloseMenus();

        if (!ShellIntegration.IsRegistered())
        {
            MessageBox.Show(this, "Add the app to the \"Open with\" menu first.",
                "Multi Target Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Pick any .rdp file to set the default handler",
            Filter = "Remote Desktop files (*.rdp)|*.rdp",
            DefaultExt = ".rdp",
        };
        if (dialog.ShowDialog(this) != true) return;

        var handle = new WindowInteropHelper(this).Handle;
        ShellIntegration.ShowOpenWithDialog(handle, dialog.FileName);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        CloseMenus();

        var text = new TextBox
        {
            Text = _vm.BuildPreviewText(),
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas, monospace"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
            BorderThickness = new Thickness(0),
            Background = (Brush)FindResource("SurfaceBackground"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Padding = new Thickness(14),
        };

        new Window
        {
            Title = "Preview .rdp file",
            Owner = this,
            Icon = Icon,
            Width = 540,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("WindowBackground"),
            Content = text,
        }.ShowDialog();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        CloseMenus();
        _vm.RefreshMonitors();
        SetStatus($"{_vm.Monitors.Count} monitor(s) detected");
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        CloseMenus();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";

        MessageBox.Show(this,
            $"MTM — Multi Target Monitor  {version}\n\n" +
            "Creates and edits .rdp files with per-monitor selection via the 'selectedmonitors' field.\n\n" +
            "Monitor ids come from EnumDisplayMonitors (0-based) and should match 'mstsc.exe /l'. " +
            "Run with --list-monitors from a console to print the mapping.",
            "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---- connect ----------------------------------------------

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.CanConnect)
        {
            MessageBox.Show(this, "Enter a computer name on the General tab first.",
                "Multi Target Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!SaveInternal()) return;

        try
        {
            RdpLauncher.Launch(_vm.CurrentPath!);
            SetStatus($"Launched mstsc with {Path.GetFileName(_vm.CurrentPath)}");
        }
        catch (Exception ex)
        {
            ShowError("Could not launch Remote Desktop", ex);
        }
    }

    // ---- save helpers ----------------------------------------

    private bool SaveInternal()
        => _vm.CurrentPath is { } path ? SaveTo(path) : SaveAsInternal();

    private bool SaveAsInternal()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Remote Desktop files (*.rdp)|*.rdp",
            DefaultExt = ".rdp",
            FileName = string.IsNullOrWhiteSpace(_vm.Computer) ? "connection.rdp" : $"{_vm.Computer}.rdp",
        };
        return dialog.ShowDialog(this) == true && SaveTo(dialog.FileName);
    }

    private bool SaveTo(string path)
    {
        try
        {
            _vm.Save(path);
            SetStatus($"Saved {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception ex)
        {
            ShowError("Could not save file", ex);
            return false;
        }
    }

    // ---- drag & drop ----------------------------------------

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetRdpPath(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (TryGetRdpPath(e, out var path))
            LoadFile(path);
    }

    private static bool TryGetRdpPath(DragEventArgs e, out string path)
    {
        path = "";
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return false;

        var match = files.FirstOrDefault(f => f.EndsWith(".rdp", StringComparison.OrdinalIgnoreCase));
        if (match is null) return false;

        path = match;
        return true;
    }

    // ---- shortcuts -----------------------------------------

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N: _vm.NewFile(); SetStatus("New connection"); e.Handled = true; break;
                case Key.O: Open_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.S: SaveInternal(); e.Handled = true; break;
            }
        }

        base.OnPreviewKeyDown(e);
    }

    // ---- misc ---------------------------------------------

    private void CloseMenus()
    {
        MenuToggle.IsChecked = false;
        ThemeToggle.IsChecked = false;
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void ShowError(string caption, Exception ex)
        => MessageBox.Show(this, ex.Message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
}
