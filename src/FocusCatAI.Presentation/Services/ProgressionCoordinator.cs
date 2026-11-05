using System.Windows.Threading;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Progression;
using FocusCatAI.Domain.Progression;
using FocusCatAI.Presentation.ViewModels;
using FocusCatAI.Presentation.Views;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Drives passive progression: one tick a minute while the pet is visible, and a quiet level-up reaction
/// (Happy animation plus an optional speech bubble). Never shows a dialog or a toast.
/// </summary>
public sealed class ProgressionCoordinator : IDisposable
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private readonly IProgressionService _progression;
    private readonly PetWindow _petWindow;
    private readonly PetViewModel _pet;
    private readonly ISettingsService _settings;
    private readonly ISpeechPresenter _speech;
    private readonly ILogger<ProgressionCoordinator> _logger;
    private DispatcherTimer? _timer;

    public ProgressionCoordinator(IProgressionService progression, PetWindow petWindow, PetViewModel pet,
        ISettingsService settings, ISpeechPresenter speech, ILogger<ProgressionCoordinator> logger)
    {
        _progression = progression;
        _petWindow = petWindow;
        _pet = pet;
        _settings = settings;
        _speech = speech;
        _logger = logger;
    }

    /// <summary>Starts ticking. Call once, on the UI thread, after startup.</summary>
    public void Start()
    {
        _progression.LeveledUp += OnLeveledUp;
        _progression.AchievementsUnlocked += OnAchievementsUnlocked;
        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Background, (_, _) => Tick(), Dispatcher.CurrentDispatcher);
        _timer.Start();
        _logger.LogInformation("Progression started at level {Level} ({Xp} XP)", _progression.Progress.Level, _progression.Current.TotalXp);
    }

    /// <summary>Grants one companionship tick if the pet is visible. Public so diagnostics can drive it.</summary>
    public void Tick()
    {
        if (_petWindow.IsVisible)
        {
            _progression.RecordCompanionshipTick();
        }
    }

    // A grant raises LeveledUp and then AchievementsUnlocked back to back. Both are collected into one pending reaction that is
    // delivered once on the UI thread, so a single grant yields one Celebrate() and one bubble, never a stack.
    private int? _pendingLevel;
    private List<AchievementDefinition>? _pendingAchievements;
    private bool _reactionQueued;

    private void OnLeveledUp(object? sender, int level) => Enqueue(level, null);

    private void OnAchievementsUnlocked(object? sender, IReadOnlyList<AchievementDefinition> unlocked) => Enqueue(null, unlocked);

    private void Enqueue(int? level, IReadOnlyList<AchievementDefinition>? unlocked)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            _pendingLevel ??= level;
            if (unlocked is not null)
            {
                (_pendingAchievements ??= []).AddRange(unlocked);
            }

            if (_reactionQueued)
            {
                return;
            }

            _reactionQueued = true;
            dispatcher.BeginInvoke(DeliverReaction, DispatcherPriority.Background);
        });
    }

    private void DeliverReaction()
    {
        var level = _pendingLevel;
        var achievements = _pendingAchievements;
        (_pendingLevel, _pendingAchievements, _reactionQueued) = (null, null, false);

        if (level is { } l)
        {
            _logger.LogInformation("Companion reached level {Level}", l);
        }

        foreach (var a in achievements ?? [])
        {
            _logger.LogInformation("Achievement unlocked: {Id}", a.Id);
        }

        // Unlocks are always recorded; a hidden pet has nothing to animate, so nothing is shown.
        if (!_petWindow.IsVisible)
        {
            return;
        }

        _pet.Celebrate();
        if (_settings.Current.Behavior.EnableMessages)
        {
            _speech.Show(BuildMessage(level, achievements), TimeSpan.FromSeconds(4));
        }
    }

    /// <summary>One short line: the first achievement when there is one, otherwise the level-up.</summary>
    public static string BuildMessage(int? level, IReadOnlyList<AchievementDefinition>? achievements) =>
        achievements is { Count: > 0 } ? $"Achievement: {achievements[0].Title}!" : $"Level {level}!";

    public void Dispose()
    {
        _timer?.Stop();
        _progression.LeveledUp -= OnLeveledUp;
        _progression.AchievementsUnlocked -= OnAchievementsUnlocked;
    }
}
