using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pomodoro;
using FocusCatAI.Domain.Pomodoro;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FocusCatAI.Application.Tests;

public class PomodoroServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2025, 1, 1, 9, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan t) => UtcNow += t;
    }

    private static (PomodoroService Sut, FakeClock Clock, List<PomodoroEvent> Events) Create()
    {
        var clock = new FakeClock();
        var sut = new PomodoroService(clock, PomodoroSettings.Default, NullLogger<PomodoroService>.Instance);
        var events = new List<PomodoroEvent>();
        sut.EventRaised += (_, e) => events.Add(e);
        return (sut, clock, events);
    }

    [Fact]
    public void Start_BeginsWorkPhase()
    {
        var (sut, _, events) = Create();
        sut.Start();
        sut.Phase.Should().Be(PomodoroPhase.Work);
        sut.RunState.Should().Be(PomodoroRunState.Running);
        sut.Remaining.Should().Be(TimeSpan.FromMinutes(45));
        events.Should().ContainSingle(e => e.Kind == PomodoroEventKind.WorkStarted);
    }

    [Fact]
    public void Pause_FreezesRemaining_AndResumeContinues()
    {
        var (sut, clock, _) = Create();
        sut.Start();
        clock.Advance(TimeSpan.FromMinutes(5));
        sut.Pause();
        clock.Advance(TimeSpan.FromMinutes(20));
        sut.Remaining.Should().Be(TimeSpan.FromMinutes(40));

        sut.Resume();
        clock.Advance(TimeSpan.FromMinutes(10));
        sut.Remaining.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Milestones_FireOnceAt15And30()
    {
        var (sut, clock, events) = Create();
        sut.Start();
        foreach (var _ in Enumerable.Range(0, 40))
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            sut.Tick(TimeSpan.FromSeconds(1));
        }

        events.Where(e => e.Kind == PomodoroEventKind.Milestone).Select(e => e.Minutes)
            .Should().Equal(15, 30);
    }

    [Fact]
    public void WorkCompletion_StartsBreak_ThenStops()
    {
        var (sut, clock, events) = Create();
        sut.Start();
        clock.Advance(TimeSpan.FromMinutes(46));
        sut.Tick(TimeSpan.FromSeconds(1));
        sut.Phase.Should().Be(PomodoroPhase.Break);
        events.Select(e => e.Kind).Should().Contain([PomodoroEventKind.WorkCompleted, PomodoroEventKind.BreakStarted]);

        clock.Advance(TimeSpan.FromMinutes(11));
        sut.Tick(TimeSpan.FromSeconds(1));
        sut.RunState.Should().Be(PomodoroRunState.Stopped);
        events.Select(e => e.Kind).Should().Contain(PomodoroEventKind.BreakCompleted);
    }

    [Fact]
    public void Stop_ResetsState()
    {
        var (sut, _, _) = Create();
        sut.Start();
        sut.Stop();
        sut.Phase.Should().Be(PomodoroPhase.None);
        sut.Remaining.Should().Be(TimeSpan.Zero);
    }
}
