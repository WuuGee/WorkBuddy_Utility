# WorkBuddy v0.1.1 (Alpha stabilization)

A small desktop cat that lives above your taskbar.

## What's new

- **Open Logs Folder** and **Report an Issue** in the tray menu, for easier diagnostics and feedback.
- **Update notice at startup:** one quiet check; a balloon appears only if a newer release exists. Click it to open the download page. **Check for Updates** remains available in the tray. Nothing is downloaded or installed automatically.
- **Display changes:** the cat repositions promptly after a resolution or DPI change, and the DPI is read from the taskbar's monitor.
- The update check now gives up after 5 seconds and never interrupts you if you are offline.

## Install

1. Download `WorkBuddy-Setup-0.1.1.exe`.
2. Double-click it. No administrator rights, Visual Studio or .NET are needed.
3. Restart Windows: the cat appears automatically (startup is on by default and can be turned off in Settings).

Installing over v0.1.0 upgrades in place and keeps your settings and startup choice.
Installs to `%LOCALAPPDATA%\Programs\WorkBuddy`.

## SmartScreen

This release is not code-signed, so Windows may warn. Choose **More info**, then **Run anyway**.
Verify the download against `WorkBuddy-Setup-0.1.1.exe.sha256`.

## Uninstall

Settings, then Apps, then WorkBuddy, then Uninstall. You are asked whether to keep your settings and, if present, old FocusCatAI data. Both default to Keep.

## Known limitations

- Installer is unsigned.
- No dedicated application or tray icon yet (the cat's first idle frame is used).
- Updates are manual to install; the app only tells you when one exists.
- Tested primarily on Windows 11; Windows 10 is not yet validated.

## License

MIT. Copyright (c) 2026 Qian Tong Low.
