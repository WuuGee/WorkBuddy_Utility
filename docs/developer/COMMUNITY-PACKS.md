# Community Packs (v0.5.0)

## Custom pets
Folder: `%LOCALAPPDATA%\WorkBuddy\Pets`. Each pack is a subfolder whose name is the pet id and which contains a `pet.json` manifest plus the sprite sheets it references (an `Idle` animation is required). Invalid packs are skipped silently, and ids that collide with built-in pets (default, tuxedo, ginger) are ignored. Built-in pets always come first in the picker.

## Custom themes
Folder: `%LOCALAPPDATA%\WorkBuddy\Themes`. A theme is a JSON file of brush colors. It is validated on load; invalid or unknown entries are ignored and the built-in theme is used, so a bad pack can never break the app.

## Settings buttons
Settings > Pet has **Open Custom Pets Folder** (`OpenCustomPetsButton`) and **Open Custom Themes Folder** (`OpenCustomThemesButton`). They create the folder if needed and open it in Explorer. A summary line (`CommunityPacksText`) shows the pack counts.

## Verification
`--verify-settings` includes a Community Packs area. It checks that both buttons exist and tests discovery/validation in an isolated temp directory, cleaning up in `finally` so the real folders stay untouched and `--verify-tray` still sees exactly 3 built-in pets.
