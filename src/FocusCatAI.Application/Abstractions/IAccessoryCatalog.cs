using FocusCatAI.Domain.Accessories;

namespace FocusCatAI.Application.Abstractions;

/// <summary>Stateless list of the built-in accessories. The selected id lives in settings.</summary>
public interface IAccessoryCatalog
{
    IReadOnlyList<AccessoryDefinition> All { get; }

    AccessoryDefinition Default { get; }

    /// <summary>Case-insensitive lookup; null, blank or unknown ids return <see cref="Default"/> (none).</summary>
    AccessoryDefinition Resolve(string? id);
}
