# WorkBuddy v0.1.0 (Release Candidate 1)

A small desktop cat that lives above your taskbar.

## Install

1. Download `WorkBuddy-Setup-0.1.0.exe`.
2. Double-click it. No administrator rights, Visual Studio or .NET are needed.
3. Restart Windows: the cat appears automatically (startup is on by default and can be turned off in Settings).

Installs to `%LOCALAPPDATA%\Programs\WorkBuddy`.

## SmartScreen

This release is not code-signed, so Windows may warn. Choose **More info**, then **Run anyway**.
Verify the download against `WorkBuddy-Setup-0.1.0.exe.sha256`.

## Uninstall

Settings, then Apps, then WorkBuddy, then Uninstall. You are asked whether to keep your settings and, if present, old FocusCatAI data. Both default to Keep.

## Known limitations

- Installer is unsigned.
- No dedicated application or tray icon yet (the cat's first idle frame is used).
- Updates are manual (Tray, then Check for Updates).
- Tested primarily on Windows 11; Windows 10 is not yet validated.

## License

MIT. Copyright (c) 2026 Qian Tong Low.
