using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Domain.Pets;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FocusCatAI.Application.Tests;

public class MoodAndSpeechTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2025, 1, 1, 9, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void LongIdle_MakesPetSleepy_AndInputWakesIt()
    {
        var idle = Substitute.For<IUserIdleProvider>();
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(0.5);
        var sut = new MoodService(idle, random, NullLogger<MoodService>.Instance);

        idle.IdleTime.Returns(TimeSpan.FromMinutes(6));
        sut.Tick(TimeSpan.FromSeconds(1));
        sut.Current.Should().Be(PetMood.Sleepy);

        idle.IdleTime.Returns(TimeSpan.FromSeconds(1));
        sut.Tick(TimeSpan.FromSeconds(1));
        sut.Current.Should().Be(PetMood.Relaxed);
    }

    [Fact]
    public void TimedMood_ExpiresBackToRelaxed()
    {
        var idle = Substitute.For<IUserIdleProvider>();
        idle.IdleTime.Returns(TimeSpan.Zero);
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(0.5);
        var sut = new MoodService(idle, random, NullLogger<MoodService>.Instance);

        sut.Set(PetMood.Happy, TimeSpan.FromSeconds(10));
        sut.Tick(TimeSpan.FromSeconds(11));
        sut.Current.Should().Be(PetMood.Relaxed);
    }

    [Fact]
    public void FocusedMood_IsNotInterruptedByIdle()
    {
        var idle = Substitute.For<IUserIdleProvider>();
        idle.IdleTime.Returns(TimeSpan.FromMinutes(30));
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(0.5);
        var sut = new MoodService(idle, random, NullLogger<MoodService>.Instance);

        sut.Set(PetMood.Focused);
        sut.Tick(TimeSpan.FromSeconds(1));
        sut.Current.Should().Be(PetMood.Focused);
    }

    [Fact]
    public void Speaker_SuppressesNormalMessagesDuringCooldown_ButAllowsMilestones()
    {
        var presenter = Substitute.For<ISpeechPresenter>();
        var pet = Substitute.For<IPetStateMachine>();
        var clock = new FakeClock();
        var sut = new PetSpeaker(presenter, pet, clock, NullLogger<PetSpeaker>.Instance);

        clock.UtcNow += TimeSpan.FromMinutes(3);
        sut.Say("Working on code?");
        clock.UtcNow += TimeSpan.FromSeconds(30);
        sut.Say("Meeting time?");
        presenter.Received(1).Show(Arg.Any<string>(), Arg.Any<TimeSpan>());

        clock.UtcNow += TimeSpan.FromSeconds(10);
        sut.Say("Nice start.", SpeechPriority.High);
        presenter.Received(2).Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    private static (PetSpeaker Sut, ISpeechPresenter Presenter, FakeClock Clock) SpeakerAfterStartup()
    {
        var presenter = Substitute.For<ISpeechPresenter>();
        var clock = new FakeClock();
        var sut = new PetSpeaker(presenter, Substitute.For<IPetStateMachine>(), clock, NullLogger<PetSpeaker>.Instance);
        clock.UtcNow += TimeSpan.FromMinutes(3);
        sut.Say("first");
        return (sut, presenter, clock);
    }

    [Fact]
    public void Speaker_DoesNotSpeakNormalMessagesDuringStartupQuietPeriod()
    {
        var presenter = Substitute.For<ISpeechPresenter>();
        var clock = new FakeClock();
        var sut = new PetSpeaker(presenter, Substitute.For<IPetStateMachine>(), clock, NullLogger<PetSpeaker>.Instance);

        clock.UtcNow += TimeSpan.FromSeconds(30);
        sut.Say("hello");

        presenter.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public void Speaker_BeforeFifteenMinutes_SuppressesNormalMessage()
    {
        var (sut, presenter, clock) = SpeakerAfterStartup();

        clock.UtcNow += TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59);
        sut.Say("second");

        presenter.Received(1).Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public void Speaker_AtExactlyFifteenMinutes_AllowsNormalMessage()
    {
        var (sut, presenter, clock) = SpeakerAfterStartup();

        clock.UtcNow += TimeSpan.FromMinutes(15);
        sut.Say("second");

        presenter.Received(2).Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public void Speaker_AfterFifteenMinutes_AllowsNormalMessage()
    {
        var (sut, presenter, clock) = SpeakerAfterStartup();

        clock.UtcNow += TimeSpan.FromMinutes(16);
        sut.Say("second");

        presenter.Received(2).Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public void Speaker_HighPriority_SpeaksEvenDuringStartupQuietPeriod()
    {
        var presenter = Substitute.For<ISpeechPresenter>();
        var sut = new PetSpeaker(presenter, Substitute.For<IPetStateMachine>(), new FakeClock(), NullLogger<PetSpeaker>.Instance);

        sut.Say("Hi!", SpeechPriority.High);

        presenter.Received(1).Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public void Productivity_AppSwitchSpam_TriggersDistractionMessageOnce()
    {
        var foreground = Substitute.For<IForegroundAppProvider>();
        var speaker = Substitute.For<IPetSpeaker>();
        var mood = Substitute.For<IMoodService>();
        var clock = new FakeClock();
        var sut = new ProductivityReactions(foreground, speaker, mood, clock, new MonitoringOptions());

        for (var i = 0; i < 20; i++)
        {
            foreground.GetForeground().Returns(new ForegroundApp($"app{i}", null));
            sut.Tick(TimeSpan.FromSeconds(3));
            clock.UtcNow += TimeSpan.FromSeconds(3);
        }

        speaker.Received(1).Say("Let's finish one thing first.");
    }
}
