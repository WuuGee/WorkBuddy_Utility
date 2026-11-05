using FluentAssertions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Progression;
using FocusCatAI.Infrastructure;
using Xunit;

namespace FocusCatAI.Application.Tests.Progression;

public class UnlockablesAndAchievementsTests
{
    private static readonly StaticPetCatalog Catalog = new();

    [Theory]
    [InlineData("cat", 1, true)]
    [InlineData("tuxedo", 1, false)]
    [InlineData("tuxedo", 2, true)]
    [InlineData("ginger", 2, false)]
    [InlineData("ginger", 3, true)]
    public void PetUnlockFollowsRequiredLevel(string id, int level, bool expected) =>
        PetUnlockPolicy.IsUnlocked(Catalog.Resolve(id), level).Should().Be(expected);

    [Fact]
    public void LockedPetLabelShowsRequiredLevel() =>
        PetUnlockPolicy.DisplayLabel(Catalog.Resolve("ginger"), 1).Should().Be("Ginger Cat (Unlocks at Lv. 3)");

    [Fact]
    public void UnlockedPetLabelIsPlainName() =>
        PetUnlockPolicy.DisplayLabel(Catalog.Resolve("ginger"), 3).Should().Be("Ginger Cat");

    [Fact]
    public void LockedSelectionFallsBackToDefault() =>
        Catalog.Effective("ginger", 1).Id.Should().Be("cat");

    [Fact]
    public void UnlockedSelectionIsKept() =>
        Catalog.Effective("ginger", 3).Id.Should().Be("ginger");

    [Fact]
    public void UnknownSelectionFallsBackToDefault() =>
        Catalog.Effective("nope", 50).Id.Should().Be("cat");

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(50, 0, 1)]
    [InlineData(150, 0, 2)]
    public void XpJumpUnlocksLevelAchievements(long xp, long minutes, int expectedCount)
    {
        var (state, _) = AchievementEvaluator.Apply(new ProgressionState { TotalXp = xp, TotalCompanionshipMinutes = minutes });
        state.UnlockedAchievementIds.Should().HaveCount(expectedCount);
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    public void FirstHourNeedsSixtyMinutes(long minutes, bool expected)
    {
        var (state, _) = AchievementEvaluator.Apply(new ProgressionState { TotalCompanionshipMinutes = minutes });
        state.UnlockedAchievementIds.Contains("first_hour").Should().Be(expected);
    }

    [Fact]
    public void GentlePatsUnlocksAfterFirstInteraction()
    {
        var (state, _) = AchievementEvaluator.Apply(new ProgressionState { LastInteractionUtc = DateTime.UtcNow });
        state.UnlockedAchievementIds.Should().Contain("gentle_pats");
    }

    [Fact]
    public void AchievementsDoNotFireTwice()
    {
        var (first, _) = AchievementEvaluator.Apply(new ProgressionState { TotalCompanionshipMinutes = 60 });
        var (_, again) = AchievementEvaluator.Apply(first);
        again.Should().BeEmpty();
    }

    [Fact]
    public void CatalogHasEightAchievements() =>
        AchievementCatalog.All.Should().HaveCount(8);

    [Fact]
    public void StateEqualityComparesAchievementsByContent()
    {
        var a = new ProgressionState { UnlockedAchievementIds = ["x", "y"] };
        var b = new ProgressionState { UnlockedAchievementIds = new List<string> { "x", "y" } };
        (a.Equals(b) && a.GetHashCode() == b.GetHashCode()).Should().BeTrue();
    }

    [Fact]
    public void StateEqualityDetectsDifferentAchievements()
    {
        var a = new ProgressionState { UnlockedAchievementIds = ["x"] };
        var b = new ProgressionState { UnlockedAchievementIds = ["y"] };
        a.Equals(b).Should().BeFalse();
    }
}
