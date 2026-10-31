using System.Configuration;
using System.Data;
using System.Windows;
using FocusCatAI.Application;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pomodoro;
using FocusCatAI.Infrastructure;
using FocusCatAI.Monitoring;
using FocusCatAI.Presentation.Animation;
using FocusCatAI.Presentation.Configuration;
using FocusCatAI.Presentation.Services;
using FocusCatAI.Presentation.ViewModels;
using FocusCatAI.Presentation.Views;
using FocusCatAI.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FocusCatAI.Presentation;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private Mutex? _singleInstance;
    private SingleInstanceChannel? _channel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var launch = StartupOptions.Parse(e.Args);

        if (launch.Quit)
        {
            // Used by the installer and uninstaller. Exit code 0 whether or not an instance was running.
            SingleInstanceChannel.TrySignalExisting(SingleInstanceChannel.QuitCommand, TimeSpan.FromSeconds(3));
            Shutdown(0);
            return;
        }

        if (launch.VerifyRelease)
        {
            var releaseOk = await Task.Run(() => new ReleaseVerifier().Run());
            Shutdown(releaseOk ? 0 : 1);
            return;
        }

        if (launch.VerifyInstaller)
        {
            // Headless and before the single-instance mutex: the installed copy being tested must be able to start.
            var ok = await Task.Run(() => new InstallerVerifier().Run());
            Shutdown(ok ? 0 : 1);
            return;
        }

        if (launch.RestartAfterProcessId is { } previous)
        {
            WaitForProcessExit(previous, TimeSpan.FromSeconds(15));
        }

        _singleInstance = new Mutex(true, AppConstants.SingleInstanceMutexName, out var isFirst);
        if (!isFirst)
        {
            // A duplicate autostart (Run key plus a manual launch) exits quietly; a manual launch brings the first instance forward.
            if (!launch.Autostart)
            {
                SingleInstanceChannel.TrySignalExisting(SingleInstanceChannel.ActivateCommand, TimeSpan.FromSeconds(3));
            }

            Shutdown();
            return;
        }

        try
        {
            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var migrated = FocusCatAI.Infrastructure.LegacyDataMigration.Run(
                System.IO.Path.Combine(localData, AppConstants.LegacyAppName),
                System.IO.Path.Combine(localData, AppConstants.AppName));

            _host = BuildHost();
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("{Product} starting (legacy files migrated: {Migrated}, autostart: {Autostart})", AppConstants.ProductName, migrated, launch.Autostart);

            DispatcherUnhandledException += (_, args) =>
            {
                logger.LogError(args.Exception, "Unhandled UI exception");
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                logger.LogCritical(args.ExceptionObject as Exception, "Unhandled domain exception");
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                logger.LogError(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };

            await _host.StartAsync();
            _host.Services.GetRequiredService<ThemeApplier>().Start();
            _host.Services.GetRequiredService<PomodoroPetReactions>();
            var petOptions = _host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PetOptions>>().Value;
            if (petOptions.DebugTestMode > 0)
            {
                var layout = _host.Services.GetRequiredService<ITaskbarLayoutProvider>().Current;
                var calc = FocusCatAI.Application.Taskbar.TaskbarFit.ComputeBounds(layout, 64, layout.Left + layout.Width / 2);
                logger.LogWarning("DebugTestMode {Mode}: forced window at (500,500) 100x100. Taskbar calc would place pet at L={L:F0} T={T:F0} R={R:F0} B={B:F0}; taskbar=({TL:F0},{TT:F0},{TR:F0},{TB:F0}) mode={PM}",
                    petOptions.DebugTestMode, calc.Left, calc.Top, calc.Right, calc.Bottom, layout.Left, layout.Top, layout.Right, layout.Bottom, layout.Mode);
                var test = new DebugTestWindow(petOptions.DebugTestMode, _host.Services.GetRequiredService<ISpriteManager>(), petOptions.PetId);
                test.Show();
                logger.LogWarning("DebugTestWindow shown: visible={Visible} actual={W:F0}x{H:F0} at ({L:F0},{T:F0})", test.IsVisible, test.ActualWidth, test.ActualHeight, test.Left, test.Top);
                _host.Services.GetRequiredService<TrayIconService>().Show();
                return;
            }

            _host.Services.GetRequiredService<SettingsApplier>()
                .Start(_host.Services.GetRequiredService<ISettingsService>().Current.General.StartMinimized);
            _host.Services.GetRequiredService<TrayIconService>().Show();

            _channel = _host.Services.GetRequiredService<SingleInstanceChannel>();
            _channel.StartListening(OnSecondLaunch);

            // Normal launches only (not diagnostics): one quiet, non-blocking check that is silent unless a newer release exists.
            if (!launch.VerifySettings && !launch.VerifyStartup && launch.TrayPhase == 0)
            {
                _ = _host.Services.GetRequiredService<TrayIconService>().CheckForUpdatesQuietlyAsync();
            }

            if (launch.VerifySettings)
            {
                var passed = await new SettingsVerifier(_host.Services).RunAsync();
                logger.LogInformation("Settings verification finished: {Result}", passed ? "PASS" : "FAIL");
                Shutdown(passed ? 0 : 1);
            }
            else if (launch.TrayPhase == 1)
            {
                // Phase 1 ends with a real restart; this process exits through it. Only exit here if the restart failed.
                var restarted = await new TrayVerifier(_host.Services).RunPhase1Async();
                if (!restarted)
                {
                    Shutdown(1);
                }
            }
            else if (launch.TrayPhase == 2)
            {
                // Phase 2 ends by clicking the real Exit item, so nothing may follow it.
                await new TrayVerifier(_host.Services).RunPhase2Async(launch.RestartAfterProcessId);
            }
            else if (launch.TrayPhase == 3)
            {
                var passed = await new TrayVerifier(_host.Services).RunPhase3Async(launch.RestartAfterProcessId);
                logger.LogInformation("Tray verification finished: {Result}", passed ? "PASS" : "FAIL");
                Shutdown(passed ? 0 : 1);
            }
            else if (launch.VerifyStartup)
            {
                var passed = await new StartupVerifier(_host.Services).RunAsync();
                logger.LogInformation("Startup verification finished: {Result}", passed ? "PASS" : "FAIL");
                Shutdown(passed ? 0 : 1);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            MessageBox.Show(ex.Message, $"{AppConstants.ProductName} failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void WaitForProcessExit(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            process.WaitForExit(timeout);
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }

    private void OnSecondLaunch(string command) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (command == SingleInstanceChannel.QuitCommand)
            {
                Shutdown();
                return;
            }

            _host?.Services.GetService<SettingsApplier>()?.ShowPet();
            if (command == SingleInstanceChannel.ActivateCommand)
            {
                _host?.Services.GetService<TrayIconService>()?.OpenSettings();
            }
        });

    // Synchronous on purpose: an async void OnExit returns at its first await, so the process could end before the host
    // stopped and before the log was flushed.
    protected override void OnExit(ExitEventArgs e)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (_host is not null)
        {
            var logger = _host.Services.GetService<ILogger<App>>();
            logger?.LogInformation("{Product} shutting down", AppConstants.ProductName);
            _channel?.Dispose();
            _host.Services.GetService<IPetPreferences>()?.Save();
            _host.Services.GetService<TrayIconService>()?.Dispose();

            // Settings are written on every change (JsonSettingsService), so there is nothing further to flush.
            // StopAsync runs on a pool thread so it can never wait on the UI thread that is blocked here.
            try
            {
                var stopped = Task.Run(() => _host.StopAsync(TimeSpan.FromSeconds(3))).Wait(TimeSpan.FromSeconds(5));
                logger?.LogInformation("Host stopped={Stopped} after {Elapsed} ms", stopped, stopwatch.ElapsedMilliseconds);
            }
            catch (AggregateException ex)
            {
                logger?.LogError(ex.Flatten(), "Host did not stop cleanly");
            }

            // Logged before Dispose: disposing the host also disposes the Serilog logger, which flushes the file sink.
            logger?.LogInformation("{Product} shutdown complete in {Elapsed} ms", AppConstants.ProductName, stopwatch.ElapsedMilliseconds);
            _host.Dispose();
        }

        Log.CloseAndFlush();
        try
        {
            _singleInstance?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread (second instance); nothing to release.
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseSerilog((context, services, config) =>
            {
                var paths = new AppPaths();
                config
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                    .WriteTo.File(
                        System.IO.Path.Combine(paths.LogsDirectory, "workbuddy-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<PetOptions>(context.Configuration.GetSection("Pet"));
                services.AddSingleton(context.Configuration.GetSection("Monitoring").Get<MonitoringOptions>() ?? new MonitoringOptions());

                services.AddApplication();
                services.AddInfrastructure();
                services.AddMonitoring();

                services.AddSingleton<ISpriteManager, SpriteManager>();
                services.AddSingleton<IAnimationCatalog, ActivePetCatalog>();
                services.AddSingleton<TaskbarLayoutService>();
                services.AddSingleton<ITaskbarLayoutProvider>(sp => sp.GetRequiredService<TaskbarLayoutService>());
                services.AddHostedService(sp => sp.GetRequiredService<TaskbarLayoutService>());
                services.AddSingleton<ISpeechPresenter, SpeechBubblePresenter>();
                services.AddSingleton<PetViewModel>();
                services.AddSingleton<PetWindow>();
                services.AddSingleton<SettingsApplier>();
                services.AddSingleton<ThemeApplier>();
                services.AddSingleton<AppLifecycle>();
                services.AddTransient<AboutWindow>();
                services.AddSingleton<FocusCatAI.Application.Updates.IUpdateChecker>(sp =>
                    new FocusCatAI.Infrastructure.Updates.GitHubUpdateChecker(
                        new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) },
                        sp.GetRequiredService<ILogger<FocusCatAI.Infrastructure.Updates.GitHubUpdateChecker>>()));
                services.AddSingleton<SingleInstanceChannel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsWindow>();
                services.AddSingleton<TrayIconService>();
            })
            .Build();
}

