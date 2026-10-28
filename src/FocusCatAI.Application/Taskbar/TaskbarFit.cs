using FocusCatAI.Domain.Taskbar;

namespace FocusCatAI.Application.Taskbar;

/// <summary>Pure helpers that keep the pet fully inside the taskbar bounds.</summary>
public static class TaskbarFit
{
    public const double Padding = 4;
    public const double MinSize = 16;

    /// <summary>Rendered pet size: the requested size, scaled down to fit the taskbar when in taskbar mode.</summary>
    public static double ComputeSize(TaskbarLayout layout, double requested)
    {
        if (layout.Mode != TaskbarPlacementMode.InTaskbar)
        {
            return requested;
        }

        var available = Math.Max(MinSize, layout.Height - Padding);
        return Math.Min(requested, available);
    }

    /// <summary>Top of the pet window so that its bottom edge sits on the taskbar bottom (or the baseline above it).</summary>
    public static double ComputeTop(TaskbarLayout layout, double petHeight)
    {
        var bottom = layout.Mode == TaskbarPlacementMode.InTaskbar ? layout.Bottom : layout.Top;
        return bottom - petHeight;
    }

    /// <summary>Pet bounds as (Left, Top, Right, Bottom).</summary>
    public static (double Left, double Top, double Right, double Bottom) ComputeBounds(
        TaskbarLayout layout, double requested, double left)
    {
        var size = ComputeSize(layout, requested);
        var top = ComputeTop(layout, size);
        return (left, top, left + size, top + size);
    }
}
