using System.ComponentModel;
using System.Runtime.CompilerServices;
using MultiTargetMonitor.Models;

namespace MultiTargetMonitor.ViewModels;

public sealed class MonitorRow : INotifyPropertyChanged
{
    public required RdpMonitor Info { get; init; }

    public int Id => Info.Id;

    public string Label =>
        $"#{Info.Id}    {Info.Width} × {Info.Height}" +
        (Info.IsPrimary ? "    (primary)" : "") +
        $"    {Info.Device}";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
