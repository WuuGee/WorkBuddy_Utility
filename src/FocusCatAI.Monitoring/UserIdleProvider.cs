using System.Runtime.InteropServices;
using FocusCatAI.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FocusCatAI.Monitoring;

/// <summary>Reads only the time of the last input event; never what was typed or clicked.</summary>
public sealed class UserIdleProvider : IUserIdleProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    public TimeSpan IdleTime
    {
        get
        {
            var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info))
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddMonitoring(this IServiceCollection services)
    {
        services.AddSingleton<IUserIdleProvider, UserIdleProvider>();
        services.AddSingleton<IForegroundAppProvider, ForegroundAppProvider>();
        return services;
    }
}

