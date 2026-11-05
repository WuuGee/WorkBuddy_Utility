using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.AI;

namespace FocusCatAI.AI;

/// <summary>Short in-character replies. Picks the device and reports a local model if installed; text is template-based for now.</summary>
public sealed class LocalAiCompanionService(
    IAiHardwareProbe probe,
    ILocalModelScanner scanner,
    ISettingsService settings) : IAiCompanionService
{
    public const int MaxLength = 110;

    private readonly Lazy<AiHardwareCapabilities> _hardware = new(() => SafeDetect(probe));

    public AiHardwareCapabilities Hardware => _hardware.Value;

    public CompanionReply GenerateReply(CompanionPromptContext context)
    {
        var preference = settings.Current.Ai?.Device ?? AiDevicePreference.Auto;
        var device = AiDeviceResolver.Resolve(preference, Hardware);

        string model = string.Empty;
        try
        {
            model = scanner.ListInstalledModels().FirstOrDefault() ?? string.Empty;
        }
        catch (Exception)
        {
        }

        var text = Compose(context);
        return new CompanionReply(text, device, model.Length > 0, model);
    }

    private static AiHardwareCapabilities SafeDetect(IAiHardwareProbe probe)
    {
        try
        {
            return probe.Detect();
        }
        catch (Exception)
        {
            return new AiHardwareCapabilities(true, false, false, "CPU");
        }
    }

    private enum Intent { Debugging, Tired, Greeting, Focus, Motivation, General }

    private static Intent Classify(string prompt)
    {
        var p = (prompt ?? string.Empty).ToLowerInvariant();
        if (ContainsAny(p, "bug", "debug", "code", "error", "compile", "exception", "crash")) return Intent.Debugging;
        if (ContainsAny(p, "tired", "sleepy", "exhaust", "break", "rest", "burnt", "burned")) return Intent.Tired;
        if (ContainsAny(p, "hello", "hi", "hey", "good morning", "good evening")
            && p.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4) return Intent.Greeting;
        if (ContainsAny(p, "focus", "pomodoro", "concentrate", "distract")) return Intent.Focus;
        if (ContainsAny(p, "motivat", "stuck", "can't", "cannot", "give up", "encourage")) return Intent.Motivation;
        return Intent.General;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (n.Length <= 2)
            {
                if (text.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Contains(n)) return true;
            }
            else if (text.Contains(n, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Compose(CompanionPromptContext c)
    {
        var intent = Classify(c.Prompt);
        var pet = (c.PetId ?? string.Empty).ToLowerInvariant();
        var hour = Math.Clamp(c.HourOfDay, 0, 23);
        var late = hour >= 22 || hour < 5;
        var morning = hour >= 5 && hour < 12;

        string text = pet switch
        {
            "tuxedo" => intent switch
            {
                Intent.Debugging => "Elementary. Check the last change, then the logs. The bug can't hide from a tuxedo.",
                Intent.Tired => late ? "Even gentlemen retire. Close the laptop and rest, the code keeps." : "A dapper break: stretch, sip some water, return refreshed.",
                Intent.Greeting => morning ? "Good morning. Shall we make today rather splendid?" : "Ah, hello. Impeccable timing as always.",
                Intent.Focus => "One task, one cup of tea, no distractions. Very civilised.",
                Intent.Motivation => "Stuck? Charming. Every great plot has a tricky middle. Carry on.",
                _ => "Quite so. I'm listening, with my best whiskers forward."
            },
            "ginger" => intent switch
            {
                Intent.Debugging => "Bug hunt! Let's pounce on it, one log line at a time!",
                Intent.Tired => late ? "Whoa, it's late! Nap time, champ. Tomorrow we zoom!" : "Quick break! Stretch, snack, then back with big energy!",
                Intent.Greeting => morning ? "Good morning, sunshine! Ready to do great things?" : "Hey hey! So happy you're here!",
                Intent.Focus => "Focus mode on! 25 minutes of awesome, you've got this!",
                Intent.Motivation => "You can do it! Small step, big win. I'm cheering loudly!",
                _ => "Ooh, tell me more! I'm all ears and very excited!"
            },
            _ => intent switch
            {
                Intent.Debugging => "Take a breath. Bugs are shy. Let's look gently at the last change together.",
                Intent.Tired => late ? "It's late, friend. Let's curl up and rest, the code can wait." : "A cozy break sounds right. Stretch and sip something warm.",
                Intent.Greeting => morning ? "Good morning. I saved you a warm spot. Ready when you are." : "Hello, friend. I'm so happy you're here.",
                Intent.Focus => "Settle in, nice and cozy. One gentle task at a time.",
                Intent.Motivation => "You're doing better than you think. One small step is enough.",
                _ => "I'm here, purring beside you. Tell me what's on your mind."
            }
        };

        return text.Length <= MaxLength ? text : text[..(MaxLength - 1)].TrimEnd() + "…";
    }
}
