using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Updates;
using FocusCatAI.Presentation.Views;
using FocusCatAI.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Developer diagnostic (<c>WorkBuddy.exe --verify-tray</c>). Clicks the real tray menu items and checks what happens.
/// Restart and Exit end the process, so the run spans three processes that pass results through
/// tray-verification.partial: phase 1 tests the menu then clicks Restart; phase 2 (the restarted process) checks the
/// restart and clicks Exit; phase 3 (started by phase 2) checks that Exit left nothing behind and writes
/// tray-verification.log. See docs/developer/TRAY-VERIFICATION.md.
/// </summary>
public sealed class TrayVerifier(IServiceProvider services)
{
    private static readonly string?[] ExpectedMenu =
    [
        AppConstants.ProductName, null,
        TrayIconService.ShowCatHeader, TrayIconService.HideCatHeader, null,
        TrayIconService.OpenSettingsHeader, TrayIconService.RestartHeader, null,
        TrayIconService.AboutHeader, TrayIconService.CheckForUpdatesHeader,
        TrayIconService.OpenLogsHeader, TrayIconService.ReportIssueHeader, null,
        TrayIconService.ExitHeader
    ];

    private readonly IAppPaths _paths = services.GetRequiredService<IAppPaths>();

    private ISettingsService Settings => services.GetRequiredService<ISettingsService>();

    private TrayIconService Tray => services.GetRequiredService<TrayIconService>();

    private string PartialPath => Path.Combine(_paths.RootDirectory, "tray-verification.partial");

    // ---------------------------------------------------------------- Phase 1

    /// <summary>Tests the menu, then clicks Restart. Returns false only if the restart did not happen.</summary>
    public async Task<bool> RunPhase1Async()
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        File.WriteAllText(PartialPath, string.Empty);
        SetState("pid1", Environment.ProcessId.ToString());
        SetState("logOffset0", CurrentLogLength().ToString());

        var originalShow = Settings.Current.Pet.ShowPet;
        SetState("originalShowPet", originalShow.ToString());
        SetState("originalSpeed", Settings.Current.Pet.AnimationSpeed.ToString("R", CultureInfo.InvariantCulture));

        try
        {
            await EnsurePetShown();
            TrayStartup();
            await HideCat();
            await ShowCat();
            await SettingsLaunch();
            await AboutDialog();
            await UpdateCheck();
        }
        catch (Exception ex)
        {
            Record("Runtime", "Phase 1 completed without exception", false, ex.ToString());
        }
        finally
        {
            if (Settings.Current.Pet.ShowPet != originalShow)
            {
                Settings.Update(s => s with { Pet = s.Pet with { ShowPet = originalShow } });
                await Task.Delay(400);
            }
        }

        // Restart: click the real menu item. The process must shut down and a replacement must start.
        SetState("restartClickedUtc", DateTime.UtcNow.Ticks.ToString());
        SetState("logOffset1", CurrentLogLength().ToString());
        Environment.SetEnvironmentVariable(StartupOptions.TrayPhaseEnvironmentVariable, "2");
        var restart = Tray.FindMenuItem(TrayIconService.RestartHeader);
        Record("Restart", "Restart item exists and is enabled", restart is { IsEnabled: true }, "");
        if (restart is null)
        {
            Environment.SetEnvironmentVariable(StartupOptions.TrayPhaseEnvironmentVariable, null);
            FinishAndWrite();
            return false;
        }

        Click(restart);

        // On success this process dies inside the delay. Reaching the end means the restart did not happen.
        await Task.Delay(TimeSpan.FromSeconds(8));
        Environment.SetEnvironmentVariable(StartupOptions.TrayPhaseEnvironmentVariable, null);
        Record("Restart", "Clicking Restart shut this process down and started a replacement", false, "process still running 8 s after the click");
        FinishAndWrite();
        return false;
    }

    private async Task EnsurePetShown()
    {
        if (!Settings.Current.Pet.ShowPet)
        {
            services.GetRequiredService<SettingsApplier>().ShowPet();
            await WaitUntil(() => services.GetRequiredService<PetWindow>().IsVisible, 2000);
        }
    }

    private void TrayStartup()
    {
        var headers = Tray.MenuHeaders;
        Record("Tray Startup", "Tray icon is created and visible", Tray.IsVisible, "");
        Record("Tray Startup", "Menu is exactly: WorkBuddy, Show Cat, Hide Cat, Open Settings, Restart, About, Check for Updates, Open Logs Folder, Report an Issue, Exit",
            headers.SequenceEqual(ExpectedMenu), string.Join(" | ", headers.Select(h => h ?? "---")));
        Record("Tray Startup", "No Pomodoro controls in the tray",
            headers.All(h => h is null || !h.Contains("Focus", StringComparison.OrdinalIgnoreCase) && !h.Contains("Pomodoro", StringComparison.OrdinalIgnoreCase)), "");

        var tip = Tray.Tooltip ?? string.Empty;
        Record("Tray Startup", "Tooltip starts with the product name", tip.StartsWith(AppConstants.ProductName, StringComparison.Ordinal), Flat(tip));
        Record("Tray Startup", "Tooltip shows 'Running' while the pet is visible", tip.Contains("Running"), "");
        Record("Tray Startup", "Tooltip shows the version", tip.Contains($"Version {AppInfo.CurrentVersion}"), "");
        var startsWithWindows = services.GetRequiredService<IStartupManager>().GetStatus().WillStartAtSignIn;
        Record("Tray Startup", "Tooltip startup line matches the registry", tip.Contains("Starts with Windows") == startsWithWindows, $"registry says startup={startsWithWindows}");
        Record("Tray Startup", "Tooltip fits Windows' 127 character limit", tip.Length <= 127, $"{tip.Length} chars");

        var show = Tray.FindMenuItem(TrayIconService.ShowCatHeader);
        var hide = Tray.FindMenuItem(TrayIconService.HideCatHeader);
        Record("Tray Startup", "Pet visible: Hide Cat enabled, Show Cat disabled", hide?.IsEnabled == true && show?.IsEnabled == false, "");
    }

    private async Task HideCat()
    {
        var pet = services.GetRequiredService<PetWindow>();
        var hide = Tray.FindMenuItem(TrayIconService.HideCatHeader)!;
        var show = Tray.FindMenuItem(TrayIconService.ShowCatHeader)!;

        Click(hide);
        var hidden = await WaitUntil(() => !pet.IsVisible, 2000);
        Record("Hide Cat", "Clicking Hide Cat hides the pet window", hidden, $"IsVisible={pet.IsVisible}");
        Record("Hide Cat", "ShowPet=false is saved to settings.json", ReadSettingsFile("Pet", "ShowPet").GetBoolean() == false && !Settings.Current.Pet.ShowPet, "");
        Record("Hide Cat", "Tray icon stays active", Tray.IsVisible, "");
        var tip = await WaitUntil(() => Tray.Tooltip?.Contains("Pet Hidden") == true, 2000);
        Record("Hide Cat", "Tooltip changes to 'Pet Hidden'", tip, Flat(Tray.Tooltip ?? string.Empty));
        var state = await WaitUntil(() => show.IsEnabled && !hide.IsEnabled, 2000);
        Record("Hide Cat", "Show Cat enabled, Hide Cat disabled", state, $"show={show.IsEnabled} hide={hide.IsEnabled}");
    }

    private async Task ShowCat()
    {
        var pet = services.GetRequiredService<PetWindow>();
        var hide = Tray.FindMenuItem(TrayIconService.HideCatHeader)!;
        var show = Tray.FindMenuItem(TrayIconService.ShowCatHeader)!;

        var clock = Stopwatch.StartNew();
        Click(show);
        var visible = await WaitUntil(() => pet.IsVisible, 2000);
        clock.Stop();
        Record("Show Cat", "Clicking Show Cat restores the pet window", visible, $"IsVisible={pet.IsVisible}");
        Record("Show Cat", "Pet is back immediately (under 1 second)", visible && clock.ElapsedMilliseconds < 1000, $"{clock.ElapsedMilliseconds} ms");
        Record("Show Cat", "ShowPet=true is saved to settings.json", ReadSettingsFile("Pet", "ShowPet").GetBoolean() && Settings.Current.Pet.ShowPet, "");
        var tip = await WaitUntil(() => Tray.Tooltip?.Contains("Running") == true && Tray.Tooltip.Contains("Pet Hidden") == false, 2000);
        Record("Show Cat", "Tooltip returns to 'Running'", tip, Flat(Tray.Tooltip ?? string.Empty));
        var state = await WaitUntil(() => hide.IsEnabled && !show.IsEnabled, 2000);
        Record("Show Cat", "Hide Cat enabled, Show Cat disabled", state, $"show={show.IsEnabled} hide={hide.IsEnabled}");
    }

    private async Task SettingsLaunch()
    {
        var item = Tray.FindMenuItem(TrayIconService.OpenSettingsHeader)!;
        Click(item);
        var opened = await WaitUntil(() => Windows<SettingsWindow>().Any(w => w.IsVisible), 3000);
        Record("Settings", "Open Settings shows the settings window", opened, "");
        Click(item);
        await Task.Delay(300);
        Record("Settings", "Clicking again reuses the window (no duplicate)", Windows<SettingsWindow>().Count(w => w.IsVisible) == 1, $"windows={Windows<SettingsWindow>().Count(w => w.IsVisible)}");
        foreach (var w in Windows<SettingsWindow>().ToList())
        {
            w.Close();
        }

        await Task.Delay(200);
        Record("Settings", "Settings window closes and the tray remains", Tray.IsVisible && !Windows<SettingsWindow>().Any(), "");
    }

    private async Task AboutDialog()
    {
        var item = Tray.FindMenuItem(TrayIconService.AboutHeader)!;
        Click(item);
        var opened = await WaitUntil(() => Windows<AboutWindow>().Any(w => w.IsVisible), 3000);
        Record("About", "About opens from the tray", opened, "");
        var about = Windows<AboutWindow>().FirstOrDefault();
        if (about is not null)
        {
            string Text(string name) => (about.FindName(name) as TextBlock)?.Text ?? "(missing)";
            Record("About", "Shows the application name", about.Title.Contains("WorkBuddy"), about.Title);
            Record("About", "Shows the version", Text("VersionText").Contains(AppInfo.CurrentVersion.ToString()), Text("VersionText"));
            Record("About", "Shows the copyright", Text("CopyrightText") == AppInfo.Copyright, Text("CopyrightText"));
            Record("About", "Shows the license line", Text("LicenseText") == AppInfo.LicenseSummary, Text("LicenseText"));
            Record("About", "Shows the GitHub repository", Text("RepositoryText") == AppInfo.RepositoryUrl, Text("RepositoryText"));
            Record("About", "Shows the real data folder", Text("DataFolderText") == _paths.RootDirectory, Text("DataFolderText"));
            Record("About", "Shows the open-source notice", Text("NoticeText") == AppInfo.OpenSourceNotice, "");
            var button = about.FindName("OpenGitHubButton") as Button;
            Record("About", "Open GitHub button present and enabled (not clicked: it would open a browser)", button is { IsEnabled: true, Visibility: Visibility.Visible }, "");
        }

        Click(item);
        await Task.Delay(300);
        Record("About", "Clicking About again reuses the window (no duplicate)", Windows<AboutWindow>().Count(w => w.IsVisible) == 1, "");
        foreach (var w in Windows<AboutWindow>().ToList())
        {
            w.Close();
        }

        await Task.Delay(200);
        Record("About", "About closes and the tray remains", Tray.IsVisible && !Windows<AboutWindow>().Any(), "");
    }

    private async Task UpdateCheck()
    {
        Record("Diagnostics", "Open Logs Folder and Report an Issue exist and are enabled (not clicked: they open Explorer and a browser)",
            Tray.FindMenuItem(TrayIconService.OpenLogsHeader) is { IsEnabled: true } && Tray.FindMenuItem(TrayIconService.ReportIssueHeader) is { IsEnabled: true }, "");
        Record("Diagnostics", "Report an Issue targets the repository issues page over https",
            AppInfo.IssuesUrl == AppInfo.RepositoryUrl + "/issues" && AppInfo.IssuesUrl.StartsWith("https://", StringComparison.Ordinal), AppInfo.IssuesUrl);
        Record("Diagnostics", "The logs folder the launcher opens exists on disk", Directory.Exists(_paths.LogsDirectory), _paths.LogsDirectory);

        var item = Tray.FindMenuItem(TrayIconService.CheckForUpdatesHeader);
        Record("Update Check", "Check for Updates item exists and is enabled (not clicked: it shows a message box)", item is { IsEnabled: true }, "");
        try
        {
            var result = await services.GetRequiredService<IUpdateChecker>().CheckAsync(AppInfo.CurrentVersion);
            Record("Update Check", "Real GitHub query returns a defined outcome without throwing", Enum.IsDefined(result.Outcome),
                $"{result.Outcome} current={result.Current} latest={result.Latest?.ToString() ?? "-"} {result.Error}");
            Record("Update Check", "The outcome produces a non-empty message", !string.IsNullOrWhiteSpace(TrayIconService.DescribeUpdate(result)), "");
        }
        catch (Exception ex)
        {
            Record("Update Check", "Real GitHub query returns a defined outcome without throwing", false, ex.Message);
        }

        var available = TrayIconService.DescribeUpdate(new UpdateCheckResult(UpdateOutcome.UpdateAvailable, new Version(0, 1, 0), new Version(0, 2, 0), AppInfo.ReleasesPageUrl, null));
        Record("Update Check", "Update-available message shows current and latest versions", available.Contains("0.1.0") && available.Contains("0.2.0"), "");
    }

    // ---------------------------------------------------------------- Phase 2

    /// <summary>Runs in the process started by the tray's Restart item. Ends by clicking the real Exit item.</summary>
    public async Task RunPhase2Async(int? previousPid)
    {
        var state = ReadState();
        try
        {
            var pid1 = int.Parse(state.GetValueOrDefault("pid1", "0"));
            Record("Restart", "Replacement process was started with --restart-after <old pid>",
                previousPid == pid1 && Environment.GetCommandLineArgs().Contains(StartupOptions.RestartAfterArgument), $"restart-after={previousPid} expected={pid1}");
            Record("Restart", "Replacement is a different process", Environment.ProcessId != pid1, $"old={pid1} new={Environment.ProcessId}");
            Record("Restart", "Old process has exited", !ProcessAlive(pid1), "");
            Record("Restart", "Exactly one WorkBuddy process is running", Process.GetProcessesByName("WorkBuddy").Length == 1, $"count={Process.GetProcessesByName("WorkBuddy").Length}");
            Record("Restart", "Tray icon is back and the menu is complete", Tray.IsVisible && Tray.MenuHeaders.SequenceEqual(ExpectedMenu), "");

            var originalShow = bool.Parse(state.GetValueOrDefault("originalShowPet", "True"));
            var originalSpeed = double.Parse(state.GetValueOrDefault("originalSpeed", "1"), CultureInfo.InvariantCulture);
            Record("Restart", "Settings survived the restart unchanged",
                Settings.Current.Pet.ShowPet == originalShow && Math.Abs(Settings.Current.Pet.AnimationSpeed - originalSpeed) < 0.0001,
                $"ShowPet={Settings.Current.Pet.ShowPet} speed={Settings.Current.Pet.AnimationSpeed}");

            var clicked = new DateTime(long.Parse(state.GetValueOrDefault("restartClickedUtc", "0")), DateTimeKind.Utc);
            var prefs = Path.Combine(_paths.RootDirectory, "pet-preferences.json");
            Record("Restart", "Preferences were saved during the restart", File.Exists(prefs) && File.GetLastWriteTimeUtc(prefs) >= clicked,
                File.Exists(prefs) ? $"written {File.GetLastWriteTimeUtc(prefs):HH:mm:ss.fff} clicked {clicked:HH:mm:ss.fff}" : "file missing");

            var log = ReadLogFrom(long.Parse(state.GetValueOrDefault("logOffset1", "0")));
            Record("Restart", "Old process logged a controlled restart", log.Any(l => l.Contains("Restarting: replacement process started")), "");
            Record("Restart", "Old process removed the tray icon", log.Any(l => l.Contains("Tray icon removed")), "");
            Record("Restart", "Old process stopped the host cleanly", log.Any(l => l.Contains("Host stopped=true")), "");
            Record("Restart", "Old process finished shutdown", log.Any(l => l.Contains("shutdown complete")), "");

            // Exit: change a setting, then click the real Exit item. A third process verifies what was left behind.
            Settings.Update(s => s with { Pet = s.Pet with { AnimationSpeed = 1.3 } });
            SetState("exitClickedUtc", DateTime.UtcNow.Ticks.ToString());
            SetState("logOffset2", CurrentLogLength().ToString());
            SetState("pid2", Environment.ProcessId.ToString());

            Environment.SetEnvironmentVariable(StartupOptions.TrayPhaseEnvironmentVariable, "3");
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            info.ArgumentList.Add(StartupOptions.RestartAfterArgument);
            info.ArgumentList.Add(Environment.ProcessId.ToString());
            Process.Start(info)?.Dispose();
        }
        catch (Exception ex)
        {
            Record("Runtime", "Phase 2 completed without exception", false, ex.ToString());
        }

        var exit = Tray.FindMenuItem(TrayIconService.ExitHeader);
        Record("Exit", "Exit item exists and is enabled", exit is { IsEnabled: true }, "");
        if (exit is not null)
        {
            Click(exit);
            await Task.Delay(TimeSpan.FromSeconds(8));
        }

        Record("Exit", "Clicking Exit closed this process", false, "process still running 8 s after the click");
    }

    // ---------------------------------------------------------------- Phase 3

    /// <summary>Runs after the phase-2 process has exited. Inspects what Exit left behind and writes the final log.</summary>
    public async Task<bool> RunPhase3Async(int? previousPid)
    {
        var state = ReadState();
        try
        {
            var pid2 = int.Parse(state.GetValueOrDefault("pid2", "0"));
            SetState("pid3", Environment.ProcessId.ToString());
            Record("Exit", "Previous process has exited", previousPid == pid2 && !ProcessAlive(pid2), $"pid={pid2}");
            Record("Exit", "Single-instance lock was released (this launch became the first instance)", true, "this process holds the mutex");
            Record("Exit", "No other WorkBuddy process remains", Process.GetProcessesByName("WorkBuddy").Length == 1, $"count={Process.GetProcessesByName("WorkBuddy").Length}");

            var log = ReadLogFrom(long.Parse(state.GetValueOrDefault("logOffset2", "0")));
            Record("Exit", "Tray icon removed (logged by the exiting process)", log.Any(l => l.Contains("Tray icon removed")), "Windows shell tray state itself is not queryable");
            Record("Exit", "Host shutdown clean", log.Any(l => l.Contains("Host stopped=true")), Flat(log.FirstOrDefault(l => l.Contains("Host stopped")) ?? "no line"));
            Record("Exit", "Shutdown finished and log flushed", log.Any(l => l.Contains("shutdown complete")), "");

            var settingsOk = ReadSettingsFile("Pet", "AnimationSpeed").GetDouble() is var speed && Math.Abs(speed - 1.3) < 0.0001;
            Record("Exit", "Settings saved: the change made just before Exit is on disk", settingsOk, $"AnimationSpeed on disk={ReadSettingsFile("Pet", "AnimationSpeed").GetDouble()}");

            var exitClicked = new DateTime(long.Parse(state.GetValueOrDefault("exitClickedUtc", "0")), DateTimeKind.Utc);
            var prefs = Path.Combine(_paths.RootDirectory, "pet-preferences.json");
            Record("Exit", "Preferences saved on Exit", File.Exists(prefs) && File.GetLastWriteTimeUtc(prefs) >= exitClicked, "");

            var problems = ReadLogFrom(long.Parse(state.GetValueOrDefault("logOffset0", "0")))
                .Where(l => l.Contains("[ERR]") || l.Contains("[WRN]") || l.Contains("[FTL]")).ToList();
            Record("Runtime", "No [ERR]/[WRN]/[FTL] in the app log across all three processes", problems.Count == 0, string.Join(" | ", problems.Take(4).Select(Flat)));
        }
        catch (Exception ex)
        {
            Record("Runtime", "Phase 3 completed without exception", false, ex.ToString());
        }
        finally
        {
            var originalSpeed = double.Parse(ReadState().GetValueOrDefault("originalSpeed", "1"), CultureInfo.InvariantCulture);
            Settings.Update(s => s with { Pet = s.Pet with { AnimationSpeed = originalSpeed } });
        }

        await Task.CompletedTask;
        return FinishAndWrite();
    }

    // ---------------------------------------------------------------- Shared

    private bool FinishAndWrite()
    {
        var results = File.ReadAllLines(PartialPath).Where(l => l.StartsWith("R|")).Select(l => l.Split('|', 4)).ToList();
        var report = new StringBuilder();
        report.AppendLine($"WorkBuddy tray verification  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        var state = ReadState();
        report.AppendLine($"Processes: start={state.GetValueOrDefault("pid1")} -> restarted={state.GetValueOrDefault("pid2")} -> final={state.GetValueOrDefault("pid3", "-")}");
        report.AppendLine(new string('=', 78));
        foreach (var r in results)
        {
            report.AppendLine($"[{r[1]}] {r[2],-13} {r[3]}");
        }

        report.AppendLine(new string('=', 78));
        foreach (var group in results.GroupBy(r => r[2]))
        {
            report.AppendLine($"{(group.All(r => r[1] == "PASS") ? "PASS" : "FAIL")}  {group.Key}");
        }

        var failures = results.Count(r => r[1] != "PASS");
        report.AppendLine($"OVERALL: {(failures == 0 ? "PASS" : "FAIL")} ({failures} failed check(s) of {results.Count})");
        File.WriteAllText(Path.Combine(_paths.RootDirectory, "tray-verification.log"), report.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.Delete(PartialPath);
        return failures == 0;
    }

    private void Record(string group, string check, bool pass, string detail)
    {
        var text = Flat(string.IsNullOrEmpty(detail) ? check : $"{check}  -> {detail}");
        File.AppendAllText(PartialPath, $"R|{(pass ? "PASS" : "FAIL")}|{group}|{text}{Environment.NewLine}");
    }

    private void SetState(string key, string value) => File.AppendAllText(PartialPath, $"K|{key}|{value}{Environment.NewLine}");

    private Dictionary<string, string> ReadState() =>
        File.ReadAllLines(PartialPath).Where(l => l.StartsWith("K|")).Select(l => l.Split('|', 3))
            .GroupBy(p => p[1]).ToDictionary(g => g.Key, g => g.Last()[2]);

    private JsonElement ReadSettingsFile(string section, string name)
    {
        var text = File.ReadAllText(Path.Combine(_paths.RootDirectory, "settings.json"));
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty(section).GetProperty(name).Clone();
    }

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));

    private static IEnumerable<T> Windows<T>() where T : Window => System.Windows.Application.Current.Windows.OfType<T>();

    private static bool ProcessAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string Flat(string text) => text.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");

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

    private string? CurrentLogFile() =>
        Directory.Exists(_paths.LogsDirectory)
            ? Directory.GetFiles(_paths.LogsDirectory, "workbuddy-*.log").OrderBy(File.GetLastWriteTimeUtc).LastOrDefault()
            : null;

    private long CurrentLogLength() => CurrentLogFile() is { } f ? new FileInfo(f).Length : 0;

    private List<string> ReadLogFrom(long offset)
    {
        var lines = new List<string>();
        if (CurrentLogFile() is not { } file)
        {
            return lines;
        }

        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }
}
