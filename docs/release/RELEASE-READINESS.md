# WorkBuddy Utility - Release Readiness (v0.1.0) - PROPOSAL, awaiting approval

## 0. Current state (from the code)
| Area | Today | Gap for release |
|---|---|---|
| Tray | `TrayIconService` (Hardcodet) with Show/Hide Pet, Pomodoro items, Exit | Needs Settings, Restart, Check for Updates, About; Pomodoro items are not in your list |
| Single instance | Named mutex, 2nd launch silently exits | 2nd launch should tell the first to show the cat |
| Settings | `appsettings.json` next to the exe (becomes read-only under Program Files) + `pet-preferences.json` in `%LOCALAPPDATA%\FocusCatAI` | No user settings file, no UI. User settings must never live in the install folder |
| Startup | Starts the host, then window and tray | No `--autostart` handling, no wait for Explorer/taskbar |
| Shutdown | `OnExit` is `async void`, awaits `StopAsync` | WPF may finish exiting before the host stops. Fix; handle `SessionEnding` |
| Packaging | Debug build, framework-dependent, project named `FocusCatAI` | No publish profile, version, icon (`.ico`), manifest, LICENSE/README |
| Naming | Assembly, mutex, data folder, log text still say FocusCat AI | Decide the rename (see section 8) |

## 1. Release Readiness Architecture
```
Installer (Inno Setup, per-user)      installs binaries only -> %LOCALAPPDATA%\Programs\WorkBuddy
WorkBuddy.exe  (self-contained win-x64)
  App (WPF, no main window)
   |- SingleInstance      mutex + named event ("show yourself" / "quit")
   |- StartupRegistration HKCU Run key read/write (+ StartupApproved check)
   |- SettingsStore       %LOCALAPPDATA%\WorkBuddy\settings.json  (schema version, migrations, atomic write)
   |- TrayIconService     menu + tooltip
   |- SettingsWindow      General / Pet / Notifications / Advanced
   |- PetWindow (existing)
   `- UpdateChecker       (stub in 0.1.0: opens releases page)
Data (never touched by upgrade): settings.json, pet-preferences.json, logs\
```
- Layering stays as is: settings model and migration in Application/Infrastructure, window and registry code in Presentation.
- Settings precedence: built-in defaults (appsettings.json) < user `settings.json`. Changes apply live where cheap (size, speed, show/hide) and are saved atomically (write temp, then replace).
- Settings keys: General (`LaunchOnStartup` default true, `StartMinimized`, `Language` placeholder), Pet (`ShowCat`, `Size`, `AnimationSpeed`, `Placement`), Notifications (`EnableMessages`, `QuietHoursStart/End`), Advanced (Reset Settings, Open Data Folder). Quiet hours need a small hook in `PetSpeaker` (suppress non-urgent speech).

## 2. Auto-start strategy (evaluation)
| Option | Reliable | Standard user | Easy uninstall | Notes |
|---|---|---|---|---|
| Startup folder shortcut | Yes | Yes | Delete the .lnk | Visible to users, but extra file and shortcut resolution; slightly slower |
| **HKCU Run key** | Yes | Yes, no admin | Delete one value | Appears in Task Manager > Startup apps; user can disable there (stored in `StartupApproved\Run`) |
| Scheduled Task (logon trigger) | Yes | Per-user task OK, "highest privileges" needs admin | Needs `schtasks`/COM cleanup | Overkill; elevation is unwanted for a taskbar companion |

**Recommendation: HKCU Run key**, value `WorkBuddy` = `"<path>\WorkBuddy.exe" --autostart`.
- The app owns the setting: the toggle writes or removes the value. The installer only sets the initial default (on) and removes it on uninstall.
- Read `StartupApproved\Run` so the checkbox reflects a Task Manager disable instead of lying.
- Self-heal: on each launch, if the setting is on and the exe path differs (e.g. after an upgrade), rewrite the value.
- Windows delays Run entries until Explorer is up, but the taskbar may still not be ready, so see section 4.

## 3. Installer recommendation
| | MSIX | **Inno Setup** | WiX (MSI) |
|---|---|---|---|
| Needs code-signing cert | Yes (hard requirement) | No (SmartScreen warning if unsigned) | No (same warning) |
| Open-source friction | High (cert cost, sideload trust) | Low | Medium (XML, learning curve) |
| Per-user, no admin | Yes | Yes | Possible but awkward |
| Custom uninstall prompt ("keep data?") | Not supported | Simple (Pascal script) | Possible, complex |
| Run key / process kill on uninstall | Limited (startupTask only) | Direct | Possible |
| CI (GitHub Actions) | OK | OK (one `iscc` call) | OK |

**Recommendation: Inno Setup**, per-user (`PrivilegesRequired=lowest`), installs to `%LOCALAPPDATA%\Programs\WorkBuddy`, Start Menu shortcut, uninstall entry, optional "Launch at startup" checkbox (default on), `AppMutex` so installer/uninstaller close the running app.
- **Runtime:** publish **self-contained win-x64** (no .NET prerequisite, so "download, run, done"). Expect roughly 150 MB unpacked and 60-70 MB installer; trimming is not supported for WPF. Alternative: framework-dependent plus a runtime check (about 5 MB installer, but users without .NET 8 Desktop Runtime hit a prompt). I recommend self-contained for v0.1.0.
- **Signing:** unsigned v0.1.0 will trigger SmartScreen. Apply to SignPath Foundation (free for OSS) in parallel; publish SHA-256 checksums.
- **Future updates:** if Velopack is chosen later (section 6) it brings its own installer and would replace Inno. Starting with Inno is still the lowest-risk path for 0.1.0.

## 4. Startup / shutdown workflow
**Start (login or manual):**
1. Run key launches `WorkBuddy.exe --autostart`.
2. Mutex check. If another instance exists, signal it (show cat) and exit.
3. Build host, load defaults + `settings.json` + preferences, start logging.
4. Show tray icon first (the app is alive even if the taskbar is not).
5. Wait for `Shell_TrayWnd` and the taskbar scan with backoff (e.g. 1, 2, 4, 8 s up to about 60 s). If it never appears, fall back to a floating placement and keep retrying. Respect `ShowCat` and `StartMinimized` (tray only).
6. Intro plays only on first launch (already persisted).

**Runtime resilience to verify:** Explorer restart (taskbar recreated, tray icon re-added), display or DPI change, sleep/resume, fast user switching.

**Shutdown (Exit, uninstall, `SessionEnding`, Restart):**
1. Stop behaviors, save preferences and settings (atomic).
2. Dispose tray icon (avoids a ghost icon), unhook `ZOrderGuard`.
3. `StopAsync` with timeout, flush logs, release mutex.
4. Fix `OnExit` so shutdown is awaited (not `async void`); `Restart Cat` = start a new process with `--restarted`, then exit.

**Crash safety:** keep the existing unhandled-exception logging; add crash-loop protection for the autostart entry (e.g. if 3 launches fail within 2 minutes, stop retrying and show a tray message next time).

## 5. Uninstall strategy
1. Ask the running app to quit (named event); fall back to a kill after a few seconds.
2. Delete the HKCU Run value and the Start Menu shortcut.
3. Remove the install folder (binaries only).
4. Prompt: "Keep your settings, favorites and statistics?" Default **keep**. If No, delete the whole data folder (`settings.json`, `pet-preferences.json`, logs).
5. Tray icon disappears when the process exits; no other tray registration exists.
6. Silent uninstall (`/VERYSILENT`) keeps data unless `/PURGE` is passed.
**Acceptance:** after uninstall and reboot the cat does not appear, no Run value, no process, no files in the install folder.

## 6. Update architecture (design only)
| Option | Pros | Cons |
|---|---|---|
| GitHub Releases + version check | Trivial, no infra, transparent | Manual download/install |
| **Velopack** | Per-user installer + delta updates, OSS friendly | Replaces Inno; new tooling |
| winget manifest | Familiar to users, zero hosting | Slow review cycle, still manual per release |
| MSIX App Installer | Native auto-update | Needs signing cert |
| Squirrel.Windows | Mature | Largely superseded by Velopack |

**0.1.0:** `Check for Updates` calls the GitHub Releases API (or just opens the releases page), compares to the assembly version, shows a notification. No download or install. Design hooks: `IUpdateChecker` and version stored in settings. Revisit Velopack for 0.2+.

## 7. Data storage and upgrades
- Install folder holds binaries only. Upgrade overwrites it; user data is untouched.
- `settings.json` and `pet-preferences.json` have a `SchemaVersion`; add a migration step per bump. Unknown newer versions are loaded read-only with a backup instead of being overwritten.
- Atomic writes plus a `.bak` copy guard against corruption on power loss.
- Favorites survive reboots and upgrades (already persisted today).

## 8. Decisions needed before implementation
1. **Rename:** keep the data folder `%LOCALAPPDATA%\FocusCatAI` (no migration, but old name) or move to `WorkBuddy` with a one-time migration of existing preferences? Also rename assembly/exe to `WorkBuddy.exe`? (Recommended: rename, with migration.)
2. **Runtime:** self-contained (recommended) or framework-dependent?
3. **Pomodoro tray items:** keep, move into settings, or remove from the tray?
4. **Windows versions:** Win11 only, or Win10 too? (Taskbar detection was only tested on Win11.)
5. **Signing:** unsigned 0.1.0 with a README SmartScreen note, or wait for SignPath approval?

## 9. v0.1.0 release plan
| # | Work package | Acceptance |
|---|---|---|
| 1 | Rename/versioning: product name, exe, mutex, icon `.ico`, `Version 0.1.0`, LICENSE, README | Exe shows name, icon, version |
| 2 | Settings store + migration + tests | Settings and favorites survive restart and upgrade |
| 3 | Startup registration + `--autostart` + taskbar wait/backoff | Cat appears after reboot, also with a slow Explorer |
| 4 | Tray menu (Show, Hide, Settings, Restart, Updates, About, Exit) | Every item works; no main window |
| 5 | Settings window (General, Pet, Notifications, Advanced) | Changes apply live and persist |
| 6 | Shutdown fix, `SessionEnding`, single-instance signaling, crash-loop guard | Clean exit in log; no ghost tray icon |
| 7 | Publish profile + Inno script + CI (GitHub Actions) | Installer built from a tag |
| 8 | Test matrix: clean Win11 VM, standard user, reboot, upgrade 0.1.0 to 0.1.1, uninstall (keep and purge), Explorer restart | All pass; log review shows no ERR |
| 9 | Release: tag v0.1.0, GitHub Release, checksums, notes | Download, install, reboot, cat appears |

Out of scope (unchanged): XP, levels, accessories, achievements, AI expansion, automatic updates.
