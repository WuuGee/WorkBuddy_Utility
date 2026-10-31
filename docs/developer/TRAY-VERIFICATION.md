# Tray verification (`--verify-tray`)

Permanent developer diagnostic for the system tray: startup, Show Cat, Hide Cat, Open Settings, About, Check for
Updates, Open Logs Folder, Report an Issue, Restart and Exit. It clicks the real menu items, so what passes here is what a user gets.

## Usage

```powershell
# Close any running WorkBuddy first.
Start-Process .\WorkBuddy.exe -ArgumentList "--verify-tray"
# Wait about 30 seconds, then read the result:
Get-Content "$env:LOCALAPPDATA\WorkBuddy\tray-verification.log"
```

There is no exit code to wait on: the run **spans three processes** because Restart and Exit end the process they are
clicked in. Poll for `tray-verification.log` instead. `--autostart` always overrides this switch.

| Process | Does | Ends by |
|---|---|---|
| 1 (`--verify-tray`) | Tray menu, tooltip, Hide, Show, Settings, About, update query | Clicking the real **Restart WorkBuddy** item |
| 2 (started by Restart, `WORKBUDDY_VERIFY_TRAY_PHASE=2`) | Checks the restart happened; changes a setting | Clicking the real **Exit** item |
| 3 (`WORKBUDDY_VERIFY_TRAY_PHASE=3`) | Checks Exit left nothing behind; restores settings; writes the log | Exiting (code 0 or 1) |

The phase is passed in an environment variable so that the product's Restart code needs no verifier-only branches.
Intermediate results are kept in `%LOCALAPPDATA%\WorkBuddy\tray-verification.partial`, deleted at the end. If a run is
interrupted, delete that file and run again.

## Output

`%LOCALAPPDATA%\WorkBuddy\tray-verification.log`: one `[PASS]`/`[FAIL]` line per check, a per-area summary, and
`OVERALL: PASS (0 failed check(s) of 62)`.

## What it checks

| Area | Checks |
|---|---|
| Tray Startup | Icon visible; menu order is exactly as designed; no Pomodoro items; tooltip has name, status, version, startup state and fits 127 characters |
| Hide Cat | Pet hidden; saved to `settings.json`; tray stays; tooltip says `Pet Hidden`; Show/Hide enablement swaps |
| Show Cat | Pet restored in under 1 s; saved; tooltip returns to `Running` |
| Settings | Opens; clicking again reuses the window; closes |
| About | Name, version, copyright, license, repository, data folder, open-source notice; Open GitHub button present; no duplicates |
| Update Check | Real GitHub query returns a defined result without throwing; messages are produced |
| Diagnostics | Open Logs Folder and Report an Issue are present and enabled; the issues URL is the repository's https issues page; the logs folder exists |
| Restart | `--restart-after <pid>`; new pid; old process gone; one process; tray back; settings and preferences survive; old process logged tray removal, clean host stop and shutdown completion |
| Exit | Process gone; one process left (the verifier); tray removal, host stop and shutdown logged; a setting changed just before Exit is on disk; preferences saved |
| Runtime | No `[ERR]`, `[WRN]` or `[FTL]` in the app log across all three processes |

## Limits

- **Windows' tray state is not queryable.** "Tray icon removed" is the app's own log line, not an inspection of the
  notification area. The icon disappearing visually was not observed.
- Open GitHub, Check for Updates, Open Logs Folder and Report an Issue are checked for presence, not clicked: they open a browser, Explorer or a message box.
  The update logic itself is covered by unit tests with a stubbed HTTP handler plus one real GitHub query. The launcher's URL guard is covered by unit tests (`ShellLauncherTests`).
- The startup update balloon and its click-to-open behaviour are not exercised by this verifier; they need a manual test against a newer release.
- Show Cat and Hide Cat call the same code paths as the menu clicks but do not move a real mouse.
- Right-click menu rendering is not inspected.
- The real GitHub query currently reports `NoReleases` because the repository has no published release yet.
