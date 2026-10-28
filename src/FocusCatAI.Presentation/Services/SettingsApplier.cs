using System.Windows;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Application.Settings;
using FocusCatAI.Presentation.Views;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Pushes settings into the running app: sleep behaviour and pet visibility. Size, animation speed and placement are
/// read live by the view model and taskbar service, so they need no push. Changes may arrive on any thread.
/// </summary>
public sealed class SettingsApplier(
    ISettingsService settings,
    MoodService mood,
    PetWindow petWindow,
    IStartupManager startup,
    ILogger<SettingsApplier> logger)
{
    private bool _startMinimizedPending;

    /// <summary>Applies the current settings and starts following changes. Call once, on the UI thread, after startup.</summary>
    public void Start(bool startMinimized)
    {
        _startMinimizedPending = startMinimized;
        Apply(settings.Current);
        settings.Changed += (_, s) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => Apply(s));
    }

    /// <summary>Whether the pet window should be visible right now.</summary>
    public bool ShouldShowPet(AppSettings s) => s.Pet.ShowPet && !_startMinimizedPending;

    private void Apply(AppSettings s)
    {
        mood.SleepEnabled = s.Behavior.EnableSleep;
        mood.SleepyAfter = TimeSpan.FromSeconds(s.Behavior.SleepDelaySeconds);
        logger.LogInformation("Settings applied: sleep={Sleep} delay={Delay}s messages={Messages} frequency={Frequency} quiet={Quiet} size={Size} speed={Speed} placement={Placement} show={Show}",
            s.Behavior.EnableSleep, s.Behavior.SleepDelaySeconds, s.Behavior.EnableMessages, s.Behavior.MessageFrequency,
            s.QuietHours.Enabled, s.Pet.Size, s.Pet.AnimationSpeed, s.Pet.Placement, s.Pet.ShowPet);

        SetPetVisible(ShouldShowPet(s));
        startup.Synchronize(s.General.StartWithWindows);
    }

    /// <summary>Tray "Show Cat" overrides Start Minimized for the rest of this session.</summary>
    public void ShowPet()
    {
        _startMinimizedPending = false;
        settings.Update(s => s with { Pet = s.Pet with { ShowPet = true } });
        SetPetVisible(true);
    }

    public void HidePet()
    {
        settings.Update(s => s with { Pet = s.Pet with { ShowPet = false } });
        SetPetVisible(false);
    }

    private void SetPetVisible(bool visible)
    {
        if (visible && !petWindow.IsVisible)
        {
            petWindow.Show();
        }
        else if (!visible && petWindow.IsVisible)
        {
            petWindow.Hide();
        }
    }
}
