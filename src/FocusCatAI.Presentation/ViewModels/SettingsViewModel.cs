using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Presentation.ViewModels;

public sealed record SleepDelayOption(string Label, int Seconds);

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

    public SettingsViewModel(ISettingsService settings, IAppPaths paths, IPetPreferences preferences, IStartupManager startup, IExternalLauncher launcher, IPetCatalog catalog)
    {
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
    public IReadOnlyList<PetDefinition> Pets => _catalog.All;

    /// <summary>Selected pet; an unknown stored id shows as the default pet.</summary>
    public PetDefinition SelectedPet
    {
        get => _catalog.Resolve(S.Pet.SelectedPetId);
        set
        {
            if (value is not null)
            {
                Edit(s => s with { Pet = s.Pet with { SelectedPetId = value.Id } });
            }
        }
    }
    public bool ShowPet { get => S.Pet.ShowPet; set => Edit(s => s with { Pet = s.Pet with { ShowPet = value } }); }
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

    // Advanced
    public void OpenDataFolder() => _launcher.OpenDataFolder();

    public void OpenLogFolder() => _launcher.OpenLogsFolder();

    /// <summary>Restores default favorite spots. Settings and the first-launch flag are untouched.</summary>
    public void ResetPreferences() => _preferences.ResetFavorites();

    public void ResetAllSettings() => _settings.ResetToDefaults();

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

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
    private void OnSettingsChanged(object? sender, AppSettings e) => OnPropertyChanged(string.Empty);
}
