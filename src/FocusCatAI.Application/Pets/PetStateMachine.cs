using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Taskbar;
using FocusCatAI.Domain.Pets;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Application.Pets;

public sealed class PetStateMachine : IPetStateMachine
{
    private readonly IBehaviorPlanner _planner;
    private readonly IPetWorld _world;
    private readonly IPetPreferences _preferences;
    private readonly ILogger<PetStateMachine> _logger;

    private BehaviorPlan? _plan;
    private bool _walking;
    private double _remaining;
    private double _speed;

    private bool _fidgeting;
    private double _fidgetCountdown;
    private double _fidgetRemaining;
    private double _nextFidgetDelay = 4;

    private bool _overriding;
    private double _overrideRemaining;

    public PetStateMachine(
        IBehaviorPlanner planner,
        IMoodService mood,
        IPetWorld world,
        IPetPreferences preferences,
        ILogger<PetStateMachine> logger)
    {
        _planner = planner;
        _world = world;
        _preferences = preferences;
        _logger = logger;

        State = PetState.Idle;
        Activity = PetActivity.Idle;
        FacingRight = true;
        mood.Changed += (_, _) => Replan();
    }

    public PetState State { get; private set; }
    public PetActivity Activity { get; private set; }
    public bool FacingRight { get; private set; }
    public PetBehaviorType Behavior { get; private set; } = PetBehaviorType.Sitting;
    public event EventHandler<PetStateChangedEventArgs>? StateChanged;

    public void Trigger(PetTrigger trigger)
    {
        switch (trigger)
        {
            case PetTrigger.ChatStarted:
                Override(PetState.Thinking, PetActivity.Think, double.PositiveInfinity);
                break;
            case PetTrigger.AnswerReady:
                Override(PetState.Talking, PetActivity.Speak, double.PositiveInfinity);
                break;
            case PetTrigger.SpeechFinished:
                EndOverride();
                break;
            case PetTrigger.Speak:
                Override(PetState.Talking, PetActivity.Speak, 3);
                break;
            case PetTrigger.SitDown:
                Override(PetState.Idle, PetActivity.Sit, 4);
                break;
            case PetTrigger.Celebrate:
                Override(PetState.Happy, PetActivity.Happy, 3);
                break;
            case PetTrigger.UserInteracted:
                Override(PetState.Idle, PetActivity.Idle, 2);
                break;
        }
    }

    public void Play(BehaviorPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _overriding = false;
        _walking = false;
        _fidgeting = false;
        _speed = 0;
        StartPlan(plan);
    }

    public void Tick(TimeSpan elapsed)
    {
        var dt = elapsed.TotalSeconds;

        if (_overriding)
        {
            _overrideRemaining -= dt;
            if (_overrideRemaining <= 0)
            {
                EndOverride();
            }

            return;
        }

        if (_plan is null)
        {
            StartPlan(_planner.Plan());
        }

        if (_walking)
        {
            Walk(dt);
            return;
        }

        KeepInsideFreeZone();
        TickFidget(dt);
        _remaining -= dt;
        if (_remaining <= 0)
        {
            _plan = null;
        }
    }

    private void StartPlan(BehaviorPlan plan)
    {
        _plan = plan;
        Behavior = plan.Type;
        _fidgeting = false;
        if (plan.TargetX is { } target && Math.Abs(target - _world.PetX) > 2)
        {
            _walking = true;
            _speed = 0;
            var right = target > _world.PetX;
            Enter(PetState.Walking, right ? PetActivity.WalkRight : PetActivity.WalkLeft, right);
            return;
        }

        Arrive(plan);
    }

    /// <summary>
    /// Eased walking: accelerates from rest, cruises, then decelerates so the cat settles into its spot instead of
    /// stopping dead. A small creep speed guarantees it always arrives exactly at the target.
    /// </summary>
    private void Walk(double dt)
    {
        var plan = _plan!;
        var target = plan.TargetX!.Value;
        var maxSpeed = Math.Max(1, _world.WalkSpeed * plan.SpeedMultiplier);
        var accel = maxSpeed * 2.5;
        var creep = maxSpeed * 0.15;
        var delta = target - _world.PetX;
        var distance = Math.Abs(delta);

        var stoppingDistance = _speed * _speed / (2 * accel);
        _speed = distance <= stoppingDistance
            ? Math.Max(creep, _speed - accel * dt)
            : Math.Min(maxSpeed, _speed + accel * dt);

        var step = Math.Max(_speed, creep) * dt;
        if (distance <= step)
        {
            _world.PetX = target;
            _walking = false;
            _speed = 0;
            Arrive(plan);
        }
        else
        {
            _world.PetX += Math.Sign(delta) * step;
        }
    }

    private void TickFidget(double dt)
    {
        if (_plan is not { State: PetState.Idle } plan)
        {
            return;
        }

        if (_fidgeting)
        {
            _fidgetRemaining -= dt;
            if (_fidgetRemaining <= 0)
            {
                _fidgeting = false;
                _fidgetCountdown = _nextFidgetDelay;
                Enter(plan.State, plan.Activity, FacingRight);
            }

            return;
        }

        _fidgetCountdown -= dt;
        if (_fidgetCountdown > 0)
        {
            return;
        }

        if (_planner.NextFidget() is not { } fidget)
        {
            _fidgetCountdown = 5;
            return;
        }

        _fidgeting = true;
        _fidgetRemaining = fidget.Seconds;
        _nextFidgetDelay = fidget.NextDelay;
        Enter(PetState.Idle, fidget.Activity, FacingRight);
    }

    private void Arrive(BehaviorPlan plan)
    {
        _walking = false;
        _remaining = plan.Seconds;
        _fidgeting = false;
        _fidgetCountdown = 3;
        Enter(plan.State, plan.Activity, plan.FacingRight ?? FacingRight);

        if (plan.FavoriteKey is not null)
        {
            _preferences.RecordVisit(plan.FavoriteKey);
        }
    }

    private void KeepInsideFreeZone()
    {
        var zones = TaskbarGeometry.GetZones(_world.Layout, _world.PetWidth);
        if (zones.Count == 0)
        {
            return;
        }

        var x = TaskbarGeometry.ClampToNearestZone(zones, _world.PetX, _world.PetWidth);
        if (Math.Abs(x - _world.PetX) > 0.5)
        {
            _world.PetX = x;
        }
    }

    private void Replan()
    {
        if (_overriding)
        {
            return;
        }

        _plan = null;
        _walking = false;
    }

    private void Override(PetState state, PetActivity activity, double seconds)
    {
        _overriding = true;
        _overrideRemaining = seconds;
        _walking = false;
        _fidgeting = false;
        _speed = 0;
        _plan = null;
        Behavior = PetBehaviorType.Reacting;
        Enter(state, activity, FacingRight);
    }

    private void EndOverride()
    {
        _overriding = false;
        _plan = null;
    }

    private void Enter(PetState state, PetActivity activity, bool facingRight)
    {
        State = state;
        Activity = activity;
        FacingRight = facingRight;
        _logger.LogDebug("Pet {Behavior}: {State}/{Activity}", Behavior, state, activity);
        StateChanged?.Invoke(this, new PetStateChangedEventArgs(state, activity, facingRight));
    }
}
