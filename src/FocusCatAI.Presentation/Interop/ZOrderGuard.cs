using Microsoft.Extensions.Logging;

namespace FocusCatAI.Presentation.Interop;

/// <summary>
/// Keeps the pet window above the taskbar without polling. The taskbar and its flyouts are topmost windows that
/// Explorer re-raises whenever they take foreground, which can put them above the pet. Windows has no event for
/// "z-order of a top-level window changed", so this listens for the events that precede it (foreground changes and
/// the taskbar being shown or reordered) and re-asserts the pet's topmost position straight away.
/// </summary>
internal sealed class ZOrderGuard : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectReorder = 0x8004;
    private const uint WinEventOutOfContext = 0x0000;
    private const int ObjIdWindow = 0;

    private readonly IntPtr _pet;
    private readonly ILogger _logger;
    private readonly Native.WinEventProc _callback; // field: the delegate must outlive the native hook
    private readonly List<IntPtr> _hooks = [];
    private int _corrections;
    private int _fallbackCorrections;

    public int ZOrderGuardCorrections => _corrections;
    public int FallbackCorrections => _fallbackCorrections;

    /// <summary>Polling safety net: raises the pet only if the taskbar is above it, and counts it separately from event-driven fixes.</summary>
    public void FallbackCheck()
    {
        var petZ = Native.ZIndex(_pet);
        var tray = Native.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero || Native.ZIndex(tray) > petZ)
        {
            return;
        }

        Native.ForceToTopOfTopmostBand(_pet);
        _fallbackCorrections++;
        _logger.LogWarning("[ZOrder] Fallback poll corrected the pet (FallbackCorrections={Fallback}, ZOrderGuardCorrections={Guard}, petZ {Before} -> {After})",
            _fallbackCorrections, _corrections, petZ, Native.ZIndex(_pet));
    }

    public ZOrderGuard(IntPtr petWindow, ILogger logger)
    {
        _pet = petWindow;
        _logger = logger;
        _callback = OnEvent;
        _hooks.Add(Native.SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback, 0, 0, WinEventOutOfContext));
        _hooks.Add(Native.SetWinEventHook(EventObjectShow, EventObjectReorder, IntPtr.Zero, _callback, 0, 0, WinEventOutOfContext));
        _logger.LogInformation("ZOrderGuard installed ({Count} hooks)", _hooks.Count(h => h != IntPtr.Zero));
    }

    private void OnEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != ObjIdWindow || hwnd == IntPtr.Zero || hwnd == _pet)
        {
            return;
        }

        if (eventType != EventSystemForeground && !IsShell(hwnd))
        {
            return;
        }

        var petZ = Native.ZIndex(_pet);
        var tray = Native.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero || Native.ZIndex(tray) > petZ)
        {
            return;
        }

        Native.ForceToTopOfTopmostBand(_pet);
        _corrections++;
        _logger.LogInformation("[ZOrder] Taskbar was above the pet after event 0x{Event:X} from {Class}; pet raised (correction #{Count}, petZ {Before} -> {After})",
            eventType, Native.ClassNameOf(hwnd), _corrections, petZ, Native.ZIndex(_pet));
    }

    private static bool IsShell(IntPtr hwnd)
    {
        var cls = Native.ClassNameOf(hwnd);
        return cls.StartsWith("Shell_", StringComparison.Ordinal) || cls is "TrayNotifyWnd" or "MSTaskListWClass";
    }

    public void Dispose()
    {
        foreach (var hook in _hooks.Where(h => h != IntPtr.Zero))
        {
            Native.UnhookWinEvent(hook);
        }

        _hooks.Clear();
    }
}
