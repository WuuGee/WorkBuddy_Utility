using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Application.Pomodoro;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Pomodoro;
using FocusCatAI.Domain.Taskbar;
using FocusCatAI.Infrastructure;
using FocusCatAI.Presentation.ViewModels;
using FocusCatAI.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Temporary diagnostic (<c>WorkBuddy.exe --verify-settings</c>). Drives every settings path against the running app,
/// writes settings-verification.log into the data folder, then restores the user's original settings.
/// </summary>
public sealed class SettingsVerifier(IServiceProvider services)
{
    private readonly StringBuilder _report = new();
    private readonly Dictionary<string, bool> _groups = new();
    private int _failures;

    private ISettingsService Settings => services.GetRequiredService<ISettingsService>();

    /// <summary>Runs all checks. Returns true when every check passed.</summary>
    public async Task<bool> RunAsync()
    {
        var paths = services.GetRequiredService<IAppPaths>();
        var logStart = CurrentLogLength(paths);
        var original = Settings.Current;
        var originalPetX = services.GetRequiredService<IPetWorld>().PetX;
        _report.AppendLine($"WorkBuddy settings verification  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _report.AppendLine(new string('=', 78));

        try
        {
            Settings.ResetToDefaults();
            await Task.Delay(500);

            Storage(paths);
            var tray = services.GetRequiredService<TrayIconService>();
            tray.OpenSettings();
            await Task.Delay(500);
            var window = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
            await SettingsWindowChecks(window);
            var vm = window?.DataContext as SettingsViewModel ?? services.GetRequiredService<SettingsViewModel>();

            await General(vm);
            await Pet(vm, window);
            await PetSelectionChecks(vm, window);
            await ThemeChecks(vm, window);
            await Behavior(vm);
            await QuietHours(vm);
            Notifications(vm);
            Persistence(paths, vm);
            Advanced(vm, paths);

            window?.Close();
        }
        catch (Exception ex)
        {
            Record("Runtime", "Verifier completed without exception", false, ex.ToString());
        }
        finally
        {
            services.GetRequiredService<IPetWorld>().PetX = originalPetX;
            Settings.Update(_ => original);
            await Task.Delay(400);
        }

        var errors = NewLogProblems(paths, logStart);
        Record("Runtime", "No [ERR]/[WRN] lines written to the app log during the run", errors.Count == 0, string.Join(" | ", errors.Take(5)));

        _report.AppendLine(new string('=', 78));
        foreach (var group in _groups)
        {
            _report.AppendLine($"{(group.Value ? "PASS" : "FAIL")}  {group.Key}");
        }

        var ok = _failures == 0;
        _report.AppendLine($"OVERALL: {(ok ? "PASS" : "FAIL")} ({_failures} failed check(s))");
        Directory.CreateDirectory(paths.RootDirectory);
        await File.WriteAllTextAsync(Path.Combine(paths.RootDirectory, "settings-verification.log"), _report.ToString());
        return ok;
    }

    private void Storage(IAppPaths paths)
    {
        var file = Path.Combine(paths.RootDirectory, JsonSettingsService.FileName);
        var exists = File.Exists(file);
        Record("Storage", "settings.json exists", exists, file);
        if (!exists)
        {
            return;
        }

        var text = File.ReadAllText(file);
        Record("Storage", "Human readable (indented, multi-line)", text.Contains('\n') && text.Contains("  "), $"{text.Split('\n').Length} lines");
        try
        {
            using var doc = JsonDocument.Parse(text);
            Record("Storage", "Valid JSON", true, "");
            var version = doc.RootElement.TryGetProperty("SchemaVersion", out var v) ? v.GetInt32() : -1;
            Record("Storage", "SchemaVersion written", version == AppSettings.CurrentSchemaVersion, $"SchemaVersion={version}");
        }
        catch (JsonException ex)
        {
            Record("Storage", "Valid JSON", false, ex.Message);
        }
    }

    private async Task SettingsWindowChecks(SettingsWindow? window)
    {
        if (window is null)
        {
            Record("Settings Window", "Opens from tray (OpenSettings)", false, "no SettingsWindow found");
            return;
        }

        Record("Settings Window", "Opens from tray (OpenSettings)", window.IsVisible, "");
        Record("Settings Window", "Window size 720x520", Math.Abs(window.ActualWidth - 720) < 2 && Math.Abs(window.ActualHeight - 520) < 2,
            $"{window.ActualWidth:F0}x{window.ActualHeight:F0}");

        var pages = new[] { "PageGeneral", "PagePet", "PageBehavior", "PageQuiet", "PageNotifications", "PageAdvanced" };
        var list = (ListBox)window.FindName("Categories");
        for (var i = 0; i < pages.Length; i++)
        {
            list.SelectedIndex = i;
            await Task.Delay(50);
            var visible = pages.Where(p => ((FrameworkElement)window.FindName(p)).Visibility == Visibility.Visible).ToArray();
            Record("Settings Window", $"Category {i + 1} ({((ListBoxItem)list.Items[i]).Content}) shows only {pages[i]}", visible.SequenceEqual([pages[i]]), string.Join(",", visible));
        }

        var s = Settings.Current;
        CheckBoxMatches(window, "Start WorkBuddy when Windows starts", s.General.StartWithWindows);
        CheckBoxMatches(window, "Show pet", s.Pet.ShowPet);
        CheckBoxMatches(window, "Enable messages", s.Behavior.EnableMessages);
        CheckBoxMatches(window, "Enable quiet hours", s.QuietHours.Enabled);
        CheckBoxMatches(window, "Enable Pomodoro messages", s.Notifications.EnablePomodoroMessages);
        list.SelectedIndex = 0;
    }

    private void CheckBoxMatches(DependencyObject root, string content, bool expected)
    {
        var box = FindCheckBox(root, content);
        Record("Settings Window", $"Control '{content}' shows current value ({expected})", box?.IsChecked == expected, $"IsChecked={box?.IsChecked}");
    }

    private static CheckBox? FindCheckBox(DependencyObject parent, string content)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is CheckBox cb && cb.Content as string == content)
            {
                return cb;
            }

            var found = FindCheckBox(child, content);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private async Task General(SettingsViewModel vm)
    {
        vm.StartWithWindows = false;
        Record("General", "StartWithWindows saved", !Settings.Current.General.StartWithWindows, "stored only; registry effect is Priority 3");
        vm.StartWithWindows = true;
        vm.StartMinimized = true;
        Record("General", "StartMinimized saved", Settings.Current.General.StartMinimized, "applies at launch; restored at end");
        vm.StartMinimized = false;

        var layout = services.GetRequiredService<ITaskbarLayoutProvider>();
        vm.RunInTaskbarMode = false;
        var above = await WaitUntil(() => layout.Current.Mode == TaskbarPlacementMode.AboveTaskbar, 7000);
        Record("General", "RunInTaskbarMode=false moves pet above taskbar (live)", above, $"mode={layout.Current.Mode}");
        vm.RunInTaskbarMode = true;
        var back = await WaitUntil(() => layout.Current.Mode == TaskbarPlacementMode.InTaskbar, 7000);
        Record("General", "RunInTaskbarMode=true returns pet to taskbar (live)", back, $"mode={layout.Current.Mode}");
    }

    private async Task ThemeChecks(SettingsViewModel vm, SettingsWindow? settingsWindow)
    {
        var applier = services.GetRequiredService<ThemeApplier>();
        var hwnd = settingsWindow is null ? IntPtr.Zero : new System.Windows.Interop.WindowInteropHelper(settingsWindow).Handle;
        var original = Settings.Current.General.Theme;

        Func<System.Windows.Media.Color?> windowColor = () =>
            (settingsWindow?.TryFindResource("Brush.Window.Background") as System.Windows.Media.SolidColorBrush)?.Color;

        Record("Theme", "Picker lists System, Light and Dark", vm.Themes.SequenceEqual([ThemePreference.System, ThemePreference.Light, ThemePreference.Dark]), string.Join(",", vm.Themes));

        vm.Theme = ThemePreference.Light;
        var light = await WaitUntil(() => applier.Applied == EffectiveTheme.Light, 2000);
        var lightColor = windowColor();
        Record("Theme", "Light applies live", light && lightColor is not null, lightColor?.ToString() ?? "no color");

        vm.Theme = ThemePreference.Dark;
        var dark = await WaitUntil(() => applier.Applied == EffectiveTheme.Dark, 2000);
        var darkColor = windowColor();
        Record("Theme", "Dark applies live", dark && darkColor is not null, darkColor?.ToString() ?? "no color");
        Record("Theme", "Open Settings window resolves a different background (live DynamicResource swap)", lightColor is not null && darkColor is not null && lightColor != darkColor, $"{lightColor} -> {darkColor}");
        Record("Theme", "Settings window instance and HWND unchanged", settingsWindow is not null && settingsWindow.IsVisible && hwnd != IntPtr.Zero && hwnd == new System.Windows.Interop.WindowInteropHelper(settingsWindow).Handle, "");
        var storedTheme = JsonDocument.Parse(File.ReadAllText(Path.Combine(services.GetRequiredService<IAppPaths>().RootDirectory, JsonSettingsService.FileName)))
            .RootElement.GetProperty("General").GetProperty("Theme").GetString();
        Record("Theme", "Theme is saved to settings.json", storedTheme == "Dark", storedTheme ?? "null");

        for (var i = 0; i < 4; i++)
        {
            vm.Theme = i % 2 == 0 ? ThemePreference.Light : ThemePreference.Dark;
            await Task.Delay(120);
        }

        Record("Theme", "Exactly one theme dictionary after repeated switches", applier.ThemeDictionaryCount == 1, $"count={applier.ThemeDictionaryCount}");

        var systemLight = services.GetRequiredService<ISystemThemeProvider>().PrefersLightApps();
        vm.Theme = ThemePreference.System;
        var follows = await WaitUntil(() => applier.Applied == (systemLight ? EffectiveTheme.Light : EffectiveTheme.Dark), 2000);
        Record("Theme", "System follows the Windows app theme", follows, $"windows light={systemLight}, applied={applier.Applied}");

        var petWindow = services.GetRequiredService<PetWindow>();
        Record("Theme", "PetWindow is not themed or recreated", petWindow.IsVisible && ReferenceEquals(petWindow, services.GetRequiredService<PetWindow>()), "");

        Settings.Update(s => s with { General = s.General with { Theme = original } });
        await Task.Delay(250);
    }

    private async Task PetSelectionChecks(SettingsViewModel vm, SettingsWindow? settingsWindow)
    {
        var petWindow = services.GetRequiredService<PetWindow>();
        var hwnd = new System.Windows.Interop.WindowInteropHelper(petWindow).Handle;
        var petVm = services.GetRequiredService<PetViewModel>();
        var catalog = services.GetRequiredService<IPetCatalog>();

        Record("Pet Selection", "Default SelectedPetId is cat", Settings.Current.Pet.SelectedPetId == "cat" && vm.SelectedPet.Id == "cat", Settings.Current.Pet.SelectedPetId);
        Record("Pet Selection", "Picker lists all catalog pets", vm.Pets.Select(p => p.Id).SequenceEqual(catalog.All.Select(p => p.Id)), string.Join(",", vm.Pets.Select(p => p.Id)));

        vm.SelectedPet = vm.Pets.Single(p => p.Id == "ginger");
        var swapped = await WaitUntil(() => petVm.PetId == "ginger", 2000);
        Record("Pet Selection", "Choosing Ginger swaps the live skin", swapped, petVm.PetId);
        Record("Pet Selection", "PetWindow instance and HWND unchanged", ReferenceEquals(petWindow, services.GetRequiredService<PetWindow>()) && hwnd == new System.Windows.Interop.WindowInteropHelper(petWindow).Handle && hwnd != IntPtr.Zero, "");

        Settings.Update(s => s with { Pet = s.Pet with { SelectedPetId = "no-such-pet" } });
        await Task.Delay(300);
        Record("Pet Selection", "Unknown id falls back to the default pet", petVm.PetId == "cat" && vm.SelectedPet.Id == "cat", petVm.PetId);

        Settings.Update(s => s with { Pet = s.Pet with { SelectedPetId = "cat" } });
        await Task.Delay(200);
    }

    private async Task Pet(SettingsViewModel vm, SettingsWindow? settingsWindow)
    {
        var petWindow = services.GetRequiredService<PetWindow>();
        var petVm = services.GetRequiredService<PetViewModel>();
        var layout = services.GetRequiredService<ITaskbarLayoutProvider>();

        vm.ShowPet = false;
        var hidden = await WaitUntil(() => !petWindow.IsVisible, 2000);
        vm.ShowPet = true;
        var shown = await WaitUntil(() => petWindow.IsVisible, 2000);
        Record("Pet", "ShowPet: visible -> hidden -> visible", hidden && shown, $"hidden={hidden} visibleAgain={shown}");
        var box = settingsWindow is null ? null : FindCheckBox(settingsWindow, "Show pet");
        Record("Pet", "Settings window checkbox follows ShowPet", box?.IsChecked == true, $"IsChecked={box?.IsChecked}");

        // Above-taskbar placement is uncapped, so every size can be measured exactly.
        vm.Placement = PlacementPreference.AboveTaskbar;
        var placedAbove = await WaitUntil(() => layout.Current.Mode == TaskbarPlacementMode.AboveTaskbar, 7000);
        Record("Pet", "Placement=AboveTaskbar applies live", placedAbove, $"mode={layout.Current.Mode}");

        foreach (var size in new[] { PetSize.Small, PetSize.Normal, PetSize.Large, PetSize.Huge, PetSize.Normal })
        {
            var old = (int)Settings.Current.Pet.Size;
            vm.Size = size;
            var expected = (int)size;
            var ok = await WaitUntil(() => Math.Abs(petWindow.ActualWidth - expected) < 1 && Math.Abs(petWindow.ActualHeight - expected) < 1, 3000);
            Record("Pet", $"Size {old} -> {expected}px changes window", ok,
                $"OldSize={old} NewSize={expected} ActualWidth={petWindow.ActualWidth:F0} ActualHeight={petWindow.ActualHeight:F0}");
        }

        vm.Placement = PlacementPreference.InTaskbar;
        var placedIn = await WaitUntil(() => layout.Current.Mode == TaskbarPlacementMode.InTaskbar, 7000);
        Record("Pet", "Placement=InTaskbar applies live", placedIn, $"mode={layout.Current.Mode}");
        vm.Placement = PlacementPreference.Auto;
        var auto = await WaitUntil(() => layout.Current.Mode == TaskbarPlacementMode.InTaskbar, 7000);
        Record("Pet", "Placement=Auto resolves to taskbar on this machine", auto, $"mode={layout.Current.Mode}");

        var oldSpeed = petVm.AnimationSpeed;
        vm.AnimationSpeed = 1.8;
        Record("Pet", "AnimationSpeed read by animation loop", Math.Abs(petVm.AnimationSpeed - 1.8) < 0.001, $"OldSpeed={oldSpeed} NewSpeed={petVm.AnimationSpeed}");
        vm.AnimationSpeed = 1.0;

        await IntroCheck(vm);
    }

    private async Task IntroCheck(SettingsViewModel vm)
    {
        var world = services.GetRequiredService<IPetWorld>();
        var random = services.GetRequiredService<IRandomSource>();

        vm.EnableIntroAnimation = false;
        var offMachine = new RecordingStateMachine();
        new FirstLaunchIntro(new StubPreferences(), world, offMachine, new RecordingSpeaker(), random, NullLogger<FirstLaunchIntro>.Instance, Settings).Tick(TimeSpan.FromMilliseconds(100));
        Record("Pet", "EnableIntroAnimation=false skips the intro", offMachine.PlayCount == 0, $"Play calls={offMachine.PlayCount}");

        vm.EnableIntroAnimation = true;
        var onMachine = new RecordingStateMachine();
        new FirstLaunchIntro(new StubPreferences(), world, onMachine, new RecordingSpeaker(), random, NullLogger<FirstLaunchIntro>.Instance, Settings).Tick(TimeSpan.FromMilliseconds(100));
        Record("Pet", "EnableIntroAnimation=true runs the intro", onMachine.PlayCount == 1, $"Play calls={onMachine.PlayCount}");
        await Task.CompletedTask;
    }

    private async Task Behavior(SettingsViewModel vm)
    {
        var mood = services.GetRequiredService<MoodService>();
        vm.EnableSleep = false;
        vm.SleepDelaySeconds = 60;
        var applied = await WaitUntil(() => !mood.SleepEnabled && mood.SleepyAfter == TimeSpan.FromSeconds(60), 2000);
        Record("Behavior", "EnableSleep=false + SleepDelay=60s reach MoodService", applied, $"SleepEnabled={mood.SleepEnabled} SleepyAfter={mood.SleepyAfter.TotalSeconds}s");
        vm.EnableSleep = true;
        vm.SleepDelaySeconds = 300;
        var restored = await WaitUntil(() => mood.SleepEnabled && mood.SleepyAfter == TimeSpan.FromSeconds(300), 2000);
        Record("Behavior", "EnableSleep=true + SleepDelay=300s reach MoodService", restored, $"SleepEnabled={mood.SleepEnabled} SleepyAfter={mood.SleepyAfter.TotalSeconds}s");

        var clock = new TestClock();
        var presenter = new RecordingPresenter();
        var speaker = new PetSpeaker(presenter, new RecordingStateMachine(), clock, NullLogger<PetSpeaker>.Instance, Settings);
        void Say() => speaker.Say("verify");

        vm.EnableMessages = false;
        clock.Advance(TimeSpan.FromMinutes(3));
        Say();
        Record("Behavior", "EnableMessages=false suppresses speech", presenter.Count == 0, $"spoken={presenter.Count}");
        vm.EnableMessages = true;
        Say();
        Record("Behavior", "EnableMessages=true allows speech", presenter.Count == 1, $"spoken={presenter.Count}");

        vm.QuietStart = "00:00";
        vm.QuietEnd = "23:59";
        vm.QuietHoursEnabled = true;
        clock.Advance(TimeSpan.FromMinutes(20));
        Say();
        Record("Behavior", "Quiet hours active suppresses speech", presenter.Count == 1, $"spoken={presenter.Count}");
        vm.QuietHoursEnabled = false;
        Say();
        Record("Behavior", "Quiet hours disabled allows speech", presenter.Count == 2, $"spoken={presenter.Count}");

        vm.MessageFrequency = MessageFrequency.High;
        clock.Advance(TimeSpan.FromMinutes(6));
        Say();
        Record("Behavior", "Frequency High speaks after 6 min", presenter.Count == 3, $"spoken={presenter.Count}");
        vm.MessageFrequency = MessageFrequency.Normal;
        clock.Advance(TimeSpan.FromMinutes(6));
        Say();
        var normalBlocked = presenter.Count == 3;
        clock.Advance(TimeSpan.FromMinutes(10));
        Say();
        Record("Behavior", "Frequency Normal: silent at 6 min, speaks at 16 min", normalBlocked && presenter.Count == 4, $"spoken={presenter.Count}");
        vm.MessageFrequency = MessageFrequency.Low;
        clock.Advance(TimeSpan.FromMinutes(16));
        Say();
        var lowBlocked = presenter.Count == 4;
        clock.Advance(TimeSpan.FromMinutes(15));
        Say();
        Record("Behavior", "Frequency Low: silent at 16 min, speaks at 31 min", lowBlocked && presenter.Count == 5, $"spoken={presenter.Count}");
        vm.MessageFrequency = MessageFrequency.Normal;
    }

    private async Task QuietHours(SettingsViewModel vm)
    {
        vm.QuietStart = "21:15";
        vm.QuietEnd = "06:45";
        Record("Quiet Hours", "Start/End saved", Settings.Current.QuietHours.Start == new TimeOnly(21, 15) && Settings.Current.QuietHours.End == new TimeOnly(6, 45),
            $"{Settings.Current.QuietHours.Start}-{Settings.Current.QuietHours.End}");
        vm.QuietStart = "not a time";
        Record("Quiet Hours", "Invalid input is rejected", Settings.Current.QuietHours.Start == new TimeOnly(21, 15), $"Start={Settings.Current.QuietHours.Start}");
        vm.QuietHoursEnabled = true;
        Record("Quiet Hours", "Enable saved", Settings.Current.QuietHours.Enabled, "");
        vm.QuietHoursEnabled = false;
        Record("Quiet Hours", "Disable saved", !Settings.Current.QuietHours.Enabled, "");
        await Task.CompletedTask;
    }

    private void Notifications(SettingsViewModel vm)
    {
        var settings = Settings;
        var speaker = new RecordingSpeaker();

        vm.EnableProductivityMessages = false;
        var productivity = new ProductivityReactions(new FixedForeground(), speaker, new StubMood(), new TestClock(), new MonitoringOptions(), settings);
        productivity.Tick(TimeSpan.FromSeconds(3));
        var offCount = speaker.Said.Count;
        vm.EnableProductivityMessages = true;
        productivity.Tick(TimeSpan.FromSeconds(3));
        Record("Notifications", "Productivity messages off -> silent, on -> speaks", offCount == 0 && speaker.Said.Count == 1, $"off={offCount} on={speaker.Said.Count}");

        speaker.Said.Clear();
        var pomodoro = new StubPomodoro();
        _ = new PomodoroPetReactions(pomodoro, new StubMood(), new RecordingStateMachine(), speaker, PomodoroSettings.Default, settings);
        vm.EnablePomodoroMessages = false;
        pomodoro.Raise(new PomodoroEvent(PomodoroEventKind.WorkStarted));
        var pomOff = speaker.Said.Count;
        vm.EnablePomodoroMessages = true;
        pomodoro.Raise(new PomodoroEvent(PomodoroEventKind.WorkStarted));
        Record("Notifications", "Pomodoro messages off -> silent, on -> speaks", pomOff == 0 && speaker.Said.Count == 1, $"off={pomOff} on={speaker.Said.Count}");
    }

    private void Persistence(IAppPaths paths, SettingsViewModel vm)
    {
        vm.Size = PetSize.Large;
        vm.AnimationSpeed = 1.4;
        vm.MessageFrequency = MessageFrequency.Low;
        vm.QuietStart = "20:30";
        var expected = Settings.Current;
        var reloaded = new JsonSettingsService(paths, NullLogger<JsonSettingsService>.Instance).Current;
        Record("Persistence", "Fresh service instance (simulated restart) loads identical settings", reloaded == expected,
            $"Size={reloaded.Pet.Size} Speed={reloaded.Pet.AnimationSpeed} Freq={reloaded.Behavior.MessageFrequency} QuietStart={reloaded.QuietHours.Start}");
    }

    private void Advanced(SettingsViewModel vm, IAppPaths paths)
    {
        Try("Open Data Folder", () => vm.OpenDataFolder(), () => Directory.Exists(paths.RootDirectory));
        Try("Open Log Folder", () => vm.OpenLogFolder(), () => Directory.Exists(paths.LogsDirectory));

        var prefs = services.GetRequiredService<IPetPreferences>();
        Try("Reset Preferences", () => prefs.ResetFavorites(), () =>
            prefs.Favorites.Count == 4 && prefs.Favorites.Any(f => f.Key == "Start" && f.Weight == 2) && prefs.Favorites.Any(f => f.Key == "App:Teams" && f.Weight == 1));

        vm.Size = PetSize.Huge;
        vm.EnableMessages = false;
        Try("Reset All Settings", () => vm.ResetAllSettings(), () => Settings.Current == new AppSettings());
    }

    private void Try(string name, Action action, Func<bool> verify)
    {
        try
        {
            action();
            Record("Advanced", name, verify(), "");
        }
        catch (Exception ex)
        {
            Record("Advanced", name, false, ex.Message);
        }
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var until = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < until)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return condition();
    }

    private void Record(string group, string check, bool pass, string detail)
    {
        if (!pass)
        {
            _failures++;
        }

        _groups[group] = _groups.GetValueOrDefault(group, true) && pass;
        _report.AppendLine($"[{(pass ? "PASS" : "FAIL")}] {group,-14} {check}{(string.IsNullOrEmpty(detail) ? "" : "  -> " + detail)}");
    }

    private static string? CurrentLogFile(IAppPaths paths) =>
        Directory.Exists(paths.LogsDirectory)
            ? Directory.GetFiles(paths.LogsDirectory, "workbuddy-*.log").OrderBy(File.GetLastWriteTimeUtc).LastOrDefault()
            : null;

    private static long CurrentLogLength(IAppPaths paths) => CurrentLogFile(paths) is { } f ? new FileInfo(f).Length : 0;

    private static List<string> NewLogProblems(IAppPaths paths, long offset)
    {
        var problems = new List<string>();
        if (CurrentLogFile(paths) is not { } file)
        {
            return problems;
        }

        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (line.Contains("[ERR]") || line.Contains("[WRN]") || line.Contains("[FTL]"))
            {
                problems.Add(line);
            }
        }

        return problems;
    }

    private sealed class TestClock : IClock
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public DateTimeOffset UtcNow => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class RecordingPresenter : ISpeechPresenter
    {
        public int Count { get; private set; }
        public void Show(string text, TimeSpan duration) => Count++;
    }

    private sealed class RecordingSpeaker : IPetSpeaker
    {
        public List<string> Said { get; } = [];
        public void Say(string text, SpeechPriority priority = SpeechPriority.Normal) => Said.Add(text);
    }

    private sealed class RecordingStateMachine : IPetStateMachine
    {
        public int PlayCount { get; private set; }
        public PetState State => PetState.Idle;
        public PetActivity Activity => PetActivity.Sit;
        public bool FacingRight => true;
        public PetBehaviorType Behavior => PetBehaviorType.Sitting;
        public event EventHandler<PetStateChangedEventArgs>? StateChanged { add { } remove { } }
        public void Tick(TimeSpan elapsed) { }
        public void Trigger(PetTrigger trigger) { }
        public void Play(BehaviorPlan plan) => PlayCount++;
    }

    private sealed class StubPreferences : IPetPreferences
    {
        public IReadOnlyList<FavoriteSpot> Favorites => [];
        public bool HasIntroduced => false;
        public void RecordVisit(string key) { }
        public void Save() { }
        public void MarkIntroduced() { }
        public void ResetFavorites() { }
    }

    private sealed class StubMood : IMoodService
    {
        public PetMood Current => PetMood.Relaxed;
        public event EventHandler<PetMood>? Changed { add { } remove { } }
        public void Set(PetMood mood, TimeSpan? lifetime = null) { }
    }

    private sealed class FixedForeground : IForegroundAppProvider
    {
        public ForegroundApp? GetForeground() => new("devenv", null);
    }

    private sealed class StubPomodoro : IPomodoroService
    {
        public PomodoroPhase Phase => PomodoroPhase.Work;
        public PomodoroRunState RunState => PomodoroRunState.Running;
        public TimeSpan Remaining => TimeSpan.Zero;
        public event EventHandler<PomodoroEvent>? EventRaised;
        public void Raise(PomodoroEvent e) => EventRaised?.Invoke(this, e);
        public void Start() { }
        public void Pause() { }
        public void Resume() { }
        public void Stop() { }
    }
}
