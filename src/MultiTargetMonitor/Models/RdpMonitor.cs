namespace MultiTargetMonitor.Models;

/// <summary>
/// One physical monitor as seen by <c>EnumDisplayMonitors</c>.
/// <para>
/// <see cref="Id"/> is the 0-based enumeration index. This is the value that goes into the
/// <c>selectedmonitors</c> line of a .rdp file and should line up with the numbers shown by
/// <c>mstsc.exe /l</c>. Verify with the <c>--list-monitors</c> switch if in doubt.
/// </para>
/// </summary>
public sealed record RdpMonitor(
    int Id,
    string Device,
    bool IsPrimary,
    int Left,
    int Top,
    int Width,
    int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public long Area => (long)Width * Height;

    public string Resolution => $"{Width} × {Height}";
}
