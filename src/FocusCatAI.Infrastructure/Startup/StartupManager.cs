using FocusCatAI.Application.Abstractions;
using FocusCatAI.Shared;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Infrastructure.Startup;

/// <summary>
/// Owns the per-user Run entry: <c>"C:\...\WorkBuddy.exe" --autostart</c>. The registry is the source of truth for
/// what Windows will do, so <see cref="GetStatus"/> always re-reads it and the settings UI never has to guess.
/// </summary>
public sealed class StartupManager(
    IRunKeyStore store,
    string executablePath,
    ILogger<StartupManager> logger,
    string valueName = AppConstants.StartupValueName) : IStartupManager
{
    private readonly string _executablePath = Path.GetFullPath(executablePath);

    public string ExpectedCommand => $"\"{_executablePath}\" {AppConstants.AutostartArgument}";

    public StartupStatus GetStatus()
    {
        string? command;
        try
        {
            command = store.GetCommand(valueName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger.LogWarning(ex, "Could not read the startup registration");
            return new StartupStatus(StartupState.NotRegistered, null, ExpectedCommand);
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return new StartupStatus(StartupState.NotRegistered, null, ExpectedCommand);
        }

        var target = ParseExecutable(command);
        if (target is null || !File.Exists(target))
        {
            return new StartupStatus(StartupState.InvalidPath, command, ExpectedCommand);
        }

        var samePath = string.Equals(SafeFullPath(target), _executablePath, StringComparison.OrdinalIgnoreCase);
        var sameArguments = string.Equals(ParseArguments(command), AppConstants.AutostartArgument, StringComparison.Ordinal);
        if (!samePath || !sameArguments)
        {
            return new StartupStatus(StartupState.PathMismatch, command, ExpectedCommand);
        }

        return store.IsDisabledByWindows(valueName)
            ? new StartupStatus(StartupState.DisabledByWindows, command, ExpectedCommand)
            : new StartupStatus(StartupState.Registered, command, ExpectedCommand);
    }

    public StartupStatus Enable()
    {
        try
        {
            store.SetCommand(valueName, ExpectedCommand);
            store.ClearDisabledByWindows(valueName);
            logger.LogInformation("Startup registration written: {Command}", ExpectedCommand);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger.LogError(ex, "Could not write the startup registration");
        }

        return GetStatus();
    }

    public StartupStatus Disable()
    {
        try
        {
            store.RemoveCommand(valueName);
            store.ClearDisabledByWindows(valueName);
            logger.LogInformation("Startup registration removed");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger.LogError(ex, "Could not remove the startup registration");
        }

        return GetStatus();
    }

    public StartupStatus Synchronize(bool wanted)
    {
        var status = GetStatus();
        if (wanted)
        {
            switch (status.State)
            {
                case StartupState.NotRegistered:
                case StartupState.InvalidPath:
                case StartupState.PathMismatch:
                    logger.LogInformation("Startup registration repaired ({State}); was: {Old}", status.State, status.RegisteredCommand ?? "(none)");
                    return Enable();
            }

            // Registered: nothing to do. DisabledByWindows: the user chose that in Windows; do not fight it.
            return status;
        }

        return status.State == StartupState.NotRegistered ? status : Disable();
    }

    /// <summary>Extracts the executable path from a Run command, quoted or not.</summary>
    public static string? ParseExecutable(string command)
    {
        var text = command.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? text[..(exe + 4)] : text.Split(' ')[0];
    }

    private static string ParseArguments(string command)
    {
        var text = command.Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 0 ? text[(end + 1)..].Trim() : string.Empty;
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? text[(exe + 4)..].Trim() : string.Empty;
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
