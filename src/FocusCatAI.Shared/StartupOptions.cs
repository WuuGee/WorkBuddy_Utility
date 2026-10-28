namespace FocusCatAI.Shared;

/// <summary>
/// Command-line switches. <c>--autostart</c> (launched by Windows at sign-in) always wins: it skips developer
/// diagnostics so a launch at sign-in can never run a verifier or open a window.
/// </summary>
/// <param name="TrayPhase">
/// 0 = not verifying the tray. 1 = <c>--verify-tray</c>. 2 and 3 are the follow-up processes of the same verification,
/// selected through <see cref="TrayPhaseEnvironmentVariable"/> so the real tray Restart item can be clicked unmodified.
/// </param>
public sealed record StartupOptions(bool Autostart, bool VerifySettings, bool VerifyStartup, int TrayPhase = 0, int? RestartAfterProcessId = null, bool Quit = false, bool VerifyInstaller = false, bool VerifyRelease = false)
{
    public const string VerifyReleaseArgument = "--verify-release";
    public const string QuitArgument = "--quit";
    public const string VerifyInstallerArgument = "--verify-installer";
    public const string VerifySettingsArgument = "--verify-settings";
    public const string VerifyStartupArgument = "--verify-startup";
    public const string VerifyTrayArgument = "--verify-tray";
    public const string TrayPhaseEnvironmentVariable = "WORKBUDDY_VERIFY_TRAY_PHASE";

    /// <summary>Followed by a process id. Set by a controlled restart: wait for that process to end before starting.</summary>
    public const string RestartAfterArgument = "--restart-after";

    public bool VerifyTray => TrayPhase != 0;

    public static StartupOptions Parse(IEnumerable<string> args) =>
        Parse(args, Environment.GetEnvironmentVariable(TrayPhaseEnvironmentVariable));

    public static StartupOptions Parse(IEnumerable<string> args, string? trayPhaseFromEnvironment)
    {
        var list = args.ToList();
        bool Has(string name) => list.Contains(name, StringComparer.OrdinalIgnoreCase);

        int? restartAfter = null;
        var index = list.FindIndex(a => a.Equals(RestartAfterArgument, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < list.Count && int.TryParse(list[index + 1], out var pid) && pid > 0)
        {
            restartAfter = pid;
        }

        var autostart = Has(AppConstants.AutostartArgument);
        var trayPhase = 0;
        if (!autostart)
        {
            if (Has(VerifyTrayArgument))
            {
                trayPhase = 1;
            }
            else if (int.TryParse(trayPhaseFromEnvironment, out var phase) && phase is 2 or 3)
            {
                trayPhase = phase;
            }
        }

        return new StartupOptions(
            autostart,
            !autostart && Has(VerifySettingsArgument),
            !autostart && Has(VerifyStartupArgument),
            trayPhase,
            restartAfter,
            Quit: Has(QuitArgument),
            VerifyInstaller: !autostart && Has(VerifyInstallerArgument),
            VerifyRelease: !autostart && Has(VerifyReleaseArgument));
    }
}
