using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Pomodoro;

namespace FocusCatAI.Application.Pomodoro;

/// <summary>Translates focus-session events into pet behavior, mood and the occasional short message.</summary>
public sealed class PomodoroPetReactions
{
    private readonly IPomodoroService _pomodoro;
    private readonly IMoodService _mood;
    private readonly IPetStateMachine _pet;
    private readonly IPetSpeaker _speaker;
    private readonly PomodoroSettings _settings;
    private readonly ISettingsService? _appSettings;

    public PomodoroPetReactions(
        IPomodoroService pomodoro,
        IMoodService mood,
        IPetStateMachine pet,
        IPetSpeaker speaker,
        PomodoroSettings settings,
        ISettingsService? appSettings = null)
    {
        _appSettings = appSettings;
        _pomodoro = pomodoro;
        _mood = mood;
        _pet = pet;
        _speaker = speaker;
        _settings = settings;
        pomodoro.EventRaised += OnEvent;
    }

    private void Say(string text) => Say(text, SpeechPriority.High);

    private void Say(string text, SpeechPriority priority)
    {
        if (_appSettings?.Current.Notifications.EnablePomodoroMessages != false)
        {
            _speaker.Say(text, priority);
        }
    }

    private void OnEvent(object? sender, PomodoroEvent e)
    {
        switch (e.Kind)
        {
            case PomodoroEventKind.WorkStarted:
                _mood.Set(PetMood.Focused);
                _pet.Trigger(PetTrigger.SitDown);
                Say("Let's focus together.");
                break;
            case PomodoroEventKind.Resumed when _pomodoro.Phase == PomodoroPhase.Work:
                _mood.Set(PetMood.Focused);
                break;
            case PomodoroEventKind.Milestone:
                var message = e.Minutes switch
                {
                    15 => "Nice start.",
                    30 => "You're doing well.",
                    _ => null
                };
                if (message is not null)
                {
                    Say(message);
                }

                break;
            case PomodoroEventKind.WorkCompleted:
                _pet.Trigger(PetTrigger.Celebrate);
                Say("Focus session complete.");
                break;
            case PomodoroEventKind.BreakStarted:
                _mood.Set(PetMood.Happy, _settings.BreakDuration);
                break;
            case PomodoroEventKind.BreakCompleted:
                _mood.Set(PetMood.Relaxed);
                Say("Break's over. Ready when you are.");
                break;
            case PomodoroEventKind.Paused:
            case PomodoroEventKind.Stopped:
                _mood.Set(PetMood.Relaxed);
                break;
        }
    }
}
