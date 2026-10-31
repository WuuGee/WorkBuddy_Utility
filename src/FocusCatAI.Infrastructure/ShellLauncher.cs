using System.ComponentModel;
using System.Diagnostics;
using FocusCatAI.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Infrastructure;

public sealed class ShellLauncher(IAppPaths paths, ILogger<ShellLauncher> logger) : IExternalLauncher
{
    public void OpenLogsFolder() => OpenFolder(paths.LogsDirectory);

    public void OpenDataFolder() => OpenFolder(paths.RootDirectory);

    public void OpenUrl(string url)
    {
        if (!IsAllowedUrl(url))
        {
            logger.LogWarning("Refused to open a non-https URL");
            return;
        }

        Launch(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>Only absolute https URLs may be opened, whatever text reaches the launcher.</summary>
    public static bool IsAllowedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Launch(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(ex, "Could not open folder {Path}", path);
        }
    }

    private void Launch(ProcessStartInfo info)
    {
        try
        {
            Process.Start(info)?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(ex, "Could not launch {Target}", info.FileName);
        }
    }
}
