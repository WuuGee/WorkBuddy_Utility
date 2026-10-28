# WorkBuddy v0.1.0 RC1 checklist

Status: automated items PASS; manual items open. Re-run the verifiers after any change.

## Automated (developer verifiers, each writes a log to `%LOCALAPPDATA%\WorkBuddy`)

- [x] Unit tests: 161/161 (`dotnet test`)
- [x] Settings tested: `--verify-settings`
- [x] Startup tested: `--verify-startup`
- [x] Tray tested: `--verify-tray`
- [x] Installer tested: `--verify-installer` (install, upgrade, startup, keep/delete data, legacy data, `--quit`)
- [x] Uninstaller tested: `--verify-installer` (files, registry, shortcuts, Run value, keep/delete WorkBuddy data, keep/delete legacy data)
- [x] Migration tested: `LegacyDataMigration` unit tests; installer legacy keep/delete paths
- [x] Release files present and consistent: `--verify-release`

## Manual (release validation, not yet done)

- [ ] Installer wizard click-through, including both uninstall prompts
- [ ] Real reboot and sign-in: cat appears
- [ ] Windows 10 validation
- [ ] SmartScreen experience on a clean machine
- [ ] Clean-machine install without .NET installed

## Known gaps

- No application (.ico) or dedicated tray icon; the exe uses the default icon.
- Installer is unsigned.

## Build and package

1. `installer\Package-Release.ps1` (builds installer, creates `artifacts\release`).
2. `WorkBuddy.exe --verify-release`.
