using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Application.Pets;

public sealed class MoodService : IMoodService, ITickable
{
    private readonly IUserIdleProvider _idle;
    private readonly IRandomSource _random;
    private readonly ILogger<MoodService> _logger;
    private double _lifetimeLeft = double.PositiveInfinity;
    private double _nextCuriosity;

    public MoodService(IUserIdleProvider idle, IRandomSource random, ILogger<MoodService> logger)
    {
        _idle = idle;
        _random = random;
        _logger = logger;
        _nextCuriosity = NextCuriosityDelay();
    }

    public TimeSpan SleepyAfter { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>When false the pet never becomes Sleepy from inactivity.</summary>
    public bool SleepEnabled { get; set; } = true;
    public PetMood Current { get; private set; } = PetMood.Relaxed;
    public event EventHandler<PetMood>? Changed;

    public void Set(PetMood mood, TimeSpan? lifetime = null)
    {
        _lifetimeLeft = lifetime?.TotalSeconds ?? double.PositiveInfinity;
        Apply(mood);
    }

    public void Tick(TimeSpan elapsed)
    {
        if (Current == PetMood.Focused)
        {
            return;
        }

        var idle = _idle.IdleTime;
        if (Current == PetMood.Sleepy)
        {
            if (idle < TimeSpan.FromSeconds(10) || !SleepEnabled)
            {
                _lifetimeLeft = double.PositiveInfinity;
                Apply(PetMood.Relaxed);
            }

            return;
        }

        if (SleepEnabled && idle >= SleepyAfter)
        {
            _lifetimeLeft = double.PositiveInfinity;
            Apply(PetMood.Sleepy);
            return;
        }

        if (!double.IsPositiveInfinity(_lifetimeLeft))
        {
            _lifetimeLeft -= elapsed.TotalSeconds;
            if (_lifetimeLeft <= 0)
            {
                _lifetimeLeft = double.PositiveInfinity;
                Apply(PetMood.Relaxed);
            }

            return;
        }

        if (Current == PetMood.Relaxed)
        {
            _nextCuriosity -= elapsed.TotalSeconds;
            if (_nextCuriosity <= 0)
            {
                _nextCuriosity = NextCuriosityDelay();
                Set(PetMood.Curious, TimeSpan.FromSeconds(60 + _random.NextDouble() * 60));
            }
        }
    }

    private double NextCuriosityDelay() => 180 + _random.NextDouble() * 240;

    private void Apply(PetMood mood)
    {
        if (mood == Current)
        {
            return;
        }

        _logger.LogInformation("Pet mood {Previous} -> {Current}", Current, mood);
        Current = mood;
        Changed?.Invoke(this, mood);
    }
}
