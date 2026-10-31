using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Infrastructure;

/// <summary>The built-in pets, in picker order.</summary>
public sealed class StaticPetCatalog : IPetCatalog
{
    public IReadOnlyList<PetDefinition> All { get; } =
    [
        new(PetDefinition.DefaultId, "Cream Cat"),
        new("tuxedo", "Tuxedo Cat"),
        new("ginger", "Ginger Cat")
    ];

    public PetDefinition Default => All[0];

    public PetDefinition Resolve(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? Default
            : All.FirstOrDefault(p => string.Equals(p.Id, id.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;
}
