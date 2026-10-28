namespace FocusCatAI.Domain.Pomodoro;

public enum PomodoroPhase
{
    None,
    Work,
    Break
}

public enum PomodoroRunState
{
    Stopped,
    Running,
    Paused
}

public enum PomodoroEventKind
{
    WorkStarted,
    BreakStarted,
    WorkCompleted,
    BreakCompleted,
    Paused,
    Resumed,
    Stopped,
    Milestone
}

public sealed record PomodoroEvent(PomodoroEventKind Kind, int Minutes = 0);

public sealed record PomodoroSettings(TimeSpan WorkDuration, TimeSpan BreakDuration)
{
    public static PomodoroSettings Default { get; } = new(TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(10));
}
