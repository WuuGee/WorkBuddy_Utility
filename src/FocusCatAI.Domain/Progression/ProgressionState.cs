namespace FocusCatAI.Domain.Progression;

/// <summary>Persisted companion progression. The level is derived from <see cref="TotalXp"/> and never stored.</summary>
public sealed record ProgressionState
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public long TotalXp { get; init; }
    public long TotalCompanionshipMinutes { get; init; }
    public DateTime? LastInteractionUtc { get; init; }
    public DateOnly? InteractionDateUtc { get; init; }
    public int DailyInteractionXp { get; init; }

    /// <summary>Ids of achievements already unlocked, so they never fire twice. Unknown ids from newer versions are kept.</summary>
    public IReadOnlyList<string> UnlockedAchievementIds { get; init; } = [];

    // Record equality would compare the list by reference, so equality is written out and compares it by content.
    public bool Equals(ProgressionState? other) =>
        other is not null
        && SchemaVersion == other.SchemaVersion
        && TotalXp == other.TotalXp
        && TotalCompanionshipMinutes == other.TotalCompanionshipMinutes
        && LastInteractionUtc == other.LastInteractionUtc
        && InteractionDateUtc == other.InteractionDateUtc
        && DailyInteractionXp == other.DailyInteractionXp
        && UnlockedAchievementIds.SequenceEqual(other.UnlockedAchievementIds);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion);
        hash.Add(TotalXp);
        hash.Add(TotalCompanionshipMinutes);
        hash.Add(LastInteractionUtc);
        hash.Add(InteractionDateUtc);
        hash.Add(DailyInteractionXp);
        foreach (var id in UnlockedAchievementIds)
        {
            hash.Add(id);
        }

        return hash.ToHashCode();
    }
}
