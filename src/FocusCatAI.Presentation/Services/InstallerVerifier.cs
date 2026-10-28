using System.Diagnostics;
using System.IO;
using System.Text;
using FocusCatAI.Shared;
using Microsoft.Win32;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Developer diagnostic behind <c>--verify-installer</c>. Drives the real built installer through silent installs and
/// uninstalls into scratch folders under %TEMP% and logs PASS/FAIL per check. The user's Run value, settings and
/// running instance are saved first and restored afterwards. Refuses to run if WorkBuddy is already installed.
/// </summary>
internal sealed class InstallerVerifier
{
    private const string AppId = "{B7C4F0A2-5D1E-4C8B-9A63-2E7F1D4B8C90}_is1";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppId;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WorkBuddy";
    private static readonly string Ver = AppInfo.CurrentVersion.ToString(3);

    private readonly StringBuilder _log = new();
    private readonly string _dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppConstants.AppName);
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "wb-verify-installer-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _appDir;
    private readonly string _data;
    private readonly string _legacy;
    private readonly string _startMenuLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WorkBuddy.lnk");
    private readonly string _desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WorkBuddy.lnk");
    private string _installer = "";
    private int _passed;
    private int _failed;

    public InstallerVerifier()
    {
        _appDir = Path.Combine(_scratch, "app");
        _data = Path.Combine(_scratch, "data");
        _legacy = Path.Combine(_scratch, "legacy");
    }

    public bool Run()
    {
        Line($"WorkBuddy installer verification {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        string? savedRun = ReadRun();
        var settingsPath = Path.Combine(_dataRoot, "settings.json");
        byte[]? savedSettings = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
        try
        {
            Directory.CreateDirectory(_scratch);
            if (!Prepare())
            {
                return Finish();
            }

            Fresh();
            Launch();
            Upgrade();
            StartupDisabled();
            Desktop();
            UninstallKeep();
            UninstallDelete();
            Legacy();
        }
        catch (Exception ex)
        {
            Check("Verifier ran without an unexpected error", false, ex.ToString());
        }
        finally
        {
            KillInstalled();
            RestoreRun(savedRun);
            if (savedSettings is not null)
            {
                File.WriteAllBytes(settingsPath, savedSettings);
            }

            try { Directory.Delete(_scratch, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            Line($"Restored user's Run value and settings; removed {_scratch}");
        }

        return Finish();
    }

    private bool Prepare()
    {
        _installer = FindInstaller() ?? "";
        Check("Installer executable found (build it with installer\\Build-Installer.ps1)", _installer.Length > 0, _installer);
        var existing = Registry.CurrentUser.OpenSubKey(UninstallKey) is not null;
        Check("No existing WorkBuddy installation (refusing to touch a real install)", !existing);
        if (_installer.Length == 0 || existing)
        {
            return false;
        }

        // The installed copy shares the single-instance mutex with a running WorkBuddy, so close the user's instance first.
        if (Process.GetProcessesByName("WorkBuddy").Any(p => p.Id != Environment.ProcessId))
        {
            SingleInstanceChannel.TrySignalExisting(SingleInstanceChannel.QuitCommand, TimeSpan.FromSeconds(3));
            WaitFor(() => !Process.GetProcessesByName("WorkBuddy").Any(p => p.Id != Environment.ProcessId), 10);
            Line("Note: a running WorkBuddy was closed for the test; start it again afterwards.");
        }

        return true;
    }

    private void Fresh()
    {
        Line("");
        Line("== Install (fresh, startup enabled) ==");
        Check("Silent install exits 0", Install("startup") == 0);
        var exe = Path.Combine(_appDir, "WorkBuddy.exe");
        Check("WorkBuddy.exe exists", File.Exists(exe));
        Check("Assets directory exists", Directory.Exists(Path.Combine(_appDir, "Assets")));
        Check("Self-contained runtime present (coreclr.dll)", File.Exists(Path.Combine(_appDir, "coreclr.dll")));
        Check("LICENSE installed", File.Exists(Path.Combine(_appDir, "LICENSE")));
        Check("Uninstaller exists", File.Exists(Path.Combine(_appDir, "unins000.exe")));
        Check("Settings (data) directory exists", Directory.Exists(_data));

        Line("== Shortcuts ==");
        Check("Start Menu shortcut exists", File.Exists(_startMenuLink));
        Check("Start Menu shortcut targets installed exe", string.Equals(LinkTarget(_startMenuLink), exe, StringComparison.OrdinalIgnoreCase), LinkTarget(_startMenuLink));
        Check("No desktop shortcut by default", !File.Exists(_desktopLink));

        Line("== Registry ==");
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
        Check("Apps & Features registration exists", key is not null);
        Check("Display name is WorkBuddy", (key?.GetValue("DisplayName") as string) == "WorkBuddy");
        Check("Version is " + Ver, (key?.GetValue("DisplayVersion") as string) == Ver, key?.GetValue("DisplayVersion") as string);
        Check("Publisher is Qian Tong Low", (key?.GetValue("Publisher") as string) == "Qian Tong Low");
        var uninstall = (key?.GetValue("UninstallString") as string ?? "").Trim('"');
        Check("Uninstall command points to unins000.exe", string.Equals(uninstall, Path.Combine(_appDir, "unins000.exe"), StringComparison.OrdinalIgnoreCase), uninstall);

        Line("== Startup (fresh install) ==");
        Check("Run value registered for the installed exe", ReadRun() == $"\"{exe}\" --autostart", ReadRun());
    }

    private void Launch()
    {
        Line("");
        Line("== Launch and running-app handling ==");
        var exe = Path.Combine(_appDir, "WorkBuddy.exe");
        Process.Start(new ProcessStartInfo(exe, "--autostart") { UseShellExecute = false });
        Check("Installed application launches and stays running", WaitFor(() => InstalledProcesses().Length > 0, 15) && !WaitFor(() => InstalledProcesses().Length == 0, 4));

        var quit = Process.Start(new ProcessStartInfo(exe, "--quit") { UseShellExecute = false, CreateNoWindow = true })!;
        quit.WaitForExit(10000);
        Check("--quit exits 0", quit.HasExited && quit.ExitCode == 0);
        Check("--quit closes the running process", WaitFor(() => InstalledProcesses().Length == 0, 15));

        Process.Start(new ProcessStartInfo(exe, "--autostart") { UseShellExecute = false });
        var running = WaitFor(() => InstalledProcesses().Length > 0, 15);
        Check("Application running before installer starts", running);
        var code = Install("startup");
        Check("Installer closes the running app and continues (exit 0)", code == 0);
        Check("Running process gone after installer", InstalledProcesses().Length == 0);
    }

    private void Upgrade()
    {
        Line("");
        Line("== Upgrade over existing installation ==");
        Directory.CreateDirectory(_data);
        const string settings = "{ \"General\": { \"StartWithWindows\": true }, \"Marker\": \"upgrade-test\" }";
        const string prefs = "{ \"Favorite\": \"upgrade-test\" }";
        File.WriteAllText(Path.Combine(_data, "settings.json"), settings);
        File.WriteAllText(Path.Combine(_data, "pet-preferences.json"), prefs);
        Check("Upgrade install exits 0", Install("startup") == 0);
        Check("Settings preserved", File.ReadAllText(Path.Combine(_data, "settings.json")) == settings);
        Check("Preferences preserved", File.ReadAllText(Path.Combine(_data, "pet-preferences.json")) == prefs);
        Check("Startup selection preserved (Run value present)", ReadRun() is not null);
        Check("Apps & Features entry still present", Registry.CurrentUser.OpenSubKey(UninstallKey) is not null);
    }

    private void StartupDisabled()
    {
        Line("");
        Line("== Existing install with startup disabled ==");
        const string settings = "{ \"General\": { \"StartWithWindows\": false } }";
        File.WriteAllText(Path.Combine(_data, "settings.json"), settings);
        Check("Upgrade install exits 0", Install(null) == 0);
        Check("Startup not re-enabled (Run value absent)", ReadRun() is null, ReadRun());
        Check("Disabled preference preserved in settings", File.ReadAllText(Path.Combine(_data, "settings.json")) == settings);
    }

    private void Desktop()
    {
        Line("");
        Line("== Optional desktop shortcut ==");
        Check("Install with desktop shortcut exits 0", Install("desktopicon") == 0);
        Check("Desktop shortcut exists when selected", File.Exists(_desktopLink));
        Check("Desktop shortcut target is correct", string.Equals(LinkTarget(_desktopLink), Path.Combine(_appDir, "WorkBuddy.exe"), StringComparison.OrdinalIgnoreCase));
    }

    private void UninstallKeep()
    {
        Line("");
        Line("== Uninstall (keep data) ==");
        File.WriteAllText(Path.Combine(_data, "settings.json"), "{ \"General\": { \"StartWithWindows\": true } }");
        Check("Install with startup exits 0", Install("startup,desktopicon") == 0);
        Check("Run value present before uninstall", ReadRun() is not null);
        Check("Uninstall exits 0", Uninstall(false) == 0);
        CheckRemoved();
        Check("Data kept", File.Exists(Path.Combine(_data, "settings.json")));
    }

    private void UninstallDelete()
    {
        Line("");
        Line("== Uninstall (delete data) ==");
        Check("Reinstall exits 0", Install("startup") == 0);
        Check("Data folder exists before uninstall", Directory.Exists(_data));
        Check("Uninstall with data deletion exits 0", Uninstall(true) == 0);
        CheckRemoved();
        Check("Data deleted", WaitFor(() => !Directory.Exists(_data), 10));
    }

    private void Legacy()
    {
        Line("");
        Line("== Legacy FocusCatAI data ==");
        void Seed() { Directory.CreateDirectory(_legacy); File.WriteAllText(Path.Combine(_legacy, "settings.json"), "{}"); }

        Seed();
        Check("Install exits 0", Install("startup") == 0);
        Check("Uninstall (default) exits 0", Uninstall(false) == 0);
        Check("Legacy data kept by default", File.Exists(Path.Combine(_legacy, "settings.json")));

        Check("Reinstall exits 0", Install("startup") == 0);
        Check("Uninstall deleting WorkBuddy data only exits 0", Uninstall(true) == 0);
        Check("Legacy data kept when only WorkBuddy data is deleted", File.Exists(Path.Combine(_legacy, "settings.json")));

        Check("Reinstall exits 0", Install("startup") == 0);
        Check("Uninstall deleting legacy data exits 0", Uninstall(false, true) == 0);
        Check("Legacy data removed when requested", WaitFor(() => !Directory.Exists(_legacy), 10));
        Check("Startup registration removed", ReadRun() is null, ReadRun());
    }

    private void CheckRemoved()
    {
        Check("Files removed", WaitFor(() => !Directory.Exists(_appDir), 20));
        Check("Registry removed (Apps & Features)", WaitFor(() => Registry.CurrentUser.OpenSubKey(UninstallKey) is null, 10));
        Check("Start Menu shortcut removed", !File.Exists(_startMenuLink));
        Check("Desktop shortcut removed", !File.Exists(_desktopLink));
        Check("Startup registration removed", ReadRun() is null, ReadRun());
    }

    private int Install(string? tasks)
    {
        var args = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL /DIR=\"{_appDir}\" /DATADIR=\"{_data}\" /LEGACYDIR=\"{_legacy}\"";
        if (tasks is not null)
        {
            args += $" /MERGETASKS=\"{tasks}\"";
        }

        return RunProcess(_installer, args, 240);
    }

    private int Uninstall(bool deleteData, bool deleteLegacy = false) =>
        RunProcess(Path.Combine(_appDir, "unins000.exe"), $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DATADIR=\"{_data}\" /LEGACYDIR=\"{_legacy}\"" + (deleteData ? " /DELETEDATA" : "") + (deleteLegacy ? " /DELETELEGACY" : ""), 120);

    private static int RunProcess(string file, string args, int seconds)
    {
        using var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true });
        if (p is null || !p.WaitForExit(seconds * 1000))
        {
            return -1;
        }

        return p.ExitCode;
    }

    private Process[] InstalledProcesses() =>
        Process.GetProcessesByName("WorkBuddy").Where(p =>
        {
            try { return p.MainModule?.FileName?.StartsWith(_appDir, StringComparison.OrdinalIgnoreCase) == true; }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
        }).ToArray();

    private void KillInstalled()
    {
        foreach (var p in InstalledProcesses())
        {
            try { p.Kill(); p.WaitForExit(5000); } catch (InvalidOperationException) { }
        }
    }

    private static string? FindInstaller()
    {
        var env = Environment.GetEnvironmentVariable("WORKBUDDY_INSTALLER");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return env;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", "installer", $"WorkBuddy-Setup-{Ver}.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ReadRun()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    private static void RestoreRun(string? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (value is null)
        {
            key.DeleteValue(ValueName, false);
        }
        else
        {
            key.SetValue(ValueName, value);
        }
    }

    private static string LinkTarget(string link)
    {
        if (!File.Exists(link))
        {
            return "";
        }

        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type is null)
        {
            return "";
        }

        dynamic shell = Activator.CreateInstance(type)!;
        return (string)shell.CreateShortcut(link).TargetPath;
    }

    private static bool WaitFor(Func<bool> condition, int seconds)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return condition();
    }

    private void Check(string name, bool ok, string? detail = null)
    {
        if (ok) { _passed++; } else { _failed++; }
        Line($"{(ok ? "PASS" : "FAIL")}  {name}" + (!ok && !string.IsNullOrEmpty(detail) ? $"  [{detail}]" : ""));
    }

    private void Line(string text) => _log.AppendLine(text);

    private bool Finish()
    {
        Line("");
        Line($"Result: {(_failed == 0 ? "PASS" : "FAIL")}  ({_passed} passed, {_failed} failed)");
        Directory.CreateDirectory(_dataRoot);
        File.WriteAllText(Path.Combine(_dataRoot, "installer-verification.log"), _log.ToString(), new UTF8Encoding(true));
        return _failed == 0;
    }
}

