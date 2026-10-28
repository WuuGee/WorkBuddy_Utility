using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.Pets;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Application.Pets;

/// <summary>Rate-limited speech so the pet never becomes annoying.</summary>
public sealed class PetSpeaker(
    ISpeechPresenter presenter,
    IPetStateMachine stateMachine,
    IClock clock,
    ILogger<PetSpeaker> logger,
    ISettingsService? settings = null) : IPetSpeaker
{
    private static readonly TimeSpan NormalCooldown = TimeSpan.FromMinutes(15);

    // Lets the pet establish its presence through movement and animation before it ever speaks.
    private static readonly TimeSpan StartupQuiet = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan HighCooldown = TimeSpan.FromSeconds(4);

    private readonly DateTimeOffset _startedAt = clock.UtcNow;
    private DateTimeOffset _lastSpoke = DateTimeOffset.MinValue;

    public void Say(string text, SpeechPriority priority = SpeechPriority.Normal)
    {
        var now = clock.UtcNow;
        if (settings is not null)
        {
            var current = settings.Current;
            if (!current.Behavior.EnableMessages)
            {
                logger.LogDebug("Suppressed message (messages disabled): {Text}", text);
                return;
            }

            if (QuietHours.IsQuiet(current.QuietHours, TimeOnly.FromDateTime(now.ToLocalTime().DateTime)))
            {
                logger.LogDebug("Suppressed message (quiet hours): {Text}", text);
                return;
            }
        }

        if (priority == SpeechPriority.Normal && now - _startedAt < StartupQuiet)
        {
            logger.LogDebug("Suppressed message (startup quiet period): {Text}", text);
            return;
        }

        var cooldown = priority == SpeechPriority.High ? HighCooldown : NormalCooldownFor(settings?.Current.Behavior.MessageFrequency ?? MessageFrequency.Normal);
        if (now - _lastSpoke < cooldown)
        {
            logger.LogDebug("Suppressed message (cooldown): {Text}", text);
            return;
        }

        _lastSpoke = now;
        var seconds = Math.Clamp(2 + text.Length * 0.06, 3, 8);
        presenter.Show(text, TimeSpan.FromSeconds(seconds));
        stateMachine.Trigger(PetTrigger.Speak);
    }

    private static TimeSpan NormalCooldownFor(MessageFrequency frequency) => frequency switch
    {
        MessageFrequency.Low => TimeSpan.FromMinutes(30),
        MessageFrequency.High => TimeSpan.FromMinutes(5),
        _ => NormalCooldown
    };
}
