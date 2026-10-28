using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Application.Pomodoro;
using FocusCatAI.Domain.Pomodoro;
using Microsoft.Extensions.DependencyInjection;

namespace FocusCatAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(PomodoroSettings.Default);
        services.AddSingleton<IPetWorld, PetWorld>();
        services.AddSingleton<MoodService>();
        services.AddSingleton<IMoodService>(sp => sp.GetRequiredService<MoodService>());
        services.AddSingleton<IBehaviorPlanner, BehaviorPlanner>();
        services.AddSingleton<IPetStateMachine, PetStateMachine>();
        services.AddSingleton<IPetSpeaker, PetSpeaker>();
        services.AddSingleton<PomodoroService>();
        services.AddSingleton<IPomodoroService>(sp => sp.GetRequiredService<PomodoroService>());
        services.AddSingleton<PomodoroPetReactions>();
        services.AddSingleton<ProductivityReactions>();
        services.AddSingleton<FirstLaunchIntro>();

        services.AddSingleton<ITickable>(sp => sp.GetRequiredService<MoodService>());
        services.AddSingleton<ITickable>(sp => sp.GetRequiredService<PomodoroService>());
        services.AddSingleton<ITickable>(sp => sp.GetRequiredService<ProductivityReactions>());
        services.AddSingleton<ITickable>(sp => sp.GetRequiredService<FirstLaunchIntro>());
        services.AddSingleton<ITickable>(sp => sp.GetRequiredService<IPetStateMachine>());
        return services;
    }
}
