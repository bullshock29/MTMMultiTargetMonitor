using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using MultiTargetMonitor.Models;
using MultiTargetMonitor.Services;

namespace MultiTargetMonitor.ViewModels;

public enum ValidationSeverity
{
    None,
    Ok,
    Warning,
    Error,
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private RdpFile _rdp = RdpFile.CreateDefault();

    public MainViewModel()
    {
        RefreshMonitors();
        ApplyFromRdp();
    }

    public ObservableCollection<MonitorRow> Monitors { get; } = new();

    // ---- General tab ---------------------------------------------------

    private string _computer = "";
    public string Computer
    {
        get => _computer;
        set { if (Set(ref _computer, value)) OnPropertyChanged(nameof(CanConnect)); }
    }

    private string _username = "";
    public string Username
    {
        get => _username;
        set => Set(ref _username, value);
    }

    // ---- Display tab --------------------------------------------------

    private bool _useAllMonitors;
    public bool UseAllMonitors
    {
        get => _useAllMonitors;
        set
        {
            if (!Set(ref _useAllMonitors, value)) return;
            OnPropertyChanged(nameof(PerMonitorEnabled));
            Recompute();
        }
    }

    public bool PerMonitorEnabled => !_useAllMonitors;

    private string _validationText = "";
    public string ValidationText
    {
        get => _validationText;
        private set => Set(ref _validationText, value);
    }

    private ValidationSeverity _validationSeverity = ValidationSeverity.None;
    public ValidationSeverity ValidationSeverity
    {
        get => _validationSeverity;
        private set => Set(ref _validationSeverity, value);
    }

    private string _previewText = "";
    public string PreviewText
    {
        get => _previewText;
        private set => Set(ref _previewText, value);
    }

    // ---- file state -------------------------------------------------

    private string? _currentPath;
    public string? CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (Set(ref _currentPath, value)) OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string WindowTitle =>
        "MTM — " + (CurrentPath is null ? "Multi Target Monitor" : Path.GetFileName(CurrentPath));

    public bool CanConnect => !string.IsNullOrWhiteSpace(Computer);

    private HashSet<int> SelectedIds => Monitors.Where(m => m.IsSelected).Select(m => m.Id).ToHashSet();

    private List<RdpMonitor> MonitorInfos => Monitors.Select(m => m.Info).ToList();

    // ---- commands --------------------------------------------------

    public void NewFile()
    {
        _rdp = RdpFile.CreateDefault();
        CurrentPath = null;
        ApplyFromRdp();
    }

    public void LoadFile(string path)
    {
        _rdp = RdpFile.Load(path);
        CurrentPath = path;
        ApplyFromRdp();
    }

    public void Save(string path)
    {
        BuildRdp();
        _rdp.Save(path);
        CurrentPath = path;
    }

    public void RefreshMonitors()
    {
        var previouslySelected = Monitors.Where(m => m.IsSelected).Select(m => m.Id).ToHashSet();

        foreach (var row in Monitors)
            row.PropertyChanged -= OnRowPropertyChanged;
        Monitors.Clear();

        foreach (var monitor in MonitorEnumerator.Enumerate())
        {
            var row = new MonitorRow
            {
                Info = monitor,
                IsSelected = previouslySelected.Contains(monitor.Id),
            };
            row.PropertyChanged += OnRowPropertyChanged;
            Monitors.Add(row);
        }

        Recompute();
    }

    /// <summary>Rebuilds the in-memory .rdp file from the current UI state. Unknown keys are kept.</summary>
    public RdpFile BuildRdp()
    {
        _rdp.SetString("full address", Computer.Trim());

        if (string.IsNullOrWhiteSpace(Username))
            _rdp.Remove("username");
        else
            _rdp.SetString("username", Username.Trim());

        MonitorWriter.Apply(_rdp, MonitorInfos, SelectedIds, UseAllMonitors);
        return _rdp;
    }

    public string BuildPreviewText() => BuildRdp().ToText();

    // ---- internals -----------------------------------------------

    private void ApplyFromRdp()
    {
        Computer = _rdp.GetString("full address") ?? "";
        Username = _rdp.GetString("username") ?? "";

        bool multimon = (_rdp.GetInt("use multimon") ?? 0) == 1;
        var selected = ParseSelectedMonitors(_rdp.GetString("selectedmonitors"));

        if (multimon && selected.Count == 0)
        {
            _useAllMonitors = true;
        }
        else
        {
            _useAllMonitors = false;
            foreach (var row in Monitors)
                row.IsSelected = selected.Contains(row.Id);

            if (selected.Count == 0)
            {
                var primary = Monitors.FirstOrDefault(m => m.Info.IsPrimary);
                if (primary is not null) primary.IsSelected = true;
            }
        }

        OnPropertyChanged(nameof(UseAllMonitors));
        OnPropertyChanged(nameof(PerMonitorEnabled));
        Recompute();
    }

    private static HashSet<int> ParseSelectedMonitors(string? value)
    {
        var result = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, out var id))
                result.Add(id);

        return result;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorRow.IsSelected))
            Recompute();
    }

    private void Recompute()
    {
        PreviewText = MonitorWriter.PreviewLine(MonitorInfos, SelectedIds, UseAllMonitors);

        if (UseAllMonitors)
        {
            ValidationText = "The session will use every monitor, matching mstsc's \"Use all my monitors\".";
            ValidationSeverity = ValidationSeverity.None;
            return;
        }

        var result = SelectionValidator.Validate(MonitorInfos, SelectedIds);
        var lines = result.Errors.Concat(result.Warnings).ToList();

        if (lines.Count == 0)
        {
            ValidationText = "Selection looks good.";
            ValidationSeverity = ValidationSeverity.Ok;
        }
        else
        {
            ValidationText = string.Join(Environment.NewLine, lines);
            ValidationSeverity = result.IsValid ? ValidationSeverity.Warning : ValidationSeverity.Error;
        }
    }

    // ---- INotifyPropertyChanged --------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
