using FocusCatAI.Application.Abstractions;
using FocusCatAI.Infrastructure.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IRandomSource, SystemRandomSource>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPetPreferences, JsonPetPreferences>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IStartupManager>(sp =>
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path.");
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Startup registration is only available on Windows.");
            }

            return new StartupManager(new RegistryRunKeyStore(), exe, sp.GetRequiredService<ILogger<StartupManager>>());
        });
        return services;
    }
}
