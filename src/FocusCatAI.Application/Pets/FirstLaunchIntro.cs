using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Taskbar;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Taskbar;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Application.Pets;

/// <summary>
/// First launch only: the cat walks onto the taskbar from off screen, finds a place to sit, waits 30-60 seconds,
/// then greets the user once. The flag is persisted, so the greeting is never repeated.
/// </summary>
public sealed class FirstLaunchIntro(
    IPetPreferences preferences,
    IPetWorld world,
    IPetStateMachine stateMachine,
    IPetSpeaker speaker,
    IRandomSource random,
    ILogger<FirstLaunchIntro> logger,
    ISettingsService? settings = null) : ITickable
{
    public const string Greeting = "Hi! I'll keep you company while you work.";

    private enum Phase { Waiting, Entering, Settling, Done }

    private Phase _phase = Phase.Waiting;
    private double _countdown;

    public void Tick(TimeSpan elapsed)
    {
        switch (_phase)
        {
            case Phase.Waiting:
                if (preferences.HasIntroduced || settings?.Current.Pet.EnableIntroAnimation == false)
                {
                    _phase = Phase.Done;
                }
                else if (world.Layout.Width > 0 && world.PetWidth > 0)
                {
                    Enter();
                }

                break;

            case Phase.Entering:
                if (stateMachine.State != PetState.Walking)
                {
                    _countdown = 30 + random.NextDouble() * 30;
                    _phase = Phase.Settling;
                    logger.LogInformation("[Intro] Settled at X={X:F0}; greeting in {Seconds:F0}s", world.PetX, _countdown);
                }

                break;

            case Phase.Settling:
                _countdown -= elapsed.TotalSeconds;
                if (_countdown <= 0)
                {
                    _phase = Phase.Done;
                    speaker.Say(Greeting, SpeechPriority.High);
                    preferences.MarkIntroduced();
                    preferences.Save();
                    logger.LogInformation("[Intro] Greeting delivered");
                }

                break;
        }
    }

    private void Enter()
    {
        var zones = TaskbarGeometry.GetZones(world.Layout, world.PetWidth);
        var spot = TaskbarGeometry.ResolveFavorite(world.Layout, zones, "Start", world.PetWidth)
                   ?? (zones.Count > 0 ? zones[0].Left + zones[0].Width / 2 - world.PetWidth / 2 : (double?)null);

        // Start just off the left edge so the cat walks onto the taskbar.
        world.PetX = world.Layout.Left - world.PetWidth;
        logger.LogInformation("[Intro] First launch: entering from X={From:F0} toward X={To}", world.PetX, spot);
        stateMachine.Play(new BehaviorPlan(PetBehaviorType.Sitting, PetState.Idle, PetActivity.Sit, 180, spot, 1.0, true));
        _phase = Phase.Entering;
    }
}
