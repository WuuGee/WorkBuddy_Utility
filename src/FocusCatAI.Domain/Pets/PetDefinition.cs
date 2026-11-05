namespace FocusCatAI.Domain.Pets;

/// <summary>A selectable built-in companion. <see cref="Id"/> equals its folder name under Assets\Pets.</summary>
/// <param name="RequiredLevel">Companion level at which this pet becomes selectable (1 = available from the start).</param>
public sealed record PetDefinition(string Id, string DisplayName, int RequiredLevel = 1)
{
    /// <summary>Id of the pet used when nothing (or something unknown) is selected.</summary>
    public const string DefaultId = "cat";
}
