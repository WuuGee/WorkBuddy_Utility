using System.Windows;
using System.Windows.Automation;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Taskbar;
using FocusCatAI.Presentation.Configuration;
using FocusCatAI.Presentation.Interop;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Scans the Windows taskbar (UI Automation, plus the tray window) every few seconds and publishes a
/// <see cref="TaskbarLayout"/>. Falls back to <see cref="TaskbarPlacementMode.AboveTaskbar"/> whenever the
/// taskbar cannot be read reliably (auto-hide, non-bottom taskbar, UIA failure) or the user asks for it.
/// </summary>
public sealed class TaskbarLayoutService : BackgroundService, ITaskbarLayoutProvider
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

    private readonly PetOptions _options;
    private readonly FocusCatAI.Application.Abstractions.ISettingsService _settings;
    private readonly ILogger<TaskbarLayoutService> _logger;
    private volatile TaskbarLayout _current;
    private int _failures;
    private bool _loggedGeometry;
    private string _layoutSignature = string.Empty;

    public TaskbarLayoutService(IOptions<PetOptions> options, FocusCatAI.Application.Abstractions.ISettingsService settings, ILogger<TaskbarLayoutService> logger)
    {
        _options = options.Value;
        _settings = settings;
        _logger = logger;
        _current = BuildAbove();
    }

    public TaskbarLayout Current => _current;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Run(Refresh, cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await Task.Run(Refresh, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Refresh()
    {
        TaskbarLayout next;
        try
        {
            var forceAbove = !_settings.Current.General.RunInTaskbarMode
                || _settings.Current.Pet.Placement == FocusCatAI.Application.Settings.PlacementPreference.AboveTaskbar
                || string.Equals(_options.Placement, "AboveTaskbar", StringComparison.OrdinalIgnoreCase);
            next = (forceAbove ? null : TryBuildInTaskbar()) ?? BuildAbove();
            _failures = 0;
        }
        catch (Exception ex)
        {
            if (_failures++ == 0)
            {
                _logger.LogWarning(ex, "Taskbar scan failed; keeping previous layout");
            }

            if (_failures >= 3)
            {
                _current = BuildAbove();
            }

            return;
        }

        if (next.Mode != _current.Mode)
        {
            _logger.LogInformation("Taskbar placement mode: {Mode} ({Count} elements)", next.Mode, next.Elements.Count);
        }

        LogLayoutIfChanged(next);

        _current = next;
    }

    /// <summary>Dumps every element and free zone whenever the arrangement changes, to validate favorite destinations.</summary>
    private void LogLayoutIfChanged(TaskbarLayout layout)
    {
        var signature = string.Join('|', layout.Elements.Select(e => $"{e.Kind}:{e.Left:F0}-{e.Right:F0}"));
        if (signature == _layoutSignature)
        {
            return;
        }

        _layoutSignature = signature;
        foreach (var e in layout.Elements.OrderBy(e => e.Left))
        {
            _logger.LogInformation("[TaskbarElement] Name={Name} Kind={Kind} Left={Left:F0} Right={Right:F0} Center={Center:F0}",
                e.Name, e.Kind, e.Left, e.Right, (e.Left + e.Right) / 2);
        }

        foreach (var z in FocusCatAI.Application.Taskbar.TaskbarGeometry.GetZones(layout, _settings.Current.Pet.Size == FocusCatAI.Domain.Pets.PetSize.Small ? 32 : 44))
        {
            _logger.LogInformation("[FreeZone] Start={Start:F0} End={End:F0} Width={Width:F0}", z.Left, z.Right, z.Width);
        }
    }

    private static TaskbarLayout BuildAbove()
    {
        var area = SystemParameters.WorkArea;
        return new TaskbarLayout(TaskbarPlacementMode.AboveTaskbar, area.Left, area.Bottom, area.Right, area.Bottom,
            Array.Empty<TaskbarElement>());
    }

    private TaskbarLayout? TryBuildInTaskbar()
    {
        var taskbar = Native.FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !Native.GetWindowRect(taskbar, out var rect))
        {
            return null;
        }

        var data = new Native.APPBARDATA { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.APPBARDATA>(), hWnd = taskbar };
        var state = (uint)Native.SHAppBarMessage(0x4, ref data);
        if ((state & 0x1) != 0)
        {
            return null; // auto-hide: the taskbar is not a stable home
        }

        var screenWidth = Native.GetSystemMetrics(0);
        var screenHeight = Native.GetSystemMetrics(1);
        var width = rect.Right - rect.Left;
        if (rect.Top < screenHeight / 2 || width < screenWidth * 0.5)
        {
            return null; // taskbar docked top/left/right: use the above-taskbar fallback
        }

        var scale = Native.DpiScale;
        if (!_loggedGeometry)
        {
            _loggedGeometry = true;
            _logger.LogInformation(
            "Taskbar raw rect L={L} T={T} R={R} B={B} ({W}x{H}px); screen metrics={SW}x{SH}; dpiScale={Scale}; WPF primary={PW:F0}x{PH:F0}; converted T={CT:F1} B={CB:F1} H={CH:F1}",
            rect.Left, rect.Top, rect.Right, rect.Bottom, width, rect.Bottom - rect.Top, screenWidth, screenHeight, scale,
            SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight,
            rect.Top / scale, rect.Bottom / scale, (rect.Bottom - rect.Top) / scale);
        }
        var elements = ReadElements(taskbar, rect, scale);
        if (elements.Count < 2)
        {
            return null;
        }

        return new TaskbarLayout(TaskbarPlacementMode.InTaskbar,
            rect.Left / scale, rect.Top / scale, rect.Right / scale, rect.Bottom / scale, elements);
    }

    private static List<TaskbarElement> ReadElements(IntPtr taskbar, Native.RECT bar, double scale)
    {
        var result = new List<TaskbarElement>();
        var barWidth = bar.Right - bar.Left;

        var root = AutomationElement.FromHandle(taskbar);
        var condition = new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.SplitButton));

        var cache = new CacheRequest { TreeScope = TreeScope.Element };
        cache.Add(AutomationElement.NameProperty);
        cache.Add(AutomationElement.AutomationIdProperty);
        cache.Add(AutomationElement.ClassNameProperty);
        cache.Add(AutomationElement.BoundingRectangleProperty);
        cache.Add(AutomationElement.IsOffscreenProperty);

        AutomationElementCollection found;
        using (cache.Activate())
        {
            found = root.FindAll(TreeScope.Descendants, condition);
        }

        foreach (AutomationElement element in found)
        {
            var info = element.Cached;
            var r = info.BoundingRectangle;
            if (info.IsOffscreen || r.IsEmpty || r.Width < 4 || r.Width > barWidth * 0.6)
            {
                continue;
            }

            if (r.Bottom < bar.Top || r.Top > bar.Bottom)
            {
                continue;
            }

            var kind = Classify(info.Name, info.AutomationId, info.ClassName);
            result.Add(new TaskbarElement(
                info.Name ?? string.Empty,
                kind,
                r.Left / scale,
                r.Right / scale,
                kind == TaskbarElementKind.App && (info.Name?.Contains("running window", StringComparison.OrdinalIgnoreCase) ?? false)));
        }

        var tray = Native.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && Native.GetWindowRect(tray, out var t) && t.Right > t.Left)
        {
            result.Add(new TaskbarElement("Notification area", TaskbarElementKind.Tray, t.Left / scale, t.Right / scale, false));
        }

        return result;
    }

    private static TaskbarElementKind Classify(string? name, string? id, string? cls)
    {
        name ??= string.Empty;
        id ??= string.Empty;
        cls ??= string.Empty;

        bool Has(string text, string part) => text.Contains(part, StringComparison.OrdinalIgnoreCase);

        if (id == "StartButton" || name == "Start")
        {
            return TaskbarElementKind.Start;
        }

        if (id == "SearchButton" || Has(name, "Search") || Has(name, "Type here"))
        {
            return TaskbarElementKind.Search;
        }

        if (id == "TaskViewButton" || Has(name, "Task View"))
        {
            return TaskbarElementKind.TaskView;
        }

        if (id == "WidgetsButton" || Has(name, "Widgets"))
        {
            return TaskbarElementKind.Widgets;
        }

        if (Has(cls, "Clock") || Has(id, "Clock"))
        {
            return TaskbarElementKind.Clock;
        }

        if (Has(id, "SystemTray") || Has(id, "NotificationArea") || Has(name, "Notification") || Has(name, "hidden icons") ||
            Has(name, "Show desktop"))
        {
            return TaskbarElementKind.Tray;
        }

        if (Has(cls, "TaskListButton") || id.StartsWith("Appid:", StringComparison.OrdinalIgnoreCase) ||
            Has(name, "running window") || Has(name, "pinned"))
        {
            return TaskbarElementKind.App;
        }

        return TaskbarElementKind.Other;
    }
}
