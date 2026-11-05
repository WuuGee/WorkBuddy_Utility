using FluentAssertions;
using FocusCatAI.Application.Accessories;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.Accessories;
using FocusCatAI.Infrastructure;

namespace FocusCatAI.Application.Tests.Accessories;

public class AccessoryCatalogTests
{
    private static readonly StaticAccessoryCatalog Catalog = new();

    [Fact]
    public void CatalogListsNoneBowtieHatAndHaloInOrder() =>
        Catalog.All.Select(a => a.Id).Should().Equal("none", "bowtie", "party_hat", "star_halo");

    [Theory]
    [InlineData("none", 1)]
    [InlineData("bowtie", 2)]
    [InlineData("party_hat", 4)]
    [InlineData("star_halo", 6)]
    public void RequiredLevelsMatchTheApprovedPlan(string id, int level) =>
        Catalog.Resolve(id).RequiredLevel.Should().Be(level);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("no-such-accessory")]
    public void BlankOrUnknownIdsResolveToNone(string? id) =>
        Catalog.Resolve(id).Id.Should().Be("none");

    [Fact]
    public void ResolveIsCaseInsensitive() =>
        Catalog.Resolve("BOWTIE").Id.Should().Be("bowtie");

    [Theory]
    [InlineData("bowtie", 1, false)]
    [InlineData("bowtie", 2, true)]
    [InlineData("party_hat", 3, false)]
    [InlineData("party_hat", 4, true)]
    [InlineData("star_halo", 5, false)]
    [InlineData("star_halo", 6, true)]
    [InlineData("none", 1, true)]
    public void UnlockFollowsRequiredLevel(string id, int level, bool expected) =>
        AccessoryUnlockPolicy.IsUnlocked(Catalog.Resolve(id), level).Should().Be(expected);

    [Fact]
    public void LockedLabelShowsTheUnlockLevel() =>
        AccessoryUnlockPolicy.DisplayLabel(Catalog.Resolve("star_halo"), 1).Should().Be("Star Halo (Unlocks at Lv. 6)");

    [Fact]
    public void UnlockedLabelIsThePlainName() =>
        AccessoryUnlockPolicy.DisplayLabel(Catalog.Resolve("star_halo"), 6).Should().Be("Star Halo");

    [Fact]
    public void LockedSelectionFallsBackToNone() =>
        Catalog.Effective("star_halo", 1).Id.Should().Be("none");

    [Fact]
    public void UnlockedSelectionIsKept() =>
        Catalog.Effective("party_hat", 4).Id.Should().Be("party_hat");

    [Fact]
    public void DefaultSettingsSelectNone() =>
        new PetSettings().SelectedAccessoryId.Should().Be("none");

    [Fact]
    public void SanitizeFillsABlankAccessoryWithNone()
    {
        var cleaned = SettingsLimits.Sanitize(new AppSettings { Pet = new PetSettings { SelectedAccessoryId = "  " } });
        cleaned.Pet.SelectedAccessoryId.Should().Be("none");
    }

    [Fact]
    public void SanitizeKeepsALockedAccessoryIdSoSettingsAreNeverRewritten()
    {
        var cleaned = SettingsLimits.Sanitize(new AppSettings { Pet = new PetSettings { SelectedAccessoryId = " star_halo " } });
        cleaned.Pet.SelectedAccessoryId.Should().Be("star_halo");
    }

    [Fact]
    public void PlacerClampsAHatNearTheTopEdgeInsideTheFrame()
    {
        var placement = AccessoryPlacer.Place("party_hat", "Happy", new AccessoryAnchor(37, 6, 37, 32), 9, 8);
        placement.Y.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void AnchorTableWrapsFrameIndexes()
    {
        var table = AccessoryAnchorTable.Parse("{\"animations\":{\"Idle\":[{\"hx\":1,\"hy\":2,\"nx\":3,\"ny\":4},{\"hx\":5,\"hy\":6,\"nx\":7,\"ny\":8}]}}");
        table.Get("Idle", 3)!.Value.HeadX.Should().Be(5);
    }
}
