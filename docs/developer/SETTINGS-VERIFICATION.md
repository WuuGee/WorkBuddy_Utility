# Settings verification (`--verify-settings`)

A permanent developer diagnostic. It drives every settings path against the real, running application and writes a
PASS/FAIL report. Use it for regression checks, before a release, and later in CI.

## Purpose

Unit tests prove the settings logic. This proves the wiring: that the settings window, the settings file, the pet
window and the behavior services actually agree when WorkBuddy is running.

## Usage

```powershell
.\WorkBuddy.exe --verify-settings
```

- Close any running WorkBuddy first. A running instance owns the single-instance mutex, so a second launch would only
  signal it and exit.
- The run takes about 15 seconds. WorkBuddy shows the pet and opens the settings window briefly, then exits by itself.
- The process exit code is `0` when every check passed and `1` when any failed.
- `--autostart` always wins over this switch, so a launch from the Windows Run key can never start a verifier.
- Your own settings are saved before the run and restored afterwards.

```powershell
$p = Start-Process .\WorkBuddy.exe -ArgumentList "--verify-settings" -PassThru -Wait
$p.ExitCode   # 0 = all passed
```

## Output

| File | Location |
|---|---|
| `settings-verification.log` | `%LOCALAPPDATA%\WorkBuddy\` (overwritten on every run) |
| Application log | `%LOCALAPPDATA%\WorkBuddy\logs\workbuddy-YYYYMMDD.log` (ends with `Settings verification finished: PASS`) |

Report format: one line per check, `[PASS]` or `[FAIL]`, the area, the check, and `->` the measured detail. A summary
per area and an `OVERALL` line follow.

```
[PASS] Pet            Size 48 -> 32px changes window  -> OldSize=48 NewSize=32 ActualWidth=32 ActualHeight=32
[PASS] Behavior       EnableSleep=false + SleepDelay=60s reach MoodService  -> SleepEnabled=False SleepyAfter=60s
...
PASS  Storage
OVERALL: PASS (0 failed check(s))
```

## What it checks

| Area | Checks |
|---|---|
| Storage | `settings.json` exists, is indented, is valid JSON, has `SchemaVersion` |
| Settings Window | Opens, is 720x520, each of the six categories shows only its page, controls show current values |
| General | StartWithWindows and StartMinimized saved; RunInTaskbarMode moves the pet above the taskbar and back, live |
| Pet | ShowPet hide and show; Size 32/48/64/96 changes the real window size; Placement; AnimationSpeed read by the animation loop; IntroAnimation on and off |
| Behavior | EnableSleep and SleepDelay reach `MoodService`; EnableMessages; MessageFrequency cooldowns (Low 30 min, Normal 15, High 5); quiet hours suppress speech |
| Quiet Hours | Start/End saved, invalid input rejected, enable and disable |
| Notifications | Productivity and Pomodoro toggles |
| Persistence | A fresh `JsonSettingsService` (a simulated restart) loads identical values |
| Advanced | Open Data Folder, Open Log Folder, Reset Preferences, Reset All Settings |
| Runtime | No `[ERR]`, `[WRN]` or `[FTL]` lines written to the app log during the run |

## Expected result

exit code `0`, 55 checks.
measured instead.

## Limits

- It drives the window's view model and code, not mouse clicks, so it cannot judge visual layout.
- Speech, intro and notification checks use throwaway objects wired to the real settings service, with a fake clock.
- Reset All Settings runs against the live settings; the original values are restored at the end.
- `StartWithWindows` registry behavior is covered by `--verify-startup`; see `STARTUP-VERIFICATION.md`.

## Maintaining it

Add a check to `SettingsVerifier` whenever a setting is added. Keep one `Record(...)` per observable behavior.
