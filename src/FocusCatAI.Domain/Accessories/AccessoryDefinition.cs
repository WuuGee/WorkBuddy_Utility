namespace FocusCatAI.Domain.Accessories;

/// <summary>A selectable cosmetic overlay. <see cref="Id"/> equals its file name under Assets\Accessories.</summary>
/// <param name="RequiredLevel">Companion level at which this accessory becomes selectable (1 = always).</param>
public sealed record AccessoryDefinition(string Id, string DisplayName, int RequiredLevel = 1)
{
    /// <summary>Id meaning "no accessory"; also the fallback for unknown or locked ids.</summary>
    public const string NoneId = "none";
}

/// <summary>Pure, derived accessory unlocks, same pattern as pets. Nothing is stored.</summary>
public static class AccessoryUnlockPolicy
{
    public static bool IsUnlocked(AccessoryDefinition accessory, int level)
    {
        ArgumentNullException.ThrowIfNull(accessory);
        return level >= accessory.RequiredLevel;
    }

    public static string DisplayLabel(AccessoryDefinition accessory, int level) =>
        IsUnlocked(accessory, level) ? accessory.DisplayName : $"{accessory.DisplayName} (Unlocks at Lv. {accessory.RequiredLevel})";
}
