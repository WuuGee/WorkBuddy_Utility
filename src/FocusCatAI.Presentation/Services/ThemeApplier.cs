using System.Windows;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Presentation.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Applies the selected theme live: swaps the single theme dictionary in Application.Resources (every brush is a
/// DynamicResource, so open windows restyle in place) and updates the title bar of the WorkBuddy windows.
/// PetWindow and SpeechBubbleWindow are deliberately not themed. Settings changes and Windows theme changes may
/// arrive on any thread, so both are marshalled to the UI thread.
/// </summary>
public sealed class ThemeApplier(ISettingsService settings, ISystemThemeProvider systemTheme, ILogger<ThemeApplier> logger) : IDisposable
{
    private const string LightSource = "pack://application:,,,/Themes/Theme.Light.xaml";
    private const string DarkSource = "pack://application:,,,/Themes/Theme.Dark.xaml";

    private ResourceDictionary? _current;
    private bool _started;
    private bool _disposed;

    /// <summary>The theme currently applied. Exposed for diagnostics.</summary>
    public EffectiveTheme? Applied { get; private set; }

    /// <summary>Number of theme dictionaries in Application.Resources; must always be 1 once started. Exposed for diagnostics.</summary>
    public int ThemeDictionaryCount =>
        System.Windows.Application.Current.Resources.MergedDictionaries.Count(IsThemeDictionary);

    /// <summary>Applies the saved theme and starts following changes. Call once on the UI thread, before the first window is shown.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Apply(Resolve());
        settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
    }

    /// <summary>Re-resolves from current settings and the Windows theme, and applies it if it changed. UI thread only.</summary>
    public void Refresh() => Apply(Resolve());

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        settings.Changed -= OnSettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private EffectiveTheme Resolve() =>
        ThemeResolver.Resolve(settings.Current.General.Theme, systemTheme.PrefersLightApps());

    private void OnSettingsChanged(object? sender, AppSettings e) => PostToUi(Refresh);

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Only matters while following Windows. Windows updates the registry slightly after the event.
        if (settings.Current.General.Theme == ThemePreference.System)
        {
            _ = Task.Delay(300).ContinueWith(_ => PostToUi(Refresh), TaskScheduler.Default);
        }
    }

    private void PostToUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (_disposed || dispatcher is null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                action();
            }
        });
    }

    private void Apply(EffectiveTheme theme)
    {
        var resources = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (Applied == theme && resources.Count(IsThemeDictionary) == 1)
        {
            return;
        }

        var next = new ResourceDictionary { Source = new Uri(theme == EffectiveTheme.Dark ? DarkSource : LightSource, UriKind.Absolute) };

        // Replace in place (same index) so the one theme dictionary never duplicates or moves.
        var index = _current is null ? -1 : resources.IndexOf(_current);
        if (index < 0)
        {
            foreach (var stale in resources.Where(IsThemeDictionary).ToList())
            {
                resources.Remove(stale);
            }

            resources.Add(next);
        }
        else
        {
            resources[index] = next;
        }

        _current = next;
        Applied = theme;

        foreach (var window in System.Windows.Application.Current.Windows.OfType<Window>().Where(IsThemedWindow))
        {
            WindowChromeTheme.Apply(window, theme == EffectiveTheme.Dark);
        }

        logger.LogInformation("Theme applied: {Theme} (preference {Preference})", theme, settings.Current.General.Theme);
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window && IsThemedWindow(window) && Applied is { } theme)
        {
            WindowChromeTheme.Apply(window, theme == EffectiveTheme.Dark);
        }
    }

    private static bool IsThemedWindow(Window window) => window is SettingsWindow or AboutWindow;

    private static bool IsThemeDictionary(ResourceDictionary d) =>
        d.Source is { } s && s.OriginalString.Contains("Themes/Theme.", StringComparison.OrdinalIgnoreCase);
}
