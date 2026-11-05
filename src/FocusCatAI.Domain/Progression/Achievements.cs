namespace FocusCatAI.Domain.Progression;

/// <summary>A quiet milestone. <see cref="IsMet"/> is evaluated purely from <see cref="ProgressionState"/>.</summary>
public sealed record AchievementDefinition(string Id, string Title, string Description, Func<ProgressionState, bool> IsMet);

/// <summary>The built-in achievements, in display order.</summary>
public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementDefinition> All { get; } =
    [
        new("first_steps", "First Steps", "Reach companion level 2.", s => Level(s) >= 2),
        new("full_house", "Full House", "Unlock all three companions (level 3).", s => Level(s) >= 3),
        new("trusted_friend", "Trusted Friend", "Reach companion level 5.", s => Level(s) >= 5),
        new("old_friends", "Old Friends", "Reach companion level 10.", s => Level(s) >= 10),
        new("gentle_pats", "Gentle Pats", "Earn your first interaction bonus.", s => s.LastInteractionUtc is not null || s.InteractionDateUtc is not null),
        new("first_hour", "First Hour", "Spend 1 hour together.", s => s.TotalCompanionshipMinutes >= 60),
        new("workday_buddy", "Workday Buddy", "Spend 8 hours together.", s => s.TotalCompanionshipMinutes >= 480),
        new("long_haul", "Long Haul", "Spend 50 hours together.", s => s.TotalCompanionshipMinutes >= 3000)
    ];

    private static int Level(ProgressionState s) => LevelCurve.LevelForXp(s.TotalXp);
}

/// <summary>Pure achievement evaluation. An achievement already recorded in the state never fires again.</summary>
public static class AchievementEvaluator
{
    /// <summary>Returns the state with newly satisfied achievements recorded, plus those newly satisfied (in catalog order).</summary>
    public static (ProgressionState State, IReadOnlyList<AchievementDefinition> NewlyUnlocked) Apply(ProgressionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var fresh = AchievementCatalog.All
            .Where(a => !state.UnlockedAchievementIds.Contains(a.Id, StringComparer.Ordinal) && a.IsMet(state))
            .ToList();
        if (fresh.Count == 0)
        {
            return (state, fresh);
        }

        var ids = state.UnlockedAchievementIds.Concat(fresh.Select(a => a.Id)).ToList();
        return (state with { UnlockedAchievementIds = ids }, fresh);
    }
}
