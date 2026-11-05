using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Application.Pets;

public static class PetSelection
{
    /// <summary>
    /// The pet that should actually be shown: the stored selection when it exists and is unlocked at <paramref name="level"/>,
    /// otherwise the default pet. Never changes the stored setting.
    /// </summary>
    public static PetDefinition Effective(this IPetCatalog catalog, string? selectedId, int level)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var pet = catalog.Resolve(selectedId);
        return PetUnlockPolicy.IsUnlocked(pet, level) ? pet : catalog.Default;
    }
}
