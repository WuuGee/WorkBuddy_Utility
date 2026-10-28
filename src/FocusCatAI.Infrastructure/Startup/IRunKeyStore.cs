namespace FocusCatAI.Infrastructure.Startup;

/// <summary>The registry operations <see cref="StartupManager"/> needs. Abstracted so the logic is testable.</summary>
public interface IRunKeyStore
{
    string? GetCommand(string valueName);

    void SetCommand(string valueName, string command);

    void RemoveCommand(string valueName);

    /// <summary>True when Windows' StartupApproved key marks the entry as disabled.</summary>
    bool IsDisabledByWindows(string valueName);

    void ClearDisabledByWindows(string valueName);
}
