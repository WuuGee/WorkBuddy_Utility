using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FocusCatAI.Application.Abstractions;

namespace FocusCatAI.Monitoring;

public sealed class ForegroundAppProvider(MonitoringOptions options) : IForegroundAppProvider
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);

    public ForegroundApp? GetForeground()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || GetWindowThreadProcessId(hwnd, out var pid) == 0)
            {
                return null;
            }

            using var process = Process.GetProcessById((int)pid);
            string? title = null;
            if (options.DetectWebsiteTitles)
            {
                var builder = new StringBuilder(256);
                GetWindowText(hwnd, builder, builder.Capacity);
                title = builder.ToString();
            }

            return new ForegroundApp(process.ProcessName, title);
        }
        catch
        {
            return null;
        }
    }
}
