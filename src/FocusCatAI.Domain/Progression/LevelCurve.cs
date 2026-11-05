namespace FocusCatAI.Domain.Progression;

/// <summary>Snapshot of where a total XP value sits on the level curve.</summary>
public sealed record LevelProgress(int Level, long CurrentLevelXp, long NextLevelXp, double ProgressFraction, bool IsMax);

/// <summary>
/// Pure level curve. Total XP needed to reach level n is 25 * (n - 1) * n, so level n to n + 1 costs 50 * n XP.
/// </summary>
public static class LevelCurve
{
    public const int MaxLevel = 50;

    /// <summary>Total XP at which <paramref name="level"/> is reached (level is clamped to 1..MaxLevel).</summary>
    public static long XpForLevel(int level)
    {
        var n = Math.Clamp(level, 1, MaxLevel);
        return 25L * (n - 1) * n;
    }

    public static int LevelForXp(long totalXp)
    {
        var xp = Math.Max(0, totalXp);
        var level = 1;
        while (level < MaxLevel && xp >= XpForLevel(level + 1))
        {
            level++;
        }

        return level;
    }

    /// <summary>
    /// CurrentLevelXp is the XP earned inside the current level; NextLevelXp is the size of the current level
    /// (the XP needed to reach the next one). At max level both describe a full bar and the fraction is 1.
    /// </summary>
    public static LevelProgress Describe(long totalXp)
    {
        var xp = Math.Max(0, totalXp);
        var level = LevelForXp(xp);
        if (level >= MaxLevel)
        {
            var size = XpForLevel(MaxLevel) - XpForLevel(MaxLevel - 1);
            return new LevelProgress(MaxLevel, size, size, 1.0, true);
        }

        var start = XpForLevel(level);
        var span = XpForLevel(level + 1) - start;
        var into = xp - start;
        return new LevelProgress(level, into, span, (double)into / span, false);
    }
}
