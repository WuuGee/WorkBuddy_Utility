using FocusCatAI.Domain.Progression;

namespace FocusCatAI.Application.Progression;

/// <summary>
/// Holds progression in memory and saves it to <see cref="IProgressionStore"/> at most every <see cref="SaveInterval"/>,
/// immediately on level-up, and on <see cref="Flush"/>/<see cref="Dispose"/>. Independent of settings, so ticks never
/// raise a settings change.
/// </summary>
public sealed class ProgressionService : IProgressionService
{
    public static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(5);

    private readonly IProgressionStore _store;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private ProgressionState _state;
    private bool _dirty;
    private DateTimeOffset _lastSave;
    private bool _disposed;

    public ProgressionService(IProgressionStore store, TimeProvider time)
    {
        _store = store;
        _time = time;
        _state = store.Load();
        _lastSave = time.GetUtcNow();

        // Achievements already satisfied by loaded progress are recorded silently: no event, no reaction.
        var (caughtUp, newly) = AchievementEvaluator.Apply(_state);
        if (newly.Count > 0)
        {
            _state = caughtUp;
            _dirty = true;
        }
    }

    public event EventHandler? Changed;
    public event EventHandler<int>? LeveledUp;
    public event EventHandler<IReadOnlyList<AchievementDefinition>>? AchievementsUnlocked;

    public ProgressionState Current
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public LevelProgress Progress => LevelCurve.Describe(Current.TotalXp);

    public void RecordCompanionshipTick() => Apply(XpGrantPolicy.GrantCompanionshipTick);

    public void RecordInteraction() => Apply(s => XpGrantPolicy.GrantInteraction(s, _time.GetUtcNow().UtcDateTime));

    public void Flush()
    {
        lock (_gate)
        {
            SaveLocked();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Flush();
        _disposed = true;
    }

    private void Apply(Func<ProgressionState, GrantResult> grant)
    {
        GrantResult result;
        IReadOnlyList<AchievementDefinition> unlocked;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            result = grant(_state);
            if (!result.Granted)
            {
                return;
            }

            var (withAchievements, fresh) = AchievementEvaluator.Apply(result.State);
            unlocked = fresh;
            _state = withAchievements;
            _dirty = true;
            if (result.LeveledUp || unlocked.Count > 0 || _time.GetUtcNow() - _lastSave >= SaveInterval)
            {
                SaveLocked();
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        if (result.LeveledUp)
        {
            LeveledUp?.Invoke(this, result.NewLevel);
        }

        if (unlocked.Count > 0)
        {
            AchievementsUnlocked?.Invoke(this, unlocked);
        }
    }

    private void SaveLocked()
    {
        if (!_dirty)
        {
            return;
        }

        _store.Save(_state);
        _dirty = false;
        _lastSave = _time.GetUtcNow();
    }
}
