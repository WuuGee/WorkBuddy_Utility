using System.Text.Json;
using FluentAssertions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Infrastructure;
using Xunit;

namespace FocusCatAI.Application.Tests;

public class PetCatalogTests
{
    private readonly StaticPetCatalog _catalog = new();

    [Fact]
    public void AllContainsTheThreeBuiltInPets() =>
        _catalog.All.Select(p => p.Id).Should().Equal("cat", "tuxedo", "ginger");

    [Fact]
    public void IdsAreUnique() =>
        _catalog.All.Select(p => p.Id).Should().OnlyHaveUniqueItems();

    [Fact]
    public void DefaultIsInAllAndIsCat()
    {
        _catalog.All.Should().Contain(_catalog.Default);
        _catalog.Default.Id.Should().Be("cat");
    }

    [Theory]
    [InlineData("tuxedo", "tuxedo")]
    [InlineData("TUXEDO", "tuxedo")]
    [InlineData(" Ginger ", "ginger")]
    [InlineData(null, "cat")]
    [InlineData("", "cat")]
    [InlineData("   ", "cat")]
    [InlineData("dragon", "cat")]
    public void ResolveReturnsMatchOrDefault(string? id, string expected) =>
        _catalog.Resolve(id).Id.Should().Be(expected);
}

public class SelectedPetSettingsTests
{
    [Fact]
    public void DefaultSelectedPetIsCat() =>
        new AppSettings().Pet.SelectedPetId.Should().Be("cat");

    [Fact]
    public void JsonWithoutSelectedPetLoadsAsCat()
    {
        var s = JsonSerializer.Deserialize<AppSettings>("""{ "Pet": { "ShowPet": false } }""");
        s!.Pet.SelectedPetId.Should().Be("cat");
    }

    [Fact]
    public void SelectedPetRoundTripsThroughJson()
    {
        var original = new AppSettings { Pet = new PetSettings { SelectedPetId = "ginger" } };
        var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original));
        back!.Pet.SelectedPetId.Should().Be("ginger");
    }

    [Fact]
    public void SanitizeReplacesBlankPetIdWithDefault()
    {
        var s = SettingsLimits.Sanitize(new AppSettings { Pet = new PetSettings { SelectedPetId = " " } });
        s.Pet.SelectedPetId.Should().Be("cat");
    }
}

public class PetAssetIntegrityTests
{
    private static readonly string[] RequiredKeys =
        ["Idle", "Sit", "Blink", "TailFlick", "LookLeft", "LookRight", "Groom", "Stretch", "WalkRight", "WalkLeft", "Sleep", "Think", "Speak", "Happy"];

    private static string PetsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "FocusCatAI.Presentation", "Assets", "Pets")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "src", "FocusCatAI.Presentation", "Assets", "Pets");
    }

    public static IEnumerable<object[]> PetIds() => new StaticPetCatalog().All.Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(PetIds))]
    public void ManifestHasAllAnimationsAndExistingSheets(string id)
    {
        var folder = Path.Combine(PetsRoot(), id);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "pet.json")), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        doc.RootElement.GetProperty("id").GetString().Should().Be(id);
        var animations = doc.RootElement.GetProperty("animations");

        foreach (var key in RequiredKeys)
        {
            animations.TryGetProperty(key, out var anim).Should().BeTrue($"{id} needs '{key}'");
            File.Exists(Path.Combine(folder, anim.GetProperty("sheet").GetString()!)).Should().BeTrue($"{id}/{key} sheet must exist");
        }
    }
}
