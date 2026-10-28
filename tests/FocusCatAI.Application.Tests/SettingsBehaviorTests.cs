using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.Pets;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FocusCatAI.Application.Tests;

public class SettingsBehaviorTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2025, 1, 1, 9, 0, 0, TimeSpan.Zero);
    }

    private static ISettingsService SettingsWith(AppSettings value)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(value);
        return settings;
    }

    private static (PetSpeaker Sut, ISpeechPresenter Presenter, FakeClock Clock) Speaker(AppSettings value)
    {
        var presenter = Substitute.For<ISpeechPresenter>();
        var clock = new FakeClock();
        var sut = new PetSpeaker(presenter, Substitute.For<IPetStateMachine>(), clock, NullLogger<PetSpeaker>.Instance, SettingsWith(value));
        clock.UtcNow += TimeSpan.FromMinutes(3);
        return (sut, presenter, clock);
    }

    [Fact]
    public void MessagesDisabled_SuppressesEvenHighPriority()
    {
        var (sut, presenter, _) = Speaker(new AppSettings { Behavior = new BehaviorSettings { EnableMessages = false } });

        sut.Say("Hi", SpeechPriority.High);

        presenter.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public void QuietHours_SuppressMessages()
    {
        // Quiet all day: start 00:00, end 23:59 covers any test-machine local time except one minute.
        var quiet = new QuietHoursSettings { Enabled = true, Start = new TimeOnly(0, 0), End = new TimeOnly(23, 59) };
        var (sut, presenter, clock) = Speaker(new AppSettings { QuietHours = quiet });
        clock.UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, DateTimeOffset.Now.Offset);

        sut.Say("Hi", SpeechPriority.High);

        presenter.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Theory]
    [InlineData(MessageFrequency.Low, 29, 0)]
    [InlineData(MessageFrequency.Low, 31, 1)]
    [InlineData(MessageFrequency.Normal, 14, 0)]
    [InlineData(MessageFrequency.Normal, 16, 1)]
    [InlineData(MessageFrequency.High, 4, 0)]
    [InlineData(MessageFrequency.High, 6, 1)]
    public void MessageFrequency_ChangesTheCooldown(MessageFrequency frequency, int minutesLater, int expectedExtraMessages)
    {
        var (sut, presenter, clock) = Speaker(new AppSettings { Behavior = new BehaviorSettings { MessageFrequency = frequency } });
        sut.Say("first");

        clock.UtcNow += TimeSpan.FromMinutes(minutesLater);
        sut.Say("second");

        presenter.Received(1 + expectedExtraMessages).Show(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public void SleepDisabled_NeverBecomesSleepy()
    {
        var idle = Substitute.For<IUserIdleProvider>();
        idle.IdleTime.Returns(TimeSpan.FromHours(1));
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(0.5);
        var sut = new MoodService(idle, random, NullLogger<MoodService>.Instance) { SleepEnabled = false };

        sut.Tick(TimeSpan.FromSeconds(1));

        sut.Current.Should().Be(PetMood.Relaxed);
    }

    [Fact]
    public void SleepDisabledWhileAsleep_WakesThePet()
    {
        var idle = Substitute.For<IUserIdleProvider>();
        idle.IdleTime.Returns(TimeSpan.FromHours(1));
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(0.5);
        var sut = new MoodService(idle, random, NullLogger<MoodService>.Instance);
        sut.Tick(TimeSpan.FromSeconds(1));
        sut.Current.Should().Be(PetMood.Sleepy);

        sut.SleepEnabled = false;
        sut.Tick(TimeSpan.FromSeconds(1));

        sut.Current.Should().Be(PetMood.Relaxed);
    }

    [Fact]
    public void ShorterSleepDelay_SleepsSooner()
    {
        var idle = Substitute.For<IUserIdleProvider>();
        idle.IdleTime.Returns(TimeSpan.FromSeconds(40));
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(0.5);
        var sut = new MoodService(idle, random, NullLogger<MoodService>.Instance) { SleepyAfter = TimeSpan.FromSeconds(30) };

        sut.Tick(TimeSpan.FromSeconds(1));

        sut.Current.Should().Be(PetMood.Sleepy);
    }

    [Fact]
    public void ProductivityMessagesDisabled_SaysNothing()
    {
        var foreground = Substitute.For<IForegroundAppProvider>();
        foreground.GetForeground().Returns(new ForegroundApp("devenv", null));
        var speaker = Substitute.For<IPetSpeaker>();
        var settings = SettingsWith(new AppSettings { Notifications = new NotificationSettings { EnableProductivityMessages = false } });
        var sut = new ProductivityReactions(foreground, speaker, Substitute.For<IMoodService>(), new FakeClock(), new MonitoringOptions(), settings);

        sut.Tick(TimeSpan.FromSeconds(3));

        speaker.DidNotReceiveWithAnyArgs().Say(default!, default);
    }
}
