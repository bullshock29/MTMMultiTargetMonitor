using System.Text;
using MultiTargetMonitor.Models;

namespace MultiTargetMonitor.Services;

/// <summary>
/// Writes the monitor-related keys into an <see cref="RdpFile"/>. All other keys are left untouched.
/// </summary>
public static class MonitorWriter
{
    /// <summary>
    /// Orders the selected ids so the Windows primary monitor (if selected) comes first.
    /// The first id in <c>selectedmonitors</c> becomes the session's primary monitor.
    /// </summary>
    public static List<int> OrderIds(IReadOnlyList<RdpMonitor> all, ISet<int> selectedIds)
    {
        var primary = all.FirstOrDefault(m => m.IsPrimary && selectedIds.Contains(m.Id));
        var ordered = new List<int>();
        if (primary is not null) ordered.Add(primary.Id);
        ordered.AddRange(selectedIds.Where(id => primary is null || id != primary.Id).OrderBy(id => id));
        return ordered;
    }

    public static string PreviewLine(IReadOnlyList<RdpMonitor> all, ISet<int> selectedIds, bool useAllMonitors)
    {
        var sb = new StringBuilder();

        if (useAllMonitors)
        {
            sb.AppendLine("use multimon:i:1");
            sb.AppendLine("dynamic resolution:i:0");
            sb.Append("smart sizing:i:0   (selectedmonitors not written — all monitors)");
            return sb.ToString();
        }

        if (selectedIds.Count == 0)
            return "use multimon:i:0   (no monitor selected — session opens on the primary)";

        var ordered = OrderIds(all, selectedIds);
        bool multi = ordered.Count > 1;

        sb.AppendLine("use multimon:i:1");
        sb.AppendLine($"selectedmonitors:s:{string.Join(",", ordered)}");
        sb.AppendLine($"dynamic resolution:i:{(multi ? 0 : 1)}");
        sb.Append("smart sizing:i:0");
        return sb.ToString();
    }

    public static void Apply(RdpFile file, IReadOnlyList<RdpMonitor> all, ISet<int> selectedIds, bool useAllMonitors)
    {
        file.SetInt("screen mode id", 2);   // full screen
        file.SetInt("span monitors", 0);    // the legacy spanning mode — always off

        if (useAllMonitors)
        {
            file.SetInt("use multimon", 1);
            file.Remove("selectedmonitors");
            file.Remove("desktopwidth");
            file.Remove("desktopheight");
            ApplyMultiMonScaling(file);
            return;
        }

        if (selectedIds.Count == 0)
        {
            file.SetInt("use multimon", 0);
            file.Remove("selectedmonitors");
            file.SetInt("dynamic resolution", 1);
            return;
        }

        // One or more explicit monitors. mstsc honours selectedmonitors only with use multimon:i:1,
        // even when the list contains a single id.
        var ordered = OrderIds(all, selectedIds);
        file.SetInt("use multimon", 1);
        file.SetString("selectedmonitors", string.Join(",", ordered));
        file.Remove("desktopwidth");
        file.Remove("desktopheight");

        if (ordered.Count > 1)
        {
            ApplyMultiMonScaling(file);
        }
        else
        {
            // A single target monitor can safely track local resize.
            file.SetInt("dynamic resolution", 1);
            file.SetInt("smart sizing", 0);
        }
    }

    /// <summary>
    /// <c>dynamic resolution</c> and <c>smart sizing</c> both make the client present a single
    /// resizable/scaled desktop surface, which overrides <c>selectedmonitors</c> and shows up as one
    /// desktop stretched across the chosen monitors. Turn them off for a real multi-monitor layout.
    /// </summary>
    private static void ApplyMultiMonScaling(RdpFile file)
    {
        file.SetInt("dynamic resolution", 0);
        file.SetInt("smart sizing", 0);
    }
}
