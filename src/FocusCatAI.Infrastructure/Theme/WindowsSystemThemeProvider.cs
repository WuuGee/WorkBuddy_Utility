using FocusCatAI.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FocusCatAI.Infrastructure.Theme;

/// <summary>Reads AppsUseLightTheme from the registry. Missing or unreadable means Light.</summary>
public sealed class WindowsSystemThemeProvider : ISystemThemeProvider
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    private readonly Func<object?> _readValue;
    private readonly ILogger<WindowsSystemThemeProvider> _logger;

    public WindowsSystemThemeProvider(ILogger<WindowsSystemThemeProvider> logger)
        : this(logger, ReadRegistry)
    {
    }

    /// <summary>For tests: supplies the raw registry value.</summary>
    public WindowsSystemThemeProvider(ILogger<WindowsSystemThemeProvider> logger, Func<object?> readValue)
    {
        _logger = logger;
        _readValue = readValue;
    }

    public bool PrefersLightApps()
    {
        try
        {
            return _readValue() switch
            {
                int value => value != 0,
                _ => true
            };
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Could not read the Windows app theme; assuming light");
            return true;
        }
    }

    private static object? ReadRegistry()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName);
    }
}
