using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pomodoro;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Application.Pomodoro;

/// <summary>Timestamp-based timer, so it stays correct across timer drift and system sleep.</summary>
public sealed class PomodoroService(IClock clock, PomodoroSettings settings, ILogger<PomodoroService> logger)
    : IPomodoroService, ITickable
{
    private static readonly int[] Milestones = [15, 30];

    private DateTimeOffset _runningSince;
    private TimeSpan _elapsedBefore;
    private int _lastMilestone;

    public PomodoroPhase Phase { get; private set; }
    public PomodoroRunState RunState { get; private set; }
    public event EventHandler<PomodoroEvent>? EventRaised;

    public TimeSpan Remaining => Phase == PomodoroPhase.None
        ? TimeSpan.Zero
        : TimeSpan.FromTicks(Math.Max(0, (Duration - Elapsed).Ticks));

    private TimeSpan Duration => Phase == PomodoroPhase.Work ? settings.WorkDuration : settings.BreakDuration;

    private TimeSpan Elapsed => RunState == PomodoroRunState.Running
        ? _elapsedBefore + (clock.UtcNow - _runningSince)
        : _elapsedBefore;

    public void Start()
    {
        if (RunState == PomodoroRunState.Stopped)
        {
            BeginPhase(PomodoroPhase.Work);
        }
    }

    public void Pause()
    {
        if (RunState != PomodoroRunState.Running)
        {
            return;
        }

        _elapsedBefore = Elapsed;
        RunState = PomodoroRunState.Paused;
        Raise(PomodoroEventKind.Paused);
    }

    public void Resume()
    {
        if (RunState != PomodoroRunState.Paused)
        {
            return;
        }

        _runningSince = clock.UtcNow;
        RunState = PomodoroRunState.Running;
        Raise(PomodoroEventKind.Resumed);
    }

    public void Stop()
    {
        if (RunState == PomodoroRunState.Stopped)
        {
            return;
        }

        Phase = PomodoroPhase.None;
        RunState = PomodoroRunState.Stopped;
        _elapsedBefore = TimeSpan.Zero;
        Raise(PomodoroEventKind.Stopped);
    }

    public void Tick(TimeSpan elapsed)
    {
        if (RunState != PomodoroRunState.Running)
        {
            return;
        }

        var current = Elapsed;
        if (current >= Duration)
        {
            CompletePhase();
            return;
        }

        if (Phase != PomodoroPhase.Work)
        {
            return;
        }

        foreach (var minutes in Milestones)
        {
            if (minutes > _lastMilestone && current >= TimeSpan.FromMinutes(minutes) && TimeSpan.FromMinutes(minutes) < Duration)
            {
                _lastMilestone = minutes;
                Raise(PomodoroEventKind.Milestone, minutes);
            }
        }
    }

    private void CompletePhase()
    {
        if (Phase == PomodoroPhase.Work)
        {
            Raise(PomodoroEventKind.WorkCompleted);
            BeginPhase(PomodoroPhase.Break);
        }
        else
        {
            Phase = PomodoroPhase.None;
            RunState = PomodoroRunState.Stopped;
            _elapsedBefore = TimeSpan.Zero;
            Raise(PomodoroEventKind.BreakCompleted);
        }
    }

    private void BeginPhase(PomodoroPhase phase)
    {
        Phase = phase;
        RunState = PomodoroRunState.Running;
        _elapsedBefore = TimeSpan.Zero;
        _runningSince = clock.UtcNow;
        _lastMilestone = 0;
        Raise(phase == PomodoroPhase.Work ? PomodoroEventKind.WorkStarted : PomodoroEventKind.BreakStarted);
    }

    private void Raise(PomodoroEventKind kind, int minutes = 0)
    {
        logger.LogInformation("Pomodoro {Kind} {Minutes}", kind, minutes);
        EventRaised?.Invoke(this, new PomodoroEvent(kind, minutes));
    }
}
