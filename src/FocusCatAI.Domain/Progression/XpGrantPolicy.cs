namespace FocusCatAI.Domain.Progression;

public sealed record GrantResult(ProgressionState State, bool Granted, bool LeveledUp, int OldLevel, int NewLevel);

/// <summary>Pure XP grant rules: no clock, no timers, no I/O. Callers pass the current UTC time.</summary>
public static class XpGrantPolicy
{
    public const int TickXp = 1;
    public const int TickMinutes = 1;
    public const int InteractionXp = 2;
    public static readonly TimeSpan InteractionCooldown = TimeSpan.FromSeconds(30);
    public const int DailyInteractionCap = 30;

    public static GrantResult GrantCompanionshipTick(ProgressionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var next = state with
        {
            TotalXp = SafeAdd(state.TotalXp, TickXp),
            TotalCompanionshipMinutes = SafeAdd(state.TotalCompanionshipMinutes, TickMinutes)
        };
        return Result(state, next);
    }

    public static GrantResult GrantInteraction(ProgressionState state, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(state);
        var today = DateOnly.FromDateTime(nowUtc);
        var earnedToday = state.InteractionDateUtc == today ? state.DailyInteractionXp : 0;

        if (state.LastInteractionUtc is { } last && nowUtc >= last && nowUtc - last < InteractionCooldown)
        {
            return Denied(state);
        }

        var amount = Math.Min(InteractionXp, DailyInteractionCap - earnedToday);
        if (amount <= 0)
        {
            return Denied(state);
        }

        var next = state with
        {
            TotalXp = SafeAdd(state.TotalXp, amount),
            LastInteractionUtc = nowUtc,
            InteractionDateUtc = today,
            DailyInteractionXp = earnedToday + amount
        };
        return Result(state, next);
    }

    private static GrantResult Denied(ProgressionState state)
    {
        var level = LevelCurve.LevelForXp(state.TotalXp);
        return new GrantResult(state, false, false, level, level);
    }

    private static GrantResult Result(ProgressionState before, ProgressionState after)
    {
        var oldLevel = LevelCurve.LevelForXp(before.TotalXp);
        var newLevel = LevelCurve.LevelForXp(after.TotalXp);
        return new GrantResult(after, true, newLevel > oldLevel, oldLevel, newLevel);
    }

    private static long SafeAdd(long value, long add) => value > long.MaxValue - add ? long.MaxValue : Math.Max(0, value) + add;
}
