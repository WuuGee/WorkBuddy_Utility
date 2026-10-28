using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Application.Abstractions;

public sealed class PetStateChangedEventArgs(PetState state, PetActivity activity, bool facingRight) : EventArgs
{
    public PetState State { get; } = state;
    public PetActivity Activity { get; } = activity;
    public bool FacingRight { get; } = facingRight;
}

/// <summary>Executes behavior plans (walk to a spot, then dwell) and moves the pet through <see cref="IPetWorld"/>.
/// Not thread-safe; drive it from the UI thread.</summary>
public interface IPetStateMachine : ITickable
{
    PetState State { get; }
    PetActivity Activity { get; }
    bool FacingRight { get; }
    PetBehaviorType Behavior { get; }
    event EventHandler<PetStateChangedEventArgs>? StateChanged;

    void Trigger(PetTrigger trigger);

    /// <summary>Runs this plan next instead of asking the planner (used for scripted moments such as the first-launch entrance).</summary>
    void Play(BehaviorPlan plan);
}
