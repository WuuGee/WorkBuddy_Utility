# Companion progression (v0.3.0-A)

WorkBuddy earns XP quietly while it keeps you company. There is no dialog and no toast. A level-up plays the Happy animation and, if messages are enabled, a short speech bubble.

## Rules

| Item | Value |
|---|---|
| Total XP to reach level n | `25 * (n - 1) * n` (level n to n + 1 costs `50 * n`) |
| Max level | 50; XP and time keep accumulating, no further level-ups |
| Companionship tick | every 1 minute, +1 XP and +1 minute, only while the pet window is visible |
| Interaction bonus | +2 XP on drag/click, 30 s cooldown, 30 XP per UTC day |
| Scope | Global across all pets |

There is no reset button. Delete `progression.json` to start over.

## Layout

- `Domain\Progression`: `LevelCurve`, `ProgressionState`, `XpGrantPolicy`. Pure, no clock, no I/O.
- `Application\Progression`: `IProgressionStore`, `IProgressionService`, `ProgressionService` (injected `TimeProvider`).
- `Infrastructure\JsonProgressionStore`: `progression.json`, atomic write, corrupt file set aside.
- `Presentation\Services\ProgressionCoordinator`: 1-minute `DispatcherTimer` and the level-up reaction.
- `PetViewModel.BeginDrag` records interactions.
- Settings > General shows level, XP bar and time together. The tray shows `Companion: Lv. 1 (0 / 50 XP)`, or `Lv. N (MAX)`.

## Storage

Progression lives in `progression.json`, separate from `settings.json`, so ticks never raise `ISettingsService.Changed` (which would re-run themes, tray and pet skin). The level is derived from `TotalXp`, never stored.

Saves are debounced: every 5 minutes, immediately on level-up, and on `Flush`/`Dispose` (called in `App.OnExit`), so a clean exit loses nothing.

## Lifetime

`SettingsViewModel` subscribes to `IProgressionService.Changed` and unsubscribes in `Dispose` (called from `SettingsWindow.Closed`), so reopening Settings does not leak handlers on the singleton service.

## Verification

- Unit tests: `tests\FocusCatAI.Application.Tests\Progression`.
- `--verify-settings`: Progression area (tick, no settings `Changed`, UI card, separate file).
- `--verify-tray`: Companion line position, read-only, format.

## v0.3.0-B: Pet unlocks and quiet achievements

- Pet unlocks are derived, not stored: PetUnlockPolicy.IsUnlocked = level >= RequiredLevel (cat 1, tuxedo 2, ginger 3). No granted-pets list.
- A locked stored SelectedPetId falls back to the default pet at runtime (logged once); settings.json is never rewritten.
- Tray Pet submenu and Settings picker show locked pets disabled as 'Name (Unlocks at Lv. N)' and unlock live.
- Eight achievements in AchievementCatalog: first_steps, full_house, trusted_friend, old_friends, gentle_pats, first_hour, workday_buddy, long_haul. Ids are stored in progression.json (schema v2).
- Silent catch-up at load: no event, no animation.
- A level-up and achievements from one grant are coalesced into one Celebrate() and one bubble; silent when the pet is hidden.
- Verifiers use a scratch progression folder (VerifierScratch).

## v0.3.0-C: Accessories

- Accessories are cosmetic overlays: bowtie, party_hat, star_halo (plus "none"). `AccessoryDefinition.Id` equals the PNG file name under `Assets\Accessories`.
- Unlocks are derived like pets: `AccessoryUnlockPolicy.IsUnlocked` = level >= `RequiredLevel`. Nothing is stored.
- A locked or unknown stored accessory id falls back to "none" at runtime (`AccessorySelection.Effective`); settings.json is never rewritten.
- Locked accessories show as 'Name (Unlocks at Lv. N)' via `AccessoryUnlockPolicy.DisplayLabel`.
- Placement: `Assets\Accessories\anchors.json` holds per-animation, per-frame head/neck anchors (`hx/hy`, `nx/ny`, 64px frames), applied by `AccessoryRenderer`.
- Catalog: `IAccessoryCatalog` / `StaticAccessoryCatalog`. Tests: `tests\FocusCatAI.Application.Tests\Accessories`.

