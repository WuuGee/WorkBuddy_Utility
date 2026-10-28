namespace FocusCatAI.Domain.Taskbar;

public enum TaskbarPlacementMode
{
    InTaskbar,
    AboveTaskbar
}

public enum TaskbarElementKind
{
    Start,
    Search,
    TaskView,
    Widgets,
    App,
    Tray,
    Clock,
    Other
}

/// <summary>An occupied region of the taskbar (device-independent pixels, primary monitor coordinates).</summary>
public sealed record TaskbarElement(string Name, TaskbarElementKind Kind, double Left, double Right, bool IsRunning);

/// <summary>A free horizontal region of the taskbar the pet may stand in.</summary>
public readonly record struct Zone(double Left, double Right)
{
    public double Width => Right - Left;
}

/// <summary>
/// Snapshot of the taskbar. In <see cref="TaskbarPlacementMode.InTaskbar"/> mode Top/Bottom are the taskbar bounds.
/// In <see cref="TaskbarPlacementMode.AboveTaskbar"/> mode Top is the baseline the pet stands on and Elements is empty.
/// </summary>
public sealed record TaskbarLayout(
    TaskbarPlacementMode Mode,
    double Left,
    double Top,
    double Right,
    double Bottom,
    IReadOnlyList<TaskbarElement> Elements)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
}
