# WorkBuddy v0.2.0 (Companions and themes)

## Highlights

- **Selectable companions:** choose between **Cream Cat**, **Tuxedo Cat** and **Ginger Cat**. Switch live from the tray (**Pet** submenu) or from Settings > Pet > Companion. No restart, and the pet keeps walking where it is. Your choice is saved.
- **Light, Dark and System themes:** Settings and About follow your choice, including a dark title bar. **System** (the default) follows the Windows app theme. Switching is live, with no restart. Find it in Settings > General > Theme.
- **Quality and verification:** 229 automated unit tests, a 70-check tray verifier, and expanded settings verification for pet selection and themes.

## Also in this release

- The tray icon shows the selected companion.
- An unknown pet or theme value in `settings.json` falls back to the defaults instead of failing.
- The pet and its speech bubble keep their own art style in both themes.

## Install

1. Download `WorkBuddy-Setup-0.2.0.exe`.
2. Double-click it. No administrator rights, Visual Studio or .NET are needed.
3. Restart Windows: the cat appears automatically (startup is on by default and can be turned off in Settings).

Installing over v0.1.x upgrades in place and keeps your settings and startup choice.
Installs to `%LOCALAPPDATA%\Programs\WorkBuddy`.

## SmartScreen

This release is not code-signed, so Windows may warn. Choose **More info**, then **Run anyway**.
Verify the download against `WorkBuddy-Setup-0.2.0.exe.sha256`.

## Uninstall

Settings, then Apps, then WorkBuddy, then Uninstall. You are asked whether to keep your settings and, if present, old FocusCatAI data. Both default to Keep.

## Known limitations

- Installer is unsigned.
- Tuxedo Cat and Ginger Cat are recolors of the Cream Cat artwork (same animations).
- Dark theme covers the Settings and About windows. Native message boxes follow Windows, and the pet and its speech bubble are not themed.
- No dedicated application or tray icon yet (the selected pet's first idle frame is used).
- Updates are manual to install; the app only tells you when one exists.
- Tested primarily on Windows 11; Windows 10 is not yet validated.

## License

MIT. Copyright (c) 2026 Qian Tong Low.
