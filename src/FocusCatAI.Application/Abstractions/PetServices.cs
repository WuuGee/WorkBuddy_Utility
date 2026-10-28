using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Pomodoro;

namespace FocusCatAI.Application.Abstractions;

public interface IMoodService
{
    PetMood Current { get; }
    event EventHandler<PetMood>? Changed;

    /// <param name="lifetime">How long the mood lasts before returning to Relaxed. Null = until changed.</param>
    void Set(PetMood mood, TimeSpan? lifetime = null);
}

public interface IBehaviorPlanner
{
    BehaviorPlan Plan();

    /// <summary>Next idle variation to play while dwelling (never the same one twice in a row), or null for none.</summary>
    IdleFidget? NextFidget();
}

public enum SpeechPriority
{
    /// <summary>Rarely shown; subject to a long cooldown.</summary>
    Normal,

    /// <summary>Direct response to a user action or milestone.</summary>
    High
}

public interface IPetSpeaker
{
    void Say(string text, SpeechPriority priority = SpeechPriority.Normal);
}

public interface IPomodoroService
{
    PomodoroPhase Phase { get; }
    PomodoroRunState RunState { get; }
    TimeSpan Remaining { get; }
    event EventHandler<PomodoroEvent>? EventRaised;

    void Start();
    void Pause();
    void Resume();
    void Stop();
}
