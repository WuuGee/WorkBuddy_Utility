# Installer verification (`--verify-installer`)

Drives the real installer through silent installs and uninstalls in scratch folders under `%TEMP%`.

## Usage

1. Build the installer: `powershell -File installer\Build-Installer.ps1` (creates `artifacts\installer\WorkBuddy-Setup-<version>.exe`).
2. Run `WorkBuddy.exe --verify-installer` (about 3 minutes; headless). Set `WORKBUDDY_INSTALLER` to use another installer path.
3. Read `%LOCALAPPDATA%\WorkBuddy\installer-verification.log` (PASS/FAIL per check). Exit code 0 means all passed.

## Coverage

Install layout, data folder, Start Menu and optional desktop shortcut (and target), Apps & Features values, startup registration,
launch, `--quit`, installer closing a running app, upgrade (settings, preferences, startup preserved), startup disabled preserved,
uninstall with keep-data and with delete-data (each independently), removal of files, registry, shortcuts and Run value.

## Safety

- Installs go to a scratch dir (`/DIR`, `/DATADIR`); the real `%LOCALAPPDATA%\WorkBuddy` data is not touched.
- The user's Run value and `settings.json` are saved and restored. A running WorkBuddy is closed (restart it afterwards).
- Refuses to run if WorkBuddy is already installed.
- Test-only installer switches: `/DATADIR=<path>`, and `/DELETEDATA` for silent uninstall (silent uninstall keeps data by default).

## Not covered

Wizard GUI clicks, the interactive keep-data prompt, a real reboot/sign-in, Windows 10, SmartScreen.
