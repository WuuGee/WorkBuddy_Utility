namespace FocusCatAI.Application.Abstractions;

/// <summary>What Windows will actually do at the next sign-in, as read from the registry.</summary>
public enum StartupState
{
    /// <summary>No Run entry. WorkBuddy will not start automatically.</summary>
    NotRegistered,

    /// <summary>A Run entry points at this executable with --autostart, and Windows has not disabled it.</summary>
    Registered,

    /// <summary>A Run entry exists but points at a different copy of WorkBuddy or has different arguments.</summary>
    PathMismatch,

    /// <summary>A Run entry exists but its executable no longer exists.</summary>
    InvalidPath,

    /// <summary>The entry is correct, but Windows (Task Manager / Settings > Startup apps) has turned it off.</summary>
    DisabledByWindows
}

public sealed record StartupStatus(StartupState State, string? RegisteredCommand, string ExpectedCommand)
{
    /// <summary>True only when Windows will really start WorkBuddy at sign-in.</summary>
    public bool WillStartAtSignIn => State == StartupState.Registered;
}

/// <summary>Per-user (HKCU) start-with-Windows registration. Never needs administrator rights.</summary>
public interface IStartupManager
{
    StartupStatus GetStatus();

    /// <summary>Writes the Run entry for the current executable and clears any Windows "disabled" flag.</summary>
    StartupStatus Enable();

    /// <summary>Removes the Run entry. Safe to call when it does not exist.</summary>
    StartupStatus Disable();

    /// <summary>
    /// Brings the registry in line with the setting. Repairs missing, moved or invalid entries when wanted, removes the
    /// entry when not wanted, and never overrides a user's choice made in Windows' own startup settings.
    /// </summary>
    StartupStatus Synchronize(bool wanted);
}
