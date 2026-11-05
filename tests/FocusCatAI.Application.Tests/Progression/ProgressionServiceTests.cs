using FluentAssertions;
using FocusCatAI.Application.Progression;
using FocusCatAI.Domain.Progression;
using NSubstitute;
using Xunit;

namespace FocusCatAI.Application.Tests.Progression;

public class ProgressionServiceTests
{
    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (ProgressionService Service, IProgressionStore Store, FakeTime Time) Create(ProgressionState? initial = null)
    {
        var store = Substitute.For<IProgressionStore>();
        store.Load().Returns(initial ?? new ProgressionState());
        var time = new FakeTime();
        return (new ProgressionService(store, time), store, time);
    }

    [Fact]
    public void LoadsStateFromStore()
    {
        var (service, _, _) = Create(new ProgressionState { TotalXp = 100 });
        service.Progress.Level.Should().Be(2);
    }

    [Fact]
    public void TickDoesNotSaveImmediately()
    {
        var (service, store, _) = Create();
        service.RecordCompanionshipTick();
        store.DidNotReceive().Save(Arg.Any<ProgressionState>());
    }

    [Fact]
    public void TickSavesOnceSaveIntervalHasPassed()
    {
        var (service, store, time) = Create();
        time.Now += ProgressionService.SaveInterval;
        service.RecordCompanionshipTick();
        store.Received(1).Save(Arg.Is<ProgressionState>(s => s.TotalXp == 1));
    }

    [Fact]
    public void LevelUpSavesImmediately()
    {
        var (service, store, _) = Create(new ProgressionState { TotalXp = 49 });
        service.RecordCompanionshipTick();
        store.Received(1).Save(Arg.Is<ProgressionState>(s => s.TotalXp == 50));
    }

    [Fact]
    public void LevelUpRaisesEventWithNewLevel()
    {
        var (service, _, _) = Create(new ProgressionState { TotalXp = 49 });
        var levels = new List<int>();
        service.LeveledUp += (_, l) => levels.Add(l);
        service.RecordCompanionshipTick();
        levels.Should().Equal(2);
    }

    [Fact]
    public void ChangedIsRaisedOnEveryGrant()
    {
        var (service, _, _) = Create();
        var count = 0;
        service.Changed += (_, _) => count++;
        service.RecordCompanionshipTick();
        service.RecordCompanionshipTick();
        count.Should().Be(2);
    }

    [Fact]
    public void DeniedInteractionRaisesNothing()
    {
        var (service, _, _) = Create();
        service.RecordInteraction();
        var count = 0;
        service.Changed += (_, _) => count++;
        service.RecordInteraction();
        count.Should().Be(0);
    }

    [Fact]
    public void FlushSavesDirtyState()
    {
        var (service, store, _) = Create();
        service.RecordCompanionshipTick();
        service.Flush();
        store.Received(1).Save(Arg.Is<ProgressionState>(s => s.TotalXp == 1));
    }

    [Fact]
    public void FlushWithoutChangesDoesNotSave()
    {
        var (service, store, _) = Create();
        service.Flush();
        store.DidNotReceive().Save(Arg.Any<ProgressionState>());
    }

    [Fact]
    public void DisposeFlushesUnsavedTicks()
    {
        var (service, store, _) = Create();
        service.RecordCompanionshipTick();
        service.Dispose();
        store.Received(1).Save(Arg.Is<ProgressionState>(s => s.TotalXp == 1));
    }

    [Fact]
    public void GrantsAfterDisposeAreIgnored()
    {
        var (service, _, _) = Create();
        service.Dispose();
        service.RecordCompanionshipTick();
        service.Current.TotalXp.Should().Be(0);
    }
}
