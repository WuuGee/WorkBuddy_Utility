using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Accessories;

namespace FocusCatAI.Application.Accessories;

public static class AccessorySelection
{
    /// <summary>The stored accessory if it exists and is unlocked at <paramref name="level"/>, otherwise none. Never changes settings.</summary>
    public static AccessoryDefinition Effective(this IAccessoryCatalog catalog, string? selectedId, int level)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var accessory = catalog.Resolve(selectedId);
        return AccessoryUnlockPolicy.IsUnlocked(accessory, level) ? accessory : catalog.Default;
    }
}
