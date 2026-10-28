# WorkBuddy

WorkBuddy is a small Windows desktop companion: a virtual cat that lives on your taskbar, keeps you company while you
work, and gives gentle, rare reminders. It runs locally, needs no account, and sends no personal data anywhere.

## Install

1. Download `WorkBuddy-Setup-0.1.0.exe` from the [Releases page](https://github.com/WuuGee/WorkBuddy_Utility/releases).
2. Double-click it and follow the steps. No administrator rights are needed, and .NET does not have to be installed.
3. WorkBuddy starts with Windows by default. You can change that in Settings.

WorkBuddy installs to `%LOCALAPPDATA%\Programs\WorkBuddy`; your settings live in `%LOCALAPPDATA%\WorkBuddy`.

### Windows SmartScreen warning

Version 0.1.0 is **not code-signed yet**, so Windows may show "Windows protected your PC". This is expected for a new,
unsigned app. Choose **More info**, then **Run anyway**. Each release publishes a SHA-256 checksum next to the installer so
you can confirm the file is genuine.

## Uninstall

Settings > Apps > Installed apps > WorkBuddy > Uninstall. You are asked whether to keep your settings and preferences
(the default is to keep them).

## Requirements

Windows 11 (primary) or Windows 10, 64-bit. On Windows 10 the cat walks just above the taskbar instead of on it.

## License

[MIT](LICENSE). Copyright (c) 2026 Qian Tong Low.

## Development

See `docs/developer/` for the settings, startup, tray and installer verification tools, and `docs/release/` for the release plan.
