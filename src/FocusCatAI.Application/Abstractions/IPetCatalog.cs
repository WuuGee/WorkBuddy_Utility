using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Application.Abstractions;

/// <summary>Stateless list of the built-in pets. The selected id lives in settings, not here.</summary>
public interface IPetCatalog
{
    IReadOnlyList<PetDefinition> All { get; }

    PetDefinition Default { get; }

    /// <summary>Case-insensitive lookup; null, blank or unknown ids return <see cref="Default"/>.</summary>
    PetDefinition Resolve(string? id);
}
