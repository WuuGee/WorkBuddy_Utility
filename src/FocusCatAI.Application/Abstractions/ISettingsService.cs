using FocusCatAI.Application.Settings;

namespace FocusCatAI.Application.Abstractions;

/// <summary>Owns settings.json: loads it, validates it, applies changes live and persists them.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Raised after <see cref="Current"/> changed (any thread; UI code must marshal).</summary>
    event EventHandler<AppSettings>? Changed;

    /// <summary>Applies <paramref name="change"/> to the current settings, sanitizes, saves atomically and raises <see cref="Changed"/>.</summary>
    void Update(Func<AppSettings, AppSettings> change);

    /// <summary>Restores every setting to its default (favorites and the intro flag live in pet-preferences.json and are not touched).</summary>
    void ResetToDefaults();
}
