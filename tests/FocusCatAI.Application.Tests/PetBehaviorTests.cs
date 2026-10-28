using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Taskbar;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FocusCatAI.Application.Tests;

public class PetBehaviorTests
{
    private static PetWorld World(params TaskbarElement[] elements) => new()
    {
        PetWidth = 64,
        PetHeight = 48,
        PetX = 100,
        WalkSpeed = 50,
        Layout = new TaskbarLayout(TaskbarPlacementMode.InTaskbar, 0, 1000, 1000, 1048, elements)
    };

    private static IAnimationCatalog Catalog()
    {
        var catalog = Substitute.For<IAnimationCatalog>();
        catalog.Has(Arg.Any<PetActivity>()).Returns(true);
        catalog.WithTag(Arg.Any<string>()).Returns(new List<PetActivity>());
        return catalog;
    }

    private static IPetPreferences Prefs()
    {
        var prefs = Substitute.For<IPetPreferences>();
        prefs.Favorites.Returns(new List<FavoriteSpot> { new("Clock", 1) });
        return prefs;
    }

    private static IRandomSource Random(double value = 0.5, int next = 0)
    {
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(value);
        random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(next);
        return random;
    }

    private static IMoodService Mood(PetMood mood)
    {
        var service = Substitute.For<IMoodService>();
        service.Current.Returns(mood);
        return service;
    }

    [Fact]
    public void SleepyMood_PlansSleepNearClock()
    {
        var world = World(new TaskbarElement("Clock", TaskbarElementKind.Clock, 900, 1000, false));
        var planner = new BehaviorPlanner(Random(0.9), Mood(PetMood.Sleepy), world, Catalog(), Prefs());

        var plan = planner.Plan();

        plan.Type.Should().Be(PetBehaviorType.Sleeping);
        plan.State.Should().Be(PetState.Sleeping);
        plan.TargetX.Should().BeGreaterThan(700);
    }

    [Fact]
    public void FocusedMood_NeverRoamsOrSleeps()
    {
        var world = World(new TaskbarElement("VS", TaskbarElementKind.App, 300, 348, true));
        foreach (var value in new[] { 0.0, 0.2, 0.5, 0.8, 0.99 })
        {
            var plan = new BehaviorPlanner(Random(value), Mood(PetMood.Focused), world, Catalog(), Prefs()).Plan();
            plan.Type.Should().BeOneOf(PetBehaviorType.Sitting, PetBehaviorType.Observing, PetBehaviorType.Roaming);
            plan.Type.Should().NotBe(PetBehaviorType.Sleeping);
        }
    }

    [Fact]
    public void PlannedTargets_NeverOverlapTaskbarButtons()
    {
        var elements = new[]
        {
            new TaskbarElement("Start", TaskbarElementKind.Start, 0, 48, false),
            new TaskbarElement("VS", TaskbarElementKind.App, 300, 348, true),
            new TaskbarElement("Tray", TaskbarElementKind.Tray, 850, 1000, false)
        };
        var world = World(elements);

        for (var i = 0; i < 200; i++)
        {
            var random = new Random(i);
            var rnd = Substitute.For<IRandomSource>();
            rnd.NextDouble().Returns(_ => random.NextDouble());
            rnd.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(c => random.Next(c.ArgAt<int>(0), c.ArgAt<int>(1)));

            var plan = new BehaviorPlanner(rnd, Mood(PetMood.Relaxed), world, Catalog(), Prefs()).Plan();
            if (plan.TargetX is not { } x)
            {
                continue;
            }

            elements.Any(e => x < e.Right && x + 64 > e.Left).Should().BeFalse($"seed {i} target {x}");
        }
    }

    [Fact]
    public void NoFreeZones_FallsBackToSitting()
    {
        var world = World(new TaskbarElement("All", TaskbarElementKind.App, 0, 1000, false));
        var plan = new BehaviorPlanner(Random(0.1), Mood(PetMood.Curious), world, Catalog(), Prefs()).Plan();
        plan.Type.Should().Be(PetBehaviorType.Sitting);
    }

    [Fact]
    public void StateMachine_WalksToTarget_ThenPerformsActivity()
    {
        var world = World();
        var planner = Substitute.For<IBehaviorPlanner>();
        planner.Plan().Returns(new BehaviorPlan(PetBehaviorType.Roaming, PetState.Idle, PetActivity.Sit, 5, 200, 1.0, true));
        var sut = new PetStateMachine(planner, Mood(PetMood.Relaxed), world, Prefs(), NullLogger<PetStateMachine>.Instance);

        sut.Tick(TimeSpan.FromSeconds(0.1));
        sut.State.Should().Be(PetState.Walking);
        sut.Activity.Should().Be(PetActivity.WalkRight);

        for (var i = 0; i < 40; i++)
        {
            sut.Tick(TimeSpan.FromSeconds(0.1));
        }

        world.PetX.Should().Be(200);
        sut.Activity.Should().Be(PetActivity.Sit);
    }

    [Fact]
    public void Celebrate_OverridesThenReturnsToPlanning()
    {
        var world = World();
        var planner = Substitute.For<IBehaviorPlanner>();
        planner.Plan().Returns(new BehaviorPlan(PetBehaviorType.Sitting, PetState.Idle, PetActivity.Sit, 30));
        var sut = new PetStateMachine(planner, Mood(PetMood.Relaxed), world, Prefs(), NullLogger<PetStateMachine>.Instance);

        sut.Trigger(PetTrigger.Celebrate);
        sut.State.Should().Be(PetState.Happy);

        sut.Tick(TimeSpan.FromSeconds(4));
        sut.Tick(TimeSpan.FromSeconds(0.1));
        sut.State.Should().Be(PetState.Idle);
    }

    [Fact]
    public void Walk_AcceleratesFromRest_SoFirstStepIsSmallerThanCruiseStep()
    {
        var world = World();
        var planner = Substitute.For<IBehaviorPlanner>();
        planner.Plan().Returns(new BehaviorPlan(PetBehaviorType.Roaming, PetState.Idle, PetActivity.Sit, 5, 600, 1.0, true));
        var sut = new PetStateMachine(planner, Mood(PetMood.Relaxed), world, Prefs(), NullLogger<PetStateMachine>.Instance);

        sut.Tick(TimeSpan.FromSeconds(0.05));
        sut.Tick(TimeSpan.FromSeconds(0.05));
        var firstStep = world.PetX - 100;
        for (var i = 0; i < 20; i++)
        {
            sut.Tick(TimeSpan.FromSeconds(0.05));
        }

        var before = world.PetX;
        sut.Tick(TimeSpan.FromSeconds(0.05));
        (world.PetX - before).Should().BeGreaterThan(firstStep);
    }

    [Fact]
    public void Walk_DeceleratesNearTarget_SoFinalStepIsSmallerThanCruiseStep()
    {
        var world = World();
        var planner = Substitute.For<IBehaviorPlanner>();
        planner.Plan().Returns(new BehaviorPlan(PetBehaviorType.Roaming, PetState.Idle, PetActivity.Sit, 5, 300, 1.0, true));
        var sut = new PetStateMachine(planner, Mood(PetMood.Relaxed), world, Prefs(), NullLogger<PetStateMachine>.Instance);

        var maxStep = 0.0;
        var lastStep = 0.0;
        for (var i = 0; i < 400 && world.PetX < 300; i++)
        {
            var before = world.PetX;
            sut.Tick(TimeSpan.FromSeconds(0.05));
            lastStep = world.PetX - before;
            maxStep = Math.Max(maxStep, lastStep);
        }

        world.PetX.Should().Be(300);
        lastStep.Should().BeLessThan(maxStep * 0.5);
    }

    [Fact]
    public void NextFidget_NeverRepeatsTheSameAnimationTwiceInARow()
    {
        var planner = new BehaviorPlanner(Random(0.0), Mood(PetMood.Relaxed), World(), Catalog(), Prefs());

        var first = planner.NextFidget();
        var second = planner.NextFidget();

        second!.Activity.Should().NotBe(first!.Activity);
    }

    [Fact]
    public void NextFidget_SkipsAnimationsMissingFromTheSkin()
    {
        var catalog = Substitute.For<IAnimationCatalog>();
        catalog.Has(Arg.Any<PetActivity>()).Returns(false);
        catalog.Has(PetActivity.Blink).Returns(true);
        catalog.WithTag(Arg.Any<string>()).Returns(new List<PetActivity>());
        var planner = new BehaviorPlanner(Random(0.5), Mood(PetMood.Relaxed), World(), catalog, Prefs());

        planner.NextFidget()!.Activity.Should().Be(PetActivity.Blink);
    }
}
