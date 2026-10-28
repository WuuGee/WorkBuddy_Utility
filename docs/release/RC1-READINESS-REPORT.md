# WorkBuddy v0.1.0 RC1: Release Candidate Readiness Report

## 1. Product overview

WorkBuddy is a small animated desktop cat that lives above the Windows taskbar. It is a free, MIT-licensed utility
(Copyright (c) 2026 Qian Tong Low). It installs per user with no administrator rights and no .NET knowledge, and starts with Windows.

## 2. Implemented features

| Area | Summary |
|---|---|
| Taskbar companion | Cat window positioned above the taskbar, with Z-order protections. |
| Pet behaviors | Idle, walking and reaction animations (Pet Art V2), Pomodoro reactions. |
| Startup integration | Per-user Run key, repaired if moved, respects "disabled in Windows" and the user's choice. |
| Settings system | Persistent `settings.json`, applied live. |
| Tray management | Show Cat, Hide Cat, Settings, Restart, About, Check for Updates, Exit. |
| Data migration | One-time copy of old `%LOCALAPPDATA%\FocusCatAI` data. The old folder is never deleted automatically. |
| Installer | Inno Setup, self-contained win-x64, `%LOCALAPPDATA%\Programs\WorkBuddy`, Start Menu shortcut, optional desktop shortcut, Apps & Features entry, upgrade-safe. |
| Uninstaller | Closes the running app, removes files, shortcuts and startup entry. Asks separately whether to keep WorkBuddy data and old FocusCatAI data (both default to Keep). |
| Diagnostics | `--verify-settings`, `--verify-startup`, `--verify-tray`, `--verify-installer`, `--verify-release`, each writing a PASS/FAIL log. |

Updates are manual only (Check for Updates). There is no automatic updating.

## 3. Verification summary

Each verifier drives the real application or installer and writes a log to `%LOCALAPPDATA%\WorkBuddy`.
Documentation for each is in `docs/developer/`.

## 4. Automated verification results

| Verifier | Result | Detail |
|---|---|---|
| Unit tests | PASS | 161 of 161 |
| `--verify-settings` | PASS | 0 failed |
| `--verify-startup` | PASS | 0 failed |
| `--verify-tray` | PASS | 59 of 59 |
| `--verify-installer` | PASS | 63 of 63: install, launch, shortcuts, registry, startup, upgrade, `--quit`, keep/delete data, keep/delete legacy data, uninstall |
| `--verify-release` | PASS | 26 of 26: metadata, About data, version consistency, installer and package contents, SHA-256 |

Release package: `artifacts\release\` (installer, `.sha256`, `LICENSE`, `RELEASE-NOTES.md`), built by `installer\Package-Release.ps1`.
Installer tests ran silent installs into scratch folders; the user's real data and startup entry were saved and restored.

## 5. Known limitations

- Unsigned installer. Windows SmartScreen is expected to warn (More info, then Run anyway). The README and release notes explain this. The SHA-256 is published.
- Windows 10 has not been validated. Windows 11 is the primary target.
- A real reboot and sign-in has not been tested. Startup is verified only through the registry entry and a simulated `--autostart` launch.
- There is no dedicated application icon; the exe uses the default Windows icon. The tray uses the cat's first idle frame.
- The interactive wizard and the two uninstall prompts (keep data, keep legacy data) have not been manually exercised. Their logic was tested through silent switches (`/DELETEDATA`, `/DELETELEGACY`).
- Not tested on a clean machine without .NET installed (the build is self-contained, but this has not been proven on another machine).

## 6. Manual validation checklist

- [ ] Installer wizard walkthrough: license page, install location, tasks (desktop shortcut, startup), finish and launch
- [ ] Uninstall walkthrough from Settings, then Apps, then WorkBuddy
- [ ] Data retention prompt: choose Yes (data kept) and No (data deleted), each in its own run
- [ ] Legacy data prompt: choose Yes and No, each in its own run
- [ ] Real reboot and sign-in: the cat appears without user action
- [ ] Windows 10 install, launch, startup and uninstall
- [ ] SmartScreen experience on a machine that downloaded the installer from the internet
- [ ] Clean machine with no .NET installed
- [ ] Upgrade from a previous build while the app is running

## 7. Release risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| SmartScreen discourages installs | High | Medium | Documented; signing is a future item. |
| Wizard or prompt wording or layout issue not seen in silent tests | Medium | Low | Manual walkthrough (section 6). |
| Startup fails after a real reboot | Low | High | Registry value and autostart launch verified; confirm with a reboot test. |
| Windows 10 differences (taskbar position, shadows) | Medium | Low to medium | Validate as release testing; fix only real issues. |
| Default icon looks unpolished | Certain | Low | Accepted for v0.1.0; icon artwork is post-release. |
| Antivirus false positive on an unsigned installer | Low to medium | Medium | Publish the checksum; submit to vendors if reported. |

## 8. Release recommendation

**READY FOR PUBLIC ALPHA.**

Justification: every automated verifier passes, the installer and uninstaller were exercised end to end with real silent installs, and no data is deleted without an explicit choice. The open items
(reboot test, wizard and prompt walkthrough, Windows 10, SmartScreen, no icon, unsigned) are cosmetic or require manual validation. None is a known defect.
An alpha label fits that evidence. It is not a recommendation for a general release until the manual checklist in section 6 is complete, in particular the real reboot and the prompt walkthrough.
