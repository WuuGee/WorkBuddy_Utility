namespace FocusCatAI.Application.Abstractions;

/// <summary>Opens things outside the app (Explorer, the default browser) so presentation code never starts processes or knows disk paths.</summary>
public interface IExternalLauncher
{
    /// <summary>Opens the log folder in Explorer, creating it first if needed. Never throws.</summary>
    void OpenLogsFolder();

    /// <summary>Opens the data folder (settings and preferences) in Explorer. Never throws.</summary>
    void OpenDataFolder();

    /// <summary>Opens an https URL in the default browser. Anything else is refused. Never throws.</summary>
    void OpenUrl(string url);
}
