using System.Diagnostics;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Shared;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Presentation.Services;

/// <summary>Controlled exit and restart.</summary>
public sealed class AppLifecycle(IPetPreferences preferences, ILogger<AppLifecycle> logger)
{
    public void Exit() => System.Windows.Application.Current.Shutdown();

    /// <summary>
    /// Saves, starts a replacement process, then shuts this one down. The replacement is told to wait for this process
    /// to finish (<c>--restart-after</c>) so it never loses the single-instance race.
    /// Settings are written on every change, so only preferences need an explicit save.
    /// </summary>
    public bool Restart(params string[] extraArguments)
    {
        try
        {
            preferences.Save();
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path.");
            var info = new ProcessStartInfo(exe) { UseShellExecute = false };
            info.ArgumentList.Add(StartupOptions.RestartAfterArgument);
            info.ArgumentList.Add(Environment.ProcessId.ToString());
            foreach (var argument in extraArguments)
            {
                info.ArgumentList.Add(argument);
            }

            Process.Start(info)?.Dispose();
            logger.LogInformation("Restarting: replacement process started, shutting down pid {Pid}", Environment.ProcessId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogError(ex, "Restart failed; staying open");
            return false;
        }

        Exit();
        return true;
    }
}
