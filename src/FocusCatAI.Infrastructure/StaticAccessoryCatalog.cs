using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Accessories;

namespace FocusCatAI.Infrastructure;

/// <summary>The built-in accessories, in picker order.</summary>
public sealed class StaticAccessoryCatalog : IAccessoryCatalog
{
    public IReadOnlyList<AccessoryDefinition> All { get; } =
    [
        new(AccessoryDefinition.NoneId, "None"),
        new("bowtie", "Red Bowtie", 2),
        new("party_hat", "Party Hat", 4),
        new("star_halo", "Star Halo", 6)
    ];

    public AccessoryDefinition Default => All[0];

    public AccessoryDefinition Resolve(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? Default
            : All.FirstOrDefault(a => string.Equals(a.Id, id.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;
}
