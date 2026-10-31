namespace FocusCatAI.Domain.Pets;

/// <summary>A selectable built-in companion. <see cref="Id"/> equals its folder name under Assets\Pets.</summary>
public sealed record PetDefinition(string Id, string DisplayName)
{
    /// <summary>Id of the pet used when nothing (or something unknown) is selected.</summary>
    public const string DefaultId = "cat";
}
