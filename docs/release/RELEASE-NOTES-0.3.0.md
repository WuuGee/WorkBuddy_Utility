# WorkBuddy v0.3.0 (Progression and accessories)

## Highlights

- **Passive XP & Levels:** your companion earns XP quietly while it is visible (+1 XP per minute, plus a small capped bonus for petting and dragging). No dialogs or toasts; a level-up plays the Happy animation and, if messages are on, a short speech bubble. Max level is 50. Progress is stored in `progression.json`.
- **Level-Gated Pet Unlocks:** Cream Cat (Lv. 1), Tuxedo Cat (Lv. 2) and Ginger Cat (Lv. 3). Locked pets show as "Name (Unlocks at Lv. N)" in the tray and Settings, and unlock live.
- **8 Quiet Achievements:** First Steps, Full House, Trusted Friend, Old Friends, Gentle Pats, First Hour, Workday Buddy and Long Haul.
- **3 Unlockable Pet Accessories:** Red Bowtie, Party Hat and Star Halo, drawn on the pet and aligned per animation frame.

## Install

1. Download `WorkBuddy-Setup-0.3.0.exe`.
2. Double-click it. No administrator rights, Visual Studio or .NET are needed.

Installing over v0.2.x upgrades in place and keeps your settings and startup choice.
Installs to `%LOCALAPPDATA%\Programs\WorkBuddy`.

## SmartScreen

This release is not code-signed, so Windows may warn. Choose **More info**, then **Run anyway**.
Verify the download against `WorkBuddy-Setup-0.3.0.exe.sha256` (also recorded in `docs/release/CHECKSUMS.md`).

## Known limitations

- Installer is unsigned.
- There is no progression reset button; delete `progression.json` to start over.
- Updates are manual to install; the app only tells you when one exists.

## License

MIT. Copyright (c) 2026 Qian Tong Low.
