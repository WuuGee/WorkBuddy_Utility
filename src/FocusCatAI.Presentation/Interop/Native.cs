using System.Runtime.InteropServices;

namespace FocusCatAI.Presentation.Interop;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    private const int GwlExStyle = -20;
    public const int WsExTransparent = 0x20;
    public const int WsExToolWindow = 0x80;
    public const int WsExNoActivate = 0x08000000;

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoActivate = 0x10;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? windowName);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("shell32.dll")]
    public static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    /// <summary>System DPI scale (1.0 = 96 dpi). WPF DIPs = physical pixels / scale.</summary>
    public static double DpiScale
    {
        get
        {
            try
            {
                return GetDpiForSystem() / 96.0;
            }
            catch
            {
                return 1.0;
            }
        }
    }

    public static void SetExStyle(IntPtr hwnd, int flag, bool on)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(hwnd, GwlExStyle);
        var next = on ? style | flag : style & ~flag;
        if (next != style)
        {
            SetWindowLong(hwnd, GwlExStyle, next);
        }
    }

    /// <summary>Re-asserts topmost order without activating; needed because the taskbar also claims topmost.</summary>
    public static void KeepTopmost(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
    }

    private static readonly IntPtr HwndNoTopmost = new(-2);
    private const uint GwHwndNext = 2, GwHwndPrev = 3;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder text, int max);

    /// <summary>Window style (GWL_STYLE = -16) or extended style (GWL_EXSTYLE = -20).</summary>
    public static int GetStyle(IntPtr hwnd, bool extended) => GetWindowLong(hwnd, extended ? GwlExStyle : -16);

    /// <summary>Class name plus handle and visibility, for z-order logs.</summary>
    public static string Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "(none)";
        }

        var sb = new System.Text.StringBuilder(128);
        GetClassName(hwnd, sb, sb.Capacity);
        return $"0x{hwnd.ToInt64():X} {sb} visible={IsWindowVisible(hwnd)} exStyle=0x{GetStyle(hwnd, true):X}";
    }

    public static IntPtr WindowAbove(IntPtr hwnd) => GetWindow(hwnd, GwHwndPrev);

    public static IntPtr WindowBelow(IntPtr hwnd) => GetWindow(hwnd, GwHwndNext);

    /// <summary>Number of windows above <paramref name="hwnd"/> in the global z-order (0 = top-most window).</summary>
    public static int ZIndex(IntPtr hwnd)
    {
        var count = 0;
        for (var h = WindowAbove(hwnd); h != IntPtr.Zero && count < 10000; h = WindowAbove(h))
        {
            count++;
        }

        return count;
    }

    /// <summary>Drops and re-acquires the topmost flag so the window lands on top of the topmost band, above the taskbar.</summary>
    public static void ForceToTopOfTopmostBand(IntPtr hwnd)
    {
        const uint flags = SwpNoSize | SwpNoMove | SwpNoActivate;
        SetWindowPos(hwnd, HwndNoTopmost, 0, 0, 0, 0, flags);
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, flags);
    }

    public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint pid, uint thread, uint flags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hook);

    public static string ClassNameOf(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(64);
        return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }
}
