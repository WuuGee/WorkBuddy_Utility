namespace FocusCatAI.Domain.Pets;

/// <summary>Pure, derived pet unlocks: a pet is available once the companion level reaches its required level. Nothing is stored.</summary>
public static class PetUnlockPolicy
{
    public static bool IsUnlocked(PetDefinition pet, int level)
    {
        ArgumentNullException.ThrowIfNull(pet);
        return level >= pet.RequiredLevel;
    }

    /// <summary>Picker/menu text: the plain name, or "Name (Unlocks at Lv. N)" while locked.</summary>
    public static string DisplayLabel(PetDefinition pet, int level) =>
        IsUnlocked(pet, level) ? pet.DisplayName : $"{pet.DisplayName} (Unlocks at Lv. {pet.RequiredLevel})";
}
