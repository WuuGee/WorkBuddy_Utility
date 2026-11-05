using FocusCatAI.Domain.Progression;

namespace FocusCatAI.Application.Progression;

public interface IProgressionStore
{
    ProgressionState Load();
    void Save(ProgressionState state);
}

public interface IProgressionService : IDisposable
{
    ProgressionState Current { get; }
    LevelProgress Progress { get; }
    event EventHandler? Changed;
    event EventHandler<int>? LeveledUp;

    /// <summary>Raised once per grant with the achievements that just unlocked (never for the silent catch-up at load).</summary>
    event EventHandler<IReadOnlyList<AchievementDefinition>>? AchievementsUnlocked;
    void RecordCompanionshipTick();
    void RecordInteraction();

    /// <summary>Writes any unsaved state to the store now.</summary>
    void Flush();
}
