using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Application.Pets;

/// <summary>
/// Gentle productivity reactions. Keeps only in-memory counters (process names, switch counts, durations);
/// nothing is persisted. Messages are rare by design: <see cref="PetSpeaker"/> enforces the cooldown, and
/// each rule fires at most once per app session / focus stretch.
/// </summary>
public sealed class ProductivityReactions(
    IForegroundAppProvider foreground,
    IPetSpeaker speaker,
    IMoodService mood,
    IClock clock,
    MonitoringOptions options,
    ISettingsService? settings = null) : ITickable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SwitchWindow = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan LongFocus = TimeSpan.FromMinutes(40);
    private const int SwitchThreshold = 12;

    private readonly Queue<DateTimeOffset> _switches = new();
    private readonly HashSet<string> _greeted = new(StringComparer.OrdinalIgnoreCase);
    private double _sincePoll;
    private string? _currentProcess;
    private DateTimeOffset _focusStart;
    private bool _longFocusSaid;
    private DateTimeOffset _lastDistractionSaid = DateTimeOffset.MinValue;

    public void Tick(TimeSpan elapsed)
    {
        if (!options.Enabled || settings?.Current.Notifications.EnableProductivityMessages == false)
        {
            return;
        }

        _sincePoll += elapsed.TotalSeconds;
        if (_sincePoll < PollInterval.TotalSeconds)
        {
            return;
        }

        _sincePoll = 0;
        var app = foreground.GetForeground();
        if (app is null)
        {
            return;
        }

        var now = clock.UtcNow;
        if (!string.Equals(app.ProcessName, _currentProcess, StringComparison.OrdinalIgnoreCase))
        {
            _currentProcess = app.ProcessName;
            _switches.Enqueue(now);
            OnAppActivated(app);
        }

        while (_switches.Count > 0 && now - _switches.Peek() > SwitchWindow)
        {
            _switches.Dequeue();
        }

        if (_switches.Count >= SwitchThreshold && now - _lastDistractionSaid > TimeSpan.FromMinutes(20))
        {
            _lastDistractionSaid = now;
            _switches.Clear();
            _focusStart = now;
            _longFocusSaid = false;
            speaker.Say("Let's finish one thing first.");
            return;
        }

        if (_focusStart == default)
        {
            _focusStart = now;
        }

        if (!_longFocusSaid && _switches.Count <= 3 && now - _focusStart >= LongFocus)
        {
            _longFocusSaid = true;
            mood.Set(PetMood.Happy, TimeSpan.FromMinutes(2));
            speaker.Say("Great concentration today.");
        }
    }

    private void OnAppActivated(ForegroundApp app)
    {
        var message = Classify(app);
        if (message is { } m && _greeted.Add(m.Key))
        {
            speaker.Say(m.Text);
        }
    }

    private (string Key, string Text)? Classify(ForegroundApp app)
    {
        var name = app.ProcessName;
        if (name.Equals("devenv", StringComparison.OrdinalIgnoreCase) || name.Equals("Code", StringComparison.OrdinalIgnoreCase))
        {
            return ("code", "Working on code?");
        }

        if (name.StartsWith("ms-teams", StringComparison.OrdinalIgnoreCase) || name.Equals("Teams", StringComparison.OrdinalIgnoreCase))
        {
            return ("teams", "Meeting time?");
        }

        if (options.DetectWebsiteTitles && app.Title is not null &&
            app.Title.Contains("GitHub", StringComparison.OrdinalIgnoreCase))
        {
            return ("github", "Building something cool?");
        }

        return null;
    }
}
