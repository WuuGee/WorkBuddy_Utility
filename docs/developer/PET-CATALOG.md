# Pet catalog and live selection (v0.2.0-A)

Built-in pets are listed by `StaticPetCatalog` (Infrastructure): `cat` (Cream Cat, default), `tuxedo` (Tuxedo Cat), `ginger` (Ginger Cat).

- `ISettingsService` is the single source of truth: `Pet.SelectedPetId` in settings.json. `IPetCatalog` is a stateless lookup; null, blank or unknown ids resolve to `cat`.
- `PetViewModel` follows `ISettingsService.Changed`, and when the resolved id differs it swaps the skin in place on the UI thread. The `PetWindow` instance, its HWND and the Win32 hooks are untouched.
- The tray has a **Pet** submenu with checkmarks; Settings > Pet has a **Companion** picker. Both write through settings.

## Verification
- `--verify-settings`: Pet Selection area (default, picker, live swap, same HWND, unknown id fallback).
- `--verify-tray`: Pet Menu area (lists pets, one checked, swap, persisted, checkmark moves, same HWND, restore). 70 checks.
- Unit tests: `PetCatalogTests.cs` (catalog, settings round trip, asset manifest and sheet integrity for every pet).

## Adding a pet
Add `Assets/Pets/<id>/` with `pet.json` (all 14 animation keys) and the sheets, then add an entry to `StaticPetCatalog`.
Tuxedo and Ginger are palette-mapped from the Cat sheets (same frames and alignment).
