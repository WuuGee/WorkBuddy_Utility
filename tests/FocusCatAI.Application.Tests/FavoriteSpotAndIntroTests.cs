using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Taskbar;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FocusCatAI.Application.Tests;

public class FavoriteSpotAndIntroTests
{
    private static readonly TaskbarElement Start = new("Start", TaskbarElementKind.Start, 0, 48, false);
    private static readonly TaskbarElement Vs = new("Microsoft Visual Studio - 1 running window", TaskbarElementKind.App, 400, 448, true);
    private static readonly TaskbarElement Tray = new("Notification area", TaskbarElementKind.Tray, 900, 1000, false);

    private static PetWorld World() => new()
    {
        PetWidth = 44,
        PetHeight = 44,
        PetX = 600,
        WalkSpeed = 40,
        Layout = new TaskbarLayout(TaskbarPlacementMode.InTaskbar, 0, 1000, 1000, 1048, [Start, Vs, Tray])
    };

    private static IAnimationCatalog Catalog()
    {
        var catalog = Substitute.For<IAnimationCatalog>();
        catalog.Has(Arg.Any<PetActivity>()).Returns(true);
        catalog.WithTag(Arg.Any<string>()).Returns(new List<PetActivity>());
        return catalog;
    }

    private static IPetPreferences Prefs(params FavoriteSpot[] favorites)
    {
        var prefs = Substitute.For<IPetPreferences>();
        prefs.Favorites.Returns(favorites);
        return prefs;
    }

    private static IMoodService Mood(PetMood mood = PetMood.Relaxed)
    {
        var service = Substitute.For<IMoodService>();
        service.Current.Returns(mood);
        return service;
    }

    private static IRandomSource Random(params double[] values)
    {
        var random = Substitute.For<IRandomSource>();
        var i = 0;
        random.NextDouble().Returns(_ => values[Math.Min(i++, values.Length - 1)]);
        random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        return random;
    }

    [Fact]
    public void Roaming_CanSelectAFavoriteLocation()
    {
        // 0.3 picks Roaming for a Relaxed cat when it is not a repeat; 0.1 then picks the favorite branch.
        var planner = new BehaviorPlanner(Random(0.1, 0.1, 0.0, 0.5), Mood(), World(), Catalog(), Prefs(new FavoriteSpot("Start", 1)));

        var plan = planner.Plan();

        plan.FavoriteKey.Should().Be("Start");
    }

    [Fact]
    public void Sitting_CanWalkToAFavoriteBeforeSitting()
    {
        var rng = new System.Random(11);
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(_ => rng.NextDouble());
        random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(c => rng.Next(c.ArgAt<int>(0), c.ArgAt<int>(1)));
        var planner = new BehaviorPlanner(random, Mood(), World(), Catalog(), Prefs(new FavoriteSpot("App:Visual Studio", 1)));

        var plans = Enumerable.Range(0, 300).Select(_ => planner.Plan()).ToList();

        plans.Should().Contain(p => p.Type == PetBehaviorType.Sitting && p.FavoriteKey == "App:Visual Studio" && p.TargetX != null);
    }

    [Fact]
    public void FavoriteWeight_AffectsSelectionProbability()
    {
        var prefs = Prefs(new FavoriteSpot("Start", 5), new FavoriteSpot("App:Visual Studio", 0.5));
        var counts = new Dictionary<string, int> { ["Start"] = 0, ["App:Visual Studio"] = 0 };
        var rng = new System.Random(7);
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(_ => rng.NextDouble());
        random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(c => rng.Next(c.ArgAt<int>(0), c.ArgAt<int>(1)));
        var planner = new BehaviorPlanner(random, Mood(), World(), Catalog(), prefs);

        for (var i = 0; i < 600; i++)
        {
            if (planner.Plan().FavoriteKey is { } key)
            {
                counts[key]++;
            }
        }

        counts["Start"].Should().BeGreaterThan(counts["App:Visual Studio"] * 3);
    }

    [Fact]
    public void Intro_OnFirstLaunch_WalksInFromOffScreenAndSitsNearStart()
    {
        var world = World();
        var prefs = Prefs();
        prefs.HasIntroduced.Returns(false);
        var machine = Substitute.For<IPetStateMachine>();
        var sut = new FirstLaunchIntro(prefs, world, machine, Substitute.For<IPetSpeaker>(), Random(0.5), NullLogger<FirstLaunchIntro>.Instance);

        sut.Tick(TimeSpan.FromSeconds(0.1));

        world.PetX.Should().Be(-44);
        machine.Received(1).Play(Arg.Is<BehaviorPlan>(p => p.TargetX >= 48 && p.Activity == PetActivity.Sit));
    }

    [Theory]
    [InlineData(0.0, 30)]
    [InlineData(0.99, 60)]
    public void Intro_GreetsOnlyAfterThirtyToSixtySeconds(double roll, int seconds)
    {
        var speaker = Substitute.For<IPetSpeaker>();
        var prefs = Prefs();
        prefs.HasIntroduced.Returns(false);
        var machine = Substitute.For<IPetStateMachine>();
        machine.State.Returns(PetState.Idle);
        var sut = new FirstLaunchIntro(prefs, World(), machine, speaker, Random(roll), NullLogger<FirstLaunchIntro>.Instance);

        sut.Tick(TimeSpan.FromSeconds(0.1));
        sut.Tick(TimeSpan.FromSeconds(0.1));
        sut.Tick(TimeSpan.FromSeconds(seconds - 2));
        speaker.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<SpeechPriority>());

        sut.Tick(TimeSpan.FromSeconds(3));
        speaker.Received(1).Say(FirstLaunchIntro.Greeting, SpeechPriority.High);
    }

    [Fact]
    public void Intro_MarksIntroducedAndSaves_AfterGreeting()
    {
        var prefs = Prefs();
        prefs.HasIntroduced.Returns(false);
        var machine = Substitute.For<IPetStateMachine>();
        machine.State.Returns(PetState.Idle);
        var sut = new FirstLaunchIntro(prefs, World(), machine, Substitute.For<IPetSpeaker>(), Random(0.0), NullLogger<FirstLaunchIntro>.Instance);

        sut.Tick(TimeSpan.FromSeconds(0.1));
        sut.Tick(TimeSpan.FromSeconds(0.1));
        sut.Tick(TimeSpan.FromSeconds(31));

        prefs.Received(1).MarkIntroduced();
        prefs.Received(1).Save();
    }

    [Fact]
    public void Intro_NeverRunsOnceAlreadyIntroduced()
    {
        var speaker = Substitute.For<IPetSpeaker>();
        var prefs = Prefs();
        prefs.HasIntroduced.Returns(true);
        var machine = Substitute.For<IPetStateMachine>();
        var sut = new FirstLaunchIntro(prefs, World(), machine, speaker, Random(0.0), NullLogger<FirstLaunchIntro>.Instance);

        for (var i = 0; i < 5; i++)
        {
            sut.Tick(TimeSpan.FromSeconds(100));
        }

        machine.DidNotReceive().Play(Arg.Any<BehaviorPlan>());
        speaker.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<SpeechPriority>());
    }
}
