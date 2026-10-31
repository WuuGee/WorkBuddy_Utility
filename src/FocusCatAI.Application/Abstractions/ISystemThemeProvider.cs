namespace FocusCatAI.Application.Abstractions;

/// <summary>Whether Windows is set to light app mode. Implementations must never throw.</summary>
public interface ISystemThemeProvider
{
    /// <summary>True when apps should be light. Falls back to true if Windows does not say.</summary>
    bool PrefersLightApps();
}
