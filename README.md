# WorkBuddy

WorkBuddy is a small Windows desktop companion: a virtual cat that lives on your taskbar, keeps you company while you
work, and gives gentle, rare reminders. It runs locally, needs no account, and sends no personal data anywhere.

## Get WorkBuddy

1. Open the [Releases page](https://github.com/WuuGee/WorkBuddy_Utility/releases) and pick the latest version.
2. Under **Assets**, download `WorkBuddy-Setup-0.1.0.exe` (about 47 MB). You do not need the "Source code" files.
3. Double-click the downloaded file and follow the steps. No administrator rights are needed, and .NET does not have to be installed.
4. Leave **Start WorkBuddy when Windows starts** ticked to have the cat appear every time you sign in. You can change this later in Settings.
5. Tick **Launch WorkBuddy** on the last page, and the cat appears.

WorkBuddy installs to `%LOCALAPPDATA%\Programs\WorkBuddy`; your settings live in `%LOCALAPPDATA%\WorkBuddy`.

### Windows SmartScreen warning

Version 0.1.0 is **not code-signed yet**, so Windows may show "Windows protected your PC". This is expected for a new,
unsigned app. Choose **More info**, then **Run anyway**. Each release publishes a SHA-256 checksum next to the installer so
you can confirm the file is genuine. To check it, run this in PowerShell and compare the result with the `.sha256` file:

```powershell
Get-FileHash .\WorkBuddy-Setup-0.1.0.exe -Algorithm SHA256
```

## Using WorkBuddy

Right-click the cat's tray icon (near the clock) for Show Cat, Hide Cat, Settings, Restart, About, Check for Updates and Exit.
Updates are manual: use **Check for Updates** to see whether a newer version is available.

## Uninstall

Settings > Apps > Installed apps > WorkBuddy > Uninstall. You are asked whether to keep your settings and preferences
(the default is to keep them).

## Requirements

Windows 11 (primary) or Windows 10, 64-bit. On Windows 10 the cat walks just above the taskbar instead of on it.

## Build it yourself

Needs the .NET 8 SDK and [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
git clone https://github.com/WuuGee/WorkBuddy_Utility
cd WorkBuddy_Utility
powershell -ExecutionPolicy Bypass -File installer\Package-Release.ps1
```

The installer, checksum, license and release notes are written to `artifacts\release\`.

## License

[MIT](LICENSE). Copyright (c) 2026 Qian Tong Low.

## Development

See `docs/developer/` for the settings, startup, tray and installer verification tools, and `docs/release/` for the release plan.
