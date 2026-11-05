using FluentAssertions;
using FocusCatAI.Domain.Progression;
using Xunit;

namespace FocusCatAI.Application.Tests.Progression;

public class XpGrantPolicyTests
{
    private static readonly DateTime Noon = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TickGrantsOneXpAndOneMinute()
    {
        var r = XpGrantPolicy.GrantCompanionshipTick(new ProgressionState());
        (r.State.TotalXp, r.State.TotalCompanionshipMinutes, r.Granted).Should().Be((1L, 1L, true));
    }

    [Fact]
    public void TickThatCrossesLevelBoundaryLevelsUp()
    {
        var r = XpGrantPolicy.GrantCompanionshipTick(new ProgressionState { TotalXp = 49 });
        (r.LeveledUp, r.OldLevel, r.NewLevel).Should().Be((true, 1, 2));
    }

    [Fact]
    public void TickInsideLevelDoesNotLevelUp() =>
        XpGrantPolicy.GrantCompanionshipTick(new ProgressionState { TotalXp = 10 }).LeveledUp.Should().BeFalse();

    [Fact]
    public void TickAtMaxKeepsAccumulatingWithoutLevelUp()
    {
        var r = XpGrantPolicy.GrantCompanionshipTick(new ProgressionState { TotalXp = 61250, TotalCompanionshipMinutes = 5 });
        (r.State.TotalXp, r.State.TotalCompanionshipMinutes, r.LeveledUp, r.NewLevel).Should().Be((61251L, 6L, false, 50));
    }

    [Fact]
    public void TickDoesNotOverflow()
    {
        var r = XpGrantPolicy.GrantCompanionshipTick(new ProgressionState { TotalXp = long.MaxValue });
        r.State.TotalXp.Should().Be(long.MaxValue);
    }

    [Fact]
    public void FirstInteractionGrantsTwoXp()
    {
        var r = XpGrantPolicy.GrantInteraction(new ProgressionState(), Noon);
        (r.Granted, r.State.TotalXp, r.State.DailyInteractionXp).Should().Be((true, 2L, 2));
    }

    [Fact]
    public void InteractionWithinCooldownIsDenied()
    {
        var first = XpGrantPolicy.GrantInteraction(new ProgressionState(), Noon).State;
        var r = XpGrantPolicy.GrantInteraction(first, Noon.AddSeconds(29));
        (r.Granted, r.State.TotalXp).Should().Be((false, 2L));
    }

    [Fact]
    public void InteractionAfterCooldownIsGranted()
    {
        var first = XpGrantPolicy.GrantInteraction(new ProgressionState(), Noon).State;
        var r = XpGrantPolicy.GrantInteraction(first, Noon.AddSeconds(30));
        (r.Granted, r.State.TotalXp).Should().Be((true, 4L));
    }

    [Fact]
    public void InteractionIsDeniedOnceDailyCapIsReached()
    {
        var state = new ProgressionState { InteractionDateUtc = DateOnly.FromDateTime(Noon), DailyInteractionXp = 30 };
        XpGrantPolicy.GrantInteraction(state, Noon).Granted.Should().BeFalse();
    }

    [Fact]
    public void InteractionNeverExceedsDailyCap()
    {
        var state = new ProgressionState { InteractionDateUtc = DateOnly.FromDateTime(Noon), DailyInteractionXp = 29 };
        var r = XpGrantPolicy.GrantInteraction(state, Noon);
        (r.State.DailyInteractionXp, r.State.TotalXp).Should().Be((30, 1L));
    }

    [Fact]
    public void DailyCapResetsOnNewUtcDay()
    {
        var state = new ProgressionState { InteractionDateUtc = DateOnly.FromDateTime(Noon), DailyInteractionXp = 30, LastInteractionUtc = Noon };
        var r = XpGrantPolicy.GrantInteraction(state, Noon.AddDays(1));
        (r.Granted, r.State.DailyInteractionXp).Should().Be((true, 2));
    }

    [Fact]
    public void ClockMovingBackwardsDoesNotBlockInteraction()
    {
        var state = new ProgressionState { LastInteractionUtc = Noon };
        XpGrantPolicy.GrantInteraction(state, Noon.AddMinutes(-5)).Granted.Should().BeTrue();
    }

    [Fact]
    public void InteractionCanTriggerLevelUp()
    {
        var r = XpGrantPolicy.GrantInteraction(new ProgressionState { TotalXp = 49 }, Noon);
        r.LeveledUp.Should().BeTrue();
    }
}
