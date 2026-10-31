using System.Text.Json.Serialization;
using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Application.Settings;

/// <summary>Where the pet lives. Auto = in the taskbar on Windows 11, above it elsewhere.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlacementPreference
{
    InTaskbar,
    AboveTaskbar,
    Auto
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageFrequency
{
    Low,
    Normal,
    High
}

/// <summary>
/// Root of settings.json. Every property has a default, so a file written by an older version (missing keys)
/// or a newer one (extra keys) still loads. Records are immutable: change them with <c>with</c> and
/// <see cref="Abstractions.ISettingsService.Update"/>.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Bump when the file layout changes and add a step to the migrator.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public GeneralSettings General { get; init; } = new();
    public PetSettings Pet { get; init; } = new();
    public BehaviorSettings Behavior { get; init; } = new();
    public QuietHoursSettings QuietHours { get; init; } = new();
    public NotificationSettings Notifications { get; init; } = new();
}

public sealed record GeneralSettings
{
    /// <summary>Stored now; applied to the Windows Run key by the startup-management step.</summary>
    public bool StartWithWindows { get; init; } = true;

    /// <summary>Launch with the cat hidden (tray only) for this session; does not change <see cref="PetSettings.ShowPet"/>.</summary>
    public bool StartMinimized { get; init; }

    /// <summary>When off, the pet floats above the taskbar instead of walking along it, whatever the placement says.</summary>
    public bool RunInTaskbarMode { get; init; } = true;

    /// <summary>Placeholder for future localisation.</summary>
    public string Language { get; init; } = "en";

    /// <summary>Light, Dark, or follow Windows. Unknown values in settings.json load as System.</summary>
    [JsonConverter(typeof(ThemePreferenceConverter))]
    public ThemePreference Theme { get; init; } = ThemePreference.System;
}

public sealed record PetSettings
{
    public bool ShowPet { get; init; } = true;
    public PetSize Size { get; init; } = PetSize.Normal;
    public double AnimationSpeed { get; init; } = 1.0;
    public PlacementPreference Placement { get; init; } = PlacementPreference.Auto;
    public bool EnableIntroAnimation { get; init; } = true;

    /// <summary>Id of the selected pet; validated against the catalog where it is used.</summary>
    public string SelectedPetId { get; init; } = PetDefinition.DefaultId;
}

public sealed record BehaviorSettings
{
    public bool EnableMessages { get; init; } = true;
    public MessageFrequency MessageFrequency { get; init; } = MessageFrequency.Normal;
    public bool EnableSleep { get; init; } = true;
    public int SleepDelaySeconds { get; init; } = 300;
}

public sealed record QuietHoursSettings
{
    public bool Enabled { get; init; }
    public TimeOnly Start { get; init; } = new(22, 0);
    public TimeOnly End { get; init; } = new(7, 0);
}

public sealed record NotificationSettings
{
    public bool EnableProductivityMessages { get; init; } = true;
    public bool EnablePomodoroMessages { get; init; } = true;
}

/// <summary>Limits applied when settings are read, so a hand-edited file cannot break the pet.</summary>
public static class SettingsLimits
{
    public const double MinAnimationSpeed = 0.5;
    public const double MaxAnimationSpeed = 2.0;
    public const int MinSleepDelaySeconds = 5;
    public const int MaxSleepDelaySeconds = 3600;

    public static AppSettings Sanitize(AppSettings settings) => settings with
    {
        Pet = settings.Pet with
        {
            AnimationSpeed = double.IsFinite(settings.Pet.AnimationSpeed)
                ? Math.Clamp(settings.Pet.AnimationSpeed, MinAnimationSpeed, MaxAnimationSpeed)
                : 1.0,
            Size = Enum.IsDefined(settings.Pet.Size) ? settings.Pet.Size : PetSize.Normal,
            SelectedPetId = string.IsNullOrWhiteSpace(settings.Pet.SelectedPetId) ? PetDefinition.DefaultId : settings.Pet.SelectedPetId.Trim()
        },
        Behavior = settings.Behavior with
        {
            SleepDelaySeconds = Math.Clamp(settings.Behavior.SleepDelaySeconds, MinSleepDelaySeconds, MaxSleepDelaySeconds)
        }
    };
}

/// <summary>Quiet-hours maths, kept pure so it can be tested without a clock.</summary>
public static class QuietHours
{
    /// <summary>True when <paramref name="now"/> is inside [start, end). Handles ranges that cross midnight; start == end means never quiet.</summary>
    public static bool IsQuiet(QuietHoursSettings quiet, TimeOnly now)
    {
        if (!quiet.Enabled || quiet.Start == quiet.End)
        {
            return false;
        }

        return quiet.Start < quiet.End
            ? now >= quiet.Start && now < quiet.End
            : now >= quiet.Start || now < quiet.End;
    }
}
