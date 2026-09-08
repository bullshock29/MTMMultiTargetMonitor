using MultiTargetMonitor.Models;

namespace MultiTargetMonitor.Services;

public sealed record ValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;

    public static readonly ValidationResult Ok = new(Array.Empty<string>(), Array.Empty<string>());
}

/// <summary>
/// Checks a monitor selection against the rules mstsc.exe enforces for <c>selectedmonitors</c>.
/// </summary>
public static class SelectionValidator
{
    public static ValidationResult Validate(IReadOnlyList<RdpMonitor> all, ISet<int> selectedIds)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var selected = all.Where(m => selectedIds.Contains(m.Id)).ToList();

        if (selected.Count == 0)
        {
            errors.Add("Select at least one monitor.");
            return new ValidationResult(errors, warnings);
        }

        // 1. Every selected monitor must share an edge with the rest of the selection.
        if (!IsEdgeConnected(selected))
            errors.Add("Selected monitors are not adjacent. RDP needs a contiguous block of monitors.");

        // 2. The selection should completely fill its bounding rectangle.
        long boundingArea = BoundingArea(selected);
        long sumArea = selected.Sum(m => m.Area);
        if (sumArea < boundingArea)
            warnings.Add("The selection is L-shaped or has a gap. RDP may letterbox or fall back to all monitors.");

        // 3. The Windows primary monitor is usually required in a multi-monitor session.
        var primary = all.FirstOrDefault(m => m.IsPrimary);
        if (primary is not null && !selectedIds.Contains(primary.Id))
            warnings.Add($"The primary monitor (#{primary.Id}) is not selected. Some Windows versions require it.");

        return new ValidationResult(errors, warnings);
    }

    private static bool IsEdgeConnected(IReadOnlyList<RdpMonitor> monitors)
    {
        if (monitors.Count <= 1) return true;

        var visited = new HashSet<int> { monitors[0].Id };
        var queue = new Queue<RdpMonitor>();
        queue.Enqueue(monitors[0]);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var other in monitors)
            {
                if (visited.Contains(other.Id)) continue;
                if (SharesEdge(current, other))
                {
                    visited.Add(other.Id);
                    queue.Enqueue(other);
                }
            }
        }

        return visited.Count == monitors.Count;
    }

    private static bool SharesEdge(RdpMonitor a, RdpMonitor b)
    {
        bool horizontallyTouching =
            (a.Right == b.Left || b.Right == a.Left) && Overlap(a.Top, a.Bottom, b.Top, b.Bottom) > 0;

        bool verticallyTouching =
            (a.Bottom == b.Top || b.Bottom == a.Top) && Overlap(a.Left, a.Right, b.Left, b.Right) > 0;

        return horizontallyTouching || verticallyTouching;
    }

    private static int Overlap(int start1, int end1, int start2, int end2)
        => Math.Max(0, Math.Min(end1, end2) - Math.Max(start1, start2));

    private static long BoundingArea(IReadOnlyList<RdpMonitor> monitors)
    {
        int left = monitors.Min(m => m.Left);
        int top = monitors.Min(m => m.Top);
        int right = monitors.Max(m => m.Right);
        int bottom = monitors.Max(m => m.Bottom);
        return (long)(right - left) * (bottom - top);
    }
}
