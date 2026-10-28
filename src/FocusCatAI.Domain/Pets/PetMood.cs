namespace FocusCatAI.Domain.Pets;

public enum PetMood
{
    Relaxed,
    Curious,
    Happy,
    Sleepy,
    Focused
}

public enum PetBehaviorType
{
    Roaming,
    Sitting,
    Sleeping,
    Observing,
    Following,
    Playing,
    Reacting
}

/// <summary>Pet height in pixels. In taskbar mode it is capped to the taskbar height.</summary>
public enum PetSize
{
    Small = 32,
    Normal = 48,
    Large = 64,
    Huge = 96
}

/// <param name="Key">"Start", "Clock", "Search" or "App:&lt;part of the app name&gt;".</param>
public sealed record FavoriteSpot(string Key, double Weight);

/// <summary>A short idle animation played while the pet dwells, then <paramref name="NextDelay"/> seconds until the next one.</summary>
public sealed record IdleFidget(PetActivity Activity, double Seconds, double NextDelay);

/// <summary>
/// What the pet does next. When <paramref name="TargetX"/> is set the pet walks there first,
/// then performs <paramref name="Activity"/> for <paramref name="Seconds"/>.
/// </summary>
public sealed record BehaviorPlan(
    PetBehaviorType Type,
    PetState State,
    PetActivity Activity,
    double Seconds,
    double? TargetX = null,
    double SpeedMultiplier = 1.0,
    bool? FacingRight = null,
    string? FavoriteKey = null);
