using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Infrastructure.Startup;
using FocusCatAI.Presentation.Views;
using FocusCatAI.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Developer diagnostic (<c>WorkBuddy.exe --verify-startup</c>). Exercises startup registration, removal, detection,
/// synchronisation and single-instance signalling against the real registry and a real second process, then writes
/// startup-verification.log into the data folder. Destructive steps use a scratch registry key; the user's real
/// Run entry and settings are restored afterwards.
/// </summary>
public sealed class StartupVerifier(IServiceProvider services)
{
    private const string ScratchRoot = @"Software\WorkBuddy\VerifyStartup";
    private const string ScratchRun = ScratchRoot + @"\Run";
    private const string ScratchApproved = ScratchRoot + @"\Approved";

    private readonly StringBuilder _report = new();
    private readonly Dictionary<string, bool> _groups = new();
    private int _failures;

    private ISettingsService Settings => services.GetRequiredService<ISettingsService>();

    /// <summary>Runs all checks. Returns true when every check passed.</summary>
    public async Task<bool> RunAsync()
    {
        var paths = services.GetRequiredService<IAppPaths>();
        var exe = Environment.ProcessPath!;
        var originalSettings = Settings.Current;
        var realKey = new RegistryRunKeyStore();
        var originalRun = realKey.GetCommand(AppConstants.StartupValueName);

        _report.AppendLine($"WorkBuddy startup verification  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _report.AppendLine($"Executable: {exe}");
        _report.AppendLine(new string('=', 78));

        try
        {
            ScratchRegistry(exe);
            RealRegistry(realKey, exe);
            Synchronization(exe);
            SettingsIntegration();
            await SingleInstance(exe);
        }
        catch (Exception ex)
        {
            Record("Runtime", "Verifier completed without exception", false, ex.ToString());
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(ScratchRoot, throwOnMissingSubKey: false);
            Settings.Update(_ => originalSettings);
            var manager = services.GetRequiredService<IStartupManager>();
            if (originalRun is null)
            {
                manager.Disable();
            }
            else
            {
                realKey.SetCommand(AppConstants.StartupValueName, originalRun);
            }
        }

        Record("Runtime", "User's original Run entry restored", realKey.GetCommand(AppConstants.StartupValueName) == originalRun,
            $"before={originalRun ?? "(none)"}");

        _report.AppendLine(new string('=', 78));
        foreach (var group in _groups)
        {
            _report.AppendLine($"{(group.Value ? "PASS" : "FAIL")}  {group.Key}");
        }

        var ok = _failures == 0;
        _report.AppendLine($"OVERALL: {(ok ? "PASS" : "FAIL")} ({_failures} failed check(s))");
        Directory.CreateDirectory(paths.RootDirectory);
        await File.WriteAllTextAsync(Path.Combine(paths.RootDirectory, "startup-verification.log"), _report.ToString());
        return ok;
    }

    private StartupManager ScratchManager(string exe) =>
        new(new RegistryRunKeyStore(ScratchRun, ScratchApproved), exe, NullLogger<StartupManager>.Instance);

    private static string? ScratchValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ScratchRun);
        return key?.GetValue(AppConstants.StartupValueName) as string;
    }

    private void ScratchRegistry(string exe)
    {
        var manager = ScratchManager(exe);
        var store = new RegistryRunKeyStore(ScratchRun, ScratchApproved);

        Record("Registration", "Starts with no entry (NotRegistered)", manager.GetStatus().State == StartupState.NotRegistered, "");
        manager.Enable();
        var expected = $"\"{exe}\" --autostart";
        Record("Registration", "Enable writes \"<exe>\" --autostart to the Run key", ScratchValue() == expected, ScratchValue() ?? "(missing)");
        Record("Registration", "Status after enable is Registered", manager.GetStatus().State == StartupState.Registered, manager.GetStatus().State.ToString());
        Record("Registration", "Entry created under HKCU (no administrator rights used)", Registry.CurrentUser.OpenSubKey(ScratchRun) is not null, "HKCU");

        manager.Disable();
        Record("Removal", "Disable deletes the Run value", ScratchValue() is null, ScratchValue() ?? "(removed)");
        Record("Removal", "Status after disable is NotRegistered", manager.GetStatus().State == StartupState.NotRegistered, "");
        Record("Removal", "Disable when already removed does not throw", Tries(() => manager.Disable()), "");

        // Detection: each distinct state, written straight into the registry the way another tool or an old build would.
        store.SetCommand(AppConstants.StartupValueName, $"\"{Path.Combine(Path.GetTempPath(), "WorkBuddy-missing", "WorkBuddy.exe")}\" --autostart");
        Record("Detection", "Missing executable -> InvalidPath", manager.GetStatus().State == StartupState.InvalidPath, manager.GetStatus().State.ToString());

        var other = Path.Combine(Path.GetTempPath(), "wb-verify-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(other, "x");
        try
        {
            store.SetCommand(AppConstants.StartupValueName, $"\"{other}\" --autostart");
            Record("Detection", "Different existing executable -> PathMismatch (registry mismatch)", manager.GetStatus().State == StartupState.PathMismatch, manager.GetStatus().State.ToString());
        }
        finally
        {
            File.Delete(other);
        }

        manager.Enable();
        store.SimulateDisabledByWindows(AppConstants.StartupValueName);
        Record("Detection", "Disabled in Windows Startup apps -> DisabledByWindows", manager.GetStatus().State == StartupState.DisabledByWindows, manager.GetStatus().State.ToString());
        Record("Detection", "DisabledByWindows does not claim startup will happen", !manager.GetStatus().WillStartAtSignIn, "");
        manager.Enable();
        Record("Detection", "Enable clears the Windows disabled flag -> Registered", manager.GetStatus().State == StartupState.Registered, manager.GetStatus().State.ToString());
        manager.Disable();
        Record("Detection", "Disabled / absent -> NotRegistered", manager.GetStatus().State == StartupState.NotRegistered, "");
    }

    private void RealRegistry(RegistryRunKeyStore real, string exe)
    {
        var manager = services.GetRequiredService<IStartupManager>();
        manager.Enable();
        var value = real.GetCommand(AppConstants.StartupValueName);
        Record("Registration", "Real HKCU\\...\\Run\\WorkBuddy written for this executable", value == $"\"{exe}\" --autostart", value ?? "(missing)");
        Record("Registration", "Real entry detected as Registered", manager.GetStatus().State == StartupState.Registered, manager.GetStatus().State.ToString());
        manager.Disable();
        Record("Removal", "Real entry removed", real.GetCommand(AppConstants.StartupValueName) is null, "");
    }

    private void Synchronization(string exe)
    {
        var store = new RegistryRunKeyStore(ScratchRun, ScratchApproved);
        var manager = ScratchManager(exe);
        manager.Disable();

        var old = Path.Combine(Path.GetTempPath(), "wb-verify-old-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(old, "x");
        try
        {
            store.SetCommand(AppConstants.StartupValueName, $"\"{old}\" --autostart");
            manager.Synchronize(true);
            Record("Synchronization", "Moved install: stale path replaced with current executable", ScratchValue() == $"\"{exe}\" --autostart", ScratchValue() ?? "(missing)");
        }
        finally
        {
            File.Delete(old);
        }

        store.SetCommand(AppConstants.StartupValueName, $"\"{Path.Combine(Path.GetTempPath(), "gone", "WorkBuddy.exe")}\" --autostart");
        manager.Synchronize(true);
        Record("Synchronization", "Deleted install: invalid path repaired", manager.GetStatus().State == StartupState.Registered, "");

        manager.Disable();
        manager.Synchronize(true);
        Record("Synchronization", "Wanted but missing: entry created", manager.GetStatus().State == StartupState.Registered, "");
        manager.Synchronize(false);
        Record("Synchronization", "Not wanted: entry removed", ScratchValue() is null, "");

        manager.Enable();
        store.SimulateDisabledByWindows(AppConstants.StartupValueName);
        manager.Synchronize(true);
        Record("Synchronization", "User's choice in Windows Startup apps is not overridden", manager.GetStatus().State == StartupState.DisabledByWindows, manager.GetStatus().State.ToString());
        manager.Disable();
    }

    private void SettingsIntegration()
    {
        var vm = new ViewModels.SettingsViewModel(Settings, services.GetRequiredService<IAppPaths>(), services.GetRequiredService<IPetPreferences>(), services.GetRequiredService<IStartupManager>());
        var real = new RegistryRunKeyStore();

        vm.StartWithWindows = true;
        Record("Settings", "Checkbox on: setting saved and Run key written", Settings.Current.General.StartWithWindows && real.GetCommand(AppConstants.StartupValueName) is not null, "");
        Record("Settings", "Checkbox reads true while registered", vm.StartWithWindows, "");
        vm.StartWithWindows = false;
        Record("Settings", "Checkbox off: setting saved and Run key removed", !Settings.Current.General.StartWithWindows && real.GetCommand(AppConstants.StartupValueName) is null, "");
        Record("Settings", "Checkbox reads false while not registered", !vm.StartWithWindows, "");

        // The checkbox must never lie: change the registry behind the settings file's back.
        Settings.Update(s => s with { General = s.General with { StartWithWindows = true } });
        real.RemoveCommand(AppConstants.StartupValueName);
        Record("Settings", "Setting says on but registry empty: checkbox shows off (truth)", !vm.StartWithWindows, "");
        services.GetRequiredService<IStartupManager>().Synchronize(Settings.Current.General.StartWithWindows);
        Record("Settings", "Synchronize at launch repairs the registry to match the setting", real.GetCommand(AppConstants.StartupValueName) is not null && vm.StartWithWindows, "");
        vm.StartWithWindows = false;

        var args = StartupOptions.Parse(["--autostart", "--verify-startup"]);
        Record("Autostart", "--autostart overrides diagnostics flags", args.Autostart && !args.VerifyStartup && !args.VerifySettings, "");
        Record("Autostart", "Settings window is not open in this process before signalling", System.Windows.Application.Current.Windows.OfType<SettingsWindow>().All(w => !w.IsVisible), "");
    }

    private async Task SingleInstance(string exe)
    {
        var settingsApplier = services.GetRequiredService<SettingsApplier>();
        var pet = services.GetRequiredService<PetWindow>();

        // Second plain launch while the pet is hidden: must show the pet, open Settings, and exit.
        settingsApplier.HidePet();
        await Task.Delay(300);
        var hidden = !pet.IsVisible;
        var before = WorkBuddyProcessCount();
        var exitCode = RunSecond(exe, null, 15000, out var exited);
        await WaitUntil(() => pet.IsVisible, 3000);
        Record("Single Instance", "Pet was hidden before second launch", hidden, "");
        Record("Single Instance", "Second launch exits by itself", exited, $"exitCode={exitCode}");
        Record("Single Instance", "Second launch brings the hidden pet back", pet.IsVisible, "");
        var opened = await WaitUntil(() => System.Windows.Application.Current.Windows.OfType<SettingsWindow>().Any(w => w.IsVisible), 3000);
        Record("Single Instance", "Second launch brings Settings forward in the first instance", opened, "");
        Record("Single Instance", "Still exactly one WorkBuddy process (no duplicate)", WorkBuddyProcessCount() == before, $"before={before} after={WorkBuddyProcessCount()}");

        foreach (var w in System.Windows.Application.Current.Windows.OfType<SettingsWindow>().ToList())
        {
            w.Close();
        }

        // A duplicate --autostart (Run key plus manual start) must stay silent.
        RunSecond(exe, AppConstants.AutostartArgument, 15000, out var autostartExited);
        await Task.Delay(500);
        Record("Single Instance", "Duplicate --autostart exits silently", autostartExited, "");
        Record("Single Instance", "Duplicate --autostart opens no window", System.Windows.Application.Current.Windows.OfType<SettingsWindow>().All(w => !w.IsVisible), "");
    }

    private static int RunSecond(string exe, string? argument, int timeoutMs, out bool exited)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false };
        if (argument is not null)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        exited = process.WaitForExit(timeoutMs);
        if (!exited)
        {
            process.Kill();
            return -1;
        }

        return process.ExitCode;
    }

    private static int WorkBuddyProcessCount() => Process.GetProcessesByName("WorkBuddy").Length;

    private static bool Tries(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var until = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < until)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return condition();
    }

    private void Record(string group, string check, bool pass, string detail)
    {
        if (!pass)
        {
            _failures++;
        }

        _groups[group] = _groups.GetValueOrDefault(group, true) && pass;
        _report.AppendLine($"[{(pass ? "PASS" : "FAIL")}] {group,-16} {check}{(string.IsNullOrEmpty(detail) ? "" : "  -> " + detail)}");
    }
}
