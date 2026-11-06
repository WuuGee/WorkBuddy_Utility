using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Application.Progression;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Progression;

namespace FocusCatAI.Presentation.ViewModels;

public sealed record SleepDelayOption(string Label, int Seconds);

/// <summary>One Companion picker entry. <see cref="Label"/> includes the unlock level while locked.</summary>
public sealed record PetChoice(PetDefinition Pet, string Label, bool IsUnlocked);

/// <summary>One Accessory picker entry. <see cref="Label"/> includes the unlock level while locked.</summary>
public sealed record AccessoryChoice(FocusCatAI.Domain.Accessories.AccessoryDefinition Accessory, string Label, bool IsUnlocked);

public sealed record AchievementRow(string Title, string Description, bool IsUnlocked)
{
    public string Status => IsUnlocked ? "Unlocked" : "Locked";
}

/// <summary>Binds the settings window to <see cref="ISettingsService"/>. Every edit is applied and saved immediately.</summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private const string TimeFormat = "HH:mm";

    private readonly ISettingsService _settings;
    private readonly IAppPaths _paths;
    private readonly IPetPreferences _preferences;
    private readonly IStartupManager _startup;
    private readonly IExternalLauncher _launcher;
    private readonly IPetCatalog _catalog;
    private readonly IAccessoryCatalog _accessories;
    private readonly IProgressionService _progression;
    private readonly IAiCompanionService? _ai;
    private readonly ILocalModelScanner? _scanner;
    private readonly Func<string, FocusCatAI.Domain.AI.CompanionReply?>? _ask;
    private string _askPrompt = string.Empty;
    private string _askReply = string.Empty;

    public SettingsViewModel(ISettingsService settings, IAppPaths paths, IPetPreferences preferences, IStartupManager startup,
        IExternalLauncher launcher, IPetCatalog catalog, IProgressionService progression, IAccessoryCatalog accessories,
        IAiCompanionService? ai = null, ILocalModelScanner? scanner = null, Func<string, FocusCatAI.Domain.AI.CompanionReply?>? ask = null)
    {
        _ai = ai;
        _scanner = scanner;
        _ask = ask;
        _accessories = accessories;
        _progression = progression;
        _progression.Changed += OnProgressionChanged;
        _catalog = catalog;
        _settings = settings;
        _paths = paths;
        _preferences = preferences;
        _startup = startup;
        _launcher = launcher;
        _settings.Changed += OnSettingsChanged;

        var options = new List<SleepDelayOption>
        {
            new("30 seconds", 30), new("1 minute", 60), new("2 minutes", 120), new("5 minutes", 300),
            new("10 minutes", 600), new("15 minutes", 900), new("30 minutes", 1800)
        };
        if (options.All(o => o.Seconds != settings.Current.Behavior.SleepDelaySeconds))
        {
            options.Add(new($"{settings.Current.Behavior.SleepDelaySeconds} seconds", settings.Current.Behavior.SleepDelaySeconds));
            options.Sort((a, b) => a.Seconds.CompareTo(b.Seconds));
        }

        SleepDelayOptions = options;
    }

    public IReadOnlyList<PetSize> Sizes { get; } = [PetSize.Small, PetSize.Normal, PetSize.Large, PetSize.Huge];
    public IReadOnlyList<PlacementPreference> Placements { get; } = Enum.GetValues<PlacementPreference>();
    public IReadOnlyList<MessageFrequency> Frequencies { get; } = Enum.GetValues<MessageFrequency>();
    public IReadOnlyList<SleepDelayOption> SleepDelayOptions { get; }
    public string DataFolder => _paths.RootDirectory;
    public string LogFolder => _paths.LogsDirectory;
    public string VersionText => $"WorkBuddy {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3)}";

    private AppSettings S => _settings.Current;
    private void Edit(Func<AppSettings, AppSettings> change) => _settings.Update(change);

    // General
    // The checkbox shows what Windows will really do, read from the registry each time, so it cannot disagree with it.
    public bool StartWithWindows
    {
        get => _startup.GetStatus().WillStartAtSignIn;
        set
        {
            Edit(s => s with { General = s.General with { StartWithWindows = value } });
            if (value)
            {
                _startup.Enable();
            }
            else
            {
                _startup.Disable();
            }

            OnPropertyChanged(string.Empty);
        }
    }

    /// <summary>Explains why startup is not active when the registry says so; empty when everything is as expected.</summary>
    public string StartupStatusText => _startup.GetStatus().State switch
    {
        StartupState.DisabledByWindows => "Turned off in Windows (Settings > Apps > Startup). Tick the box to turn it back on.",
        StartupState.InvalidPath => "The registered startup entry points to a missing file. Tick the box to repair it.",
        StartupState.PathMismatch => "The startup entry points to a different copy of WorkBuddy. Tick the box to use this one.",
        _ => string.Empty
    };
    public bool StartMinimized { get => S.General.StartMinimized; set => Edit(s => s with { General = s.General with { StartMinimized = value } }); }
    public bool RunInTaskbarMode { get => S.General.RunInTaskbarMode; set => Edit(s => s with { General = s.General with { RunInTaskbarMode = value } }); }
    public string Language => "English (more languages later)";
    public IReadOnlyList<ThemePreference> Themes { get; } = Enum.GetValues<ThemePreference>();
    public ThemePreference Theme { get => S.General.Theme; set => Edit(s => s with { General = s.General with { Theme = value } }); }

    // Pet
    /// <summary>Picker entries in catalog order; locked ones are disabled and say when they unlock.</summary>
    public IReadOnlyList<PetChoice> Pets => _catalog.All
        .Select(p => new PetChoice(p, PetUnlockPolicy.DisplayLabel(p, _progression.Progress.Level), PetUnlockPolicy.IsUnlocked(p, _progression.Progress.Level)))
        .ToList();

    /// <summary>Selected pet as shown (a locked or unknown stored id displays as the pet actually in use).</summary>
    public PetChoice SelectedPet
    {
        get
        {
            var effective = _catalog.Effective(S.Pet.SelectedPetId, _progression.Progress.Level);
            return Pets.First(p => p.Pet.Id == effective.Id);
        }
        set
        {
            if (value is { IsUnlocked: true })
            {
                Edit(s => s with { Pet = s.Pet with { SelectedPetId = value.Pet.Id } });
            }
        }
    }
    public bool ShowPet { get => S.Pet.ShowPet; set => Edit(s => s with { Pet = s.Pet with { ShowPet = value } }); }

    /// <summary>Accessory picker entries in catalog order; locked ones are disabled and say when they unlock.</summary>
    public IReadOnlyList<AccessoryChoice> Accessories => _accessories.All
        .Select(a => new AccessoryChoice(a, FocusCatAI.Domain.Accessories.AccessoryUnlockPolicy.DisplayLabel(a, _progression.Progress.Level), FocusCatAI.Domain.Accessories.AccessoryUnlockPolicy.IsUnlocked(a, _progression.Progress.Level)))
        .ToList();

    /// <summary>Selected accessory as shown (a locked or unknown stored id displays as the accessory actually in use).</summary>
    public AccessoryChoice SelectedAccessory
    {
        get
        {
            var effective = FocusCatAI.Application.Accessories.AccessorySelection.Effective(_accessories, S.Pet.SelectedAccessoryId, _progression.Progress.Level);
            return Accessories.First(a => a.Accessory.Id == effective.Id);
        }
        set
        {
            if (value is { IsUnlocked: true })
            {
                Edit(s => s with { Pet = s.Pet with { SelectedAccessoryId = value.Accessory.Id } });
            }
        }
    }
    public PetSize Size { get => S.Pet.Size; set => Edit(s => s with { Pet = s.Pet with { Size = value } }); }
    public double AnimationSpeed { get => S.Pet.AnimationSpeed; set => Edit(s => s with { Pet = s.Pet with { AnimationSpeed = Math.Round(value, 1) } }); }
    public PlacementPreference Placement { get => S.Pet.Placement; set => Edit(s => s with { Pet = s.Pet with { Placement = value } }); }
    public bool EnableIntroAnimation { get => S.Pet.EnableIntroAnimation; set => Edit(s => s with { Pet = s.Pet with { EnableIntroAnimation = value } }); }

    // Behavior
    public bool EnableMessages { get => S.Behavior.EnableMessages; set => Edit(s => s with { Behavior = s.Behavior with { EnableMessages = value } }); }
    public MessageFrequency MessageFrequency { get => S.Behavior.MessageFrequency; set => Edit(s => s with { Behavior = s.Behavior with { MessageFrequency = value } }); }
    public bool EnableSleep { get => S.Behavior.EnableSleep; set => Edit(s => s with { Behavior = s.Behavior with { EnableSleep = value } }); }
    public int SleepDelaySeconds { get => S.Behavior.SleepDelaySeconds; set => Edit(s => s with { Behavior = s.Behavior with { SleepDelaySeconds = value } }); }

    // Quiet hours
    public bool QuietHoursEnabled { get => S.QuietHours.Enabled; set => Edit(s => s with { QuietHours = s.QuietHours with { Enabled = value } }); }

    public string QuietStart
    {
        get => S.QuietHours.Start.ToString(TimeFormat, CultureInfo.InvariantCulture);
        set => SetTime(value, t => Edit(s => s with { QuietHours = s.QuietHours with { Start = t } }), nameof(QuietStart));
    }

    public string QuietEnd
    {
        get => S.QuietHours.End.ToString(TimeFormat, CultureInfo.InvariantCulture);
        set => SetTime(value, t => Edit(s => s with { QuietHours = s.QuietHours with { End = t } }), nameof(QuietEnd));
    }

    // Notifications
    public bool EnableProductivityMessages { get => S.Notifications.EnableProductivityMessages; set => Edit(s => s with { Notifications = s.Notifications with { EnableProductivityMessages = value } }); }
    public bool EnablePomodoroMessages { get => S.Notifications.EnablePomodoroMessages; set => Edit(s => s with { Notifications = s.Notifications with { EnablePomodoroMessages = value } }); }

    // AI Companion
    public bool AiEnabled { get => S.Ai.Enabled; set => Edit(s => s with { Ai = s.Ai with { Enabled = value } }); }
    public IReadOnlyList<FocusCatAI.Domain.AI.AiDevicePreference> AiDevices { get; } = Enum.GetValues<FocusCatAI.Domain.AI.AiDevicePreference>();
    public FocusCatAI.Domain.AI.AiDevicePreference AiDevice { get => S.Ai.Device; set => Edit(s => s with { Ai = s.Ai with { Device = value } }); }

    public string AiHardwareText
    {
        get
        {
            if (_ai is null)
            {
                return "AI engine unavailable";
            }

            var device = FocusCatAI.Domain.AI.AiDeviceResolver.Resolve(S.Ai.Device, _ai.Hardware);
            return $"Detected: {_ai.Hardware.Summary}. Active device: {device}";
        }
    }

    public int AiModelCount
    {
        get
        {
            try
            {
                return _scanner?.ListInstalledModels().Count ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    public string AiModelsText => $"Local models found in {_paths.ModelsDirectory}: {AiModelCount}";

    public string AskPrompt { get => _askPrompt; set => SetProperty(ref _askPrompt, value); }

    public string AskReply { get => _askReply; private set => SetProperty(ref _askReply, value); }

    public void OpenModelsFolder() => _launcher.OpenModelsFolder();

    /// <summary>Asks the companion (also shown in the pet's speech bubble) and displays the reply in the card.</summary>
    public void AskCompanion()
    {
        if (!S.Ai.Enabled)
        {
            AskReply = "AI companion is turned off.";
            return;
        }

        var reply = _ask?.Invoke(AskPrompt);
        if (reply is null && _ai is not null && !string.IsNullOrWhiteSpace(AskPrompt))
        {
            reply = _ai.GenerateReply(new FocusCatAI.Domain.AI.CompanionPromptContext(AskPrompt.Trim(), S.Pet.SelectedPetId, _progression.Progress.Level, DateTime.Now.Hour));
        }

        AskReply = reply?.Text ?? "Type something to ask your companion.";
    }

    // Advanced
    public void OpenDataFolder() => _launcher.OpenDataFolder();

    public void OpenLogFolder() => _launcher.OpenLogsFolder();

    /// <summary>Restores default favorite spots. Settings and the first-launch flag are untouched.</summary>
    public void ResetPreferences() => _preferences.ResetFavorites();

    public void ResetAllSettings() => _settings.ResetToDefaults();

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _progression.Changed -= OnProgressionChanged;
    }

    // Companion progress (read-only)
    public string LevelText => _progression.Progress.IsMax ? $"Lv. {_progression.Progress.Level} (MAX)" : $"Lv. {_progression.Progress.Level}";
    public double ProgressFraction => _progression.Progress.ProgressFraction;
    public string XpText => _progression.Progress.IsMax ? "Max level reached" : $"{_progression.Progress.CurrentLevelXp} / {_progression.Progress.NextLevelXp} XP";
    public string CompanionshipTimeText => $"Time together: {FormatMinutes(_progression.Current.TotalCompanionshipMinutes)}";

    // Achievements (read-only)
    public IReadOnlyList<AchievementRow> Achievements => AchievementCatalog.All
        .Select(a => new AchievementRow(a.Title, a.Description, _progression.Current.UnlockedAchievementIds.Contains(a.Id, StringComparer.Ordinal)))
        .ToList();

    public string AchievementSummary => $"{Achievements.Count(a => a.IsUnlocked)} of {AchievementCatalog.All.Count} unlocked";

    /// <summary>Formats minutes as "2 h 5 min", or "5 min" under an hour.</summary>
    public static string FormatMinutes(long minutes) =>
        minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min";

    // Progression raises Changed from the UI thread's timer and from drag handlers, but guard anyway.
    private void OnProgressionChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            RaiseProgressionProperties();
        }
        else
        {
            dispatcher.BeginInvoke(RaiseProgressionProperties);
        }
    }

    private void RaiseProgressionProperties()
    {
        OnPropertyChanged(nameof(Pets));
        OnPropertyChanged(nameof(SelectedPet));
        OnPropertyChanged(nameof(Accessories));
        OnPropertyChanged(nameof(SelectedAccessory));
        OnPropertyChanged(nameof(Achievements));
        OnPropertyChanged(nameof(AchievementSummary));
        OnPropertyChanged(nameof(LevelText));
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(XpText));
        OnPropertyChanged(nameof(CompanionshipTimeText));
    }

    private void SetTime(string text, Action<TimeOnly> apply, string property)
    {
        if (TimeOnly.TryParseExact(text?.Trim(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            apply(time);
        }

        // Invalid text snaps back to the stored value.
        OnPropertyChanged(property);
    }

    // Raised from the settings service, possibly on another thread; the window marshals bindings via the dispatcher.
    private void OnSettingsChanged(object? sender, AppSettings e)
    {
        OnPropertyChanged(string.Empty);
        OnPropertyChanged(nameof(AiHardwareText));
        OnPropertyChanged(nameof(AiModelsText));
        OnPropertyChanged(nameof(AiModelCount));
    }
}
