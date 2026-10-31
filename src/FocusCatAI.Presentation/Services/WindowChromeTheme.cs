using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Makes the native title bar match the theme. WPF resources do not reach the non-client area, so this uses DWM.
/// Unsupported Windows builds are a silent no-op (the title bar just stays light).
/// </summary>
public static class WindowChromeTheme
{
    private const int DwmaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmaUseImmersiveDarkMode = 20;
    private const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010, SwpFrameChanged = 0x0020;

    /// <summary>Applies the title-bar mode to a window; safe before or after it is shown.</summary>
    public static void Apply(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            // Not created yet: apply as soon as it is.
            window.SourceInitialized += (_, _) => Apply(window, dark);
            return;
        }

        try
        {
            var value = dark ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, DwmaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DwmaUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
            }

            // Repaint the frame now, without moving, resizing, re-stacking or activating the window.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // dwmapi unavailable: leave the default title bar.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
