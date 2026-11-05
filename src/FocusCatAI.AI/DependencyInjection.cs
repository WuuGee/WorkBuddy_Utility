using FocusCatAI.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FocusCatAI.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddFocusCatAi(this IServiceCollection services)
    {
        services.AddSingleton<IAiHardwareProbe, WindowsAiHardwareProbe>();
        services.AddSingleton<ILocalModelScanner, FileSystemLocalModelScanner>();
        services.AddSingleton<IAiCompanionService, LocalAiCompanionService>();
        return services;
    }
}
