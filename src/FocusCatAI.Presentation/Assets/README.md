# Pet Asset Specification (v1)

A pet is a folder under `Assets/Pets/<id>/` containing a `pet.json` manifest and one PNG sprite sheet per animation. The app loads pets from configuration (`appsettings.json` ? `Pet:PetId`), so adding or replacing a pet needs no code changes.

```
Assets/
  Pets/
    Cat/        pet.json + idle.png, walk.png, ...
    Dog/        (future)
    Capybara/   (future)
    Panda/      (future)
  Skins/        (future) recolors/overrides of a pet
  Accessories/  (future) overlays (hats, scarves)
```

Folder name must equal the `Pet:PetId` value (case-insensitive). If a pet fails to load, the app logs the error and falls back to `Pet:FallbackPetId`.

## Sprite sheets

- PNG with a transparent background.
- A horizontal strip: frames side by side, left to right, each `frameWidth` x `frameHeight` (default 64x64).
- Pixel art: drawn at native size. The app scales with nearest-neighbor, so keep art crisp.
- The pet **faces right**. Left-facing is produced with `"flipHorizontal": true`.
- Leave `groundOffset` transparent rows at the bottom of the frame if the feet are not at the very bottom. Feet are aligned to the taskbar's bottom edge.
- Style guide for the default cat: chibi proportions, cream and orange fur, 1 px black outline.

## pet.json

```json
{
  "schemaVersion": 1,
  "id": "cat",
  "displayName": "Cream Cat",
  "frameWidth": 64,
  "frameHeight": 64,
  "groundOffset": 10,
  "walkSpeed": 36,
  "animations": {
    "Idle":      { "sheet": "idle.png", "frames": 4, "fps": 5, "loop": true, "tags": ["idle", "observe"] },
    "WalkLeft":  { "sheet": "walk.png", "frames": 8, "fps": 10, "loop": true, "flipHorizontal": true }
  }
}
```

| Field | Meaning |
|---|---|
| `frameWidth`, `frameHeight` | Size of one frame in pixels. |
| `groundOffset` | Transparent pixel rows below the feet (at native size). |
| `walkSpeed` | DIPs per second at 64 px. Scales with pet size. |
| `animations.<Name>` | Animation key (see below). |
| `sheet`, `frames`, `fps`, `loop` | Sheet file, frame count, speed, looping. |
| `flipHorizontal` | Mirror the sheet (lets WalkLeft reuse the walk strip). |
| `tags` | Behavior hooks (see below). Optional. |

### Required animations

| Key | Frames (default) | Used for |
|---|---|---|
| `Idle` | 4 | Standing (**mandatory**, used as fallback for any missing animation) |
| `WalkRight`, `WalkLeft` | 8 | Moving along the taskbar |
| `Sit` | 4 | Sitting, focus sessions |
| `Sleep` | 4 | Sleeping |
| `Stretch` | 4 | Occasional stretch |
| `Think` | 4 | Chat / thinking |
| `Speak` | 4 | Showing a message |
| `Happy` | 4 | Celebration, breaks, playing |

### Adding new animations without code changes

Animation keys are free-form strings. Add an entry and give it **tags**; the behavior engine picks randomly among all animations that carry the tag it wants:

| Tag | Picked when the pet... |
|---|---|
| `idle` | stands still |
| `sit` | sits (roaming stops, focus, following) |
| `stretch` | occasionally stretches |
| `sleep` | sleeps (Sleepy mood) |
| `observe` | looks at taskbar icons |
| `play` | plays (Happy mood, breaks) |

Example: add `"Groom": { "sheet": "groom.png", "frames": 6, "fps": 6, "tags": ["sit"] }` and the pet will occasionally groom while sitting. Same for yawn, scratch, jump, lay down, etc.

## Pet size

`Pet:Size` = `Small` (32), `Normal` (64), `Large` (96), `Huge` (128). In taskbar mode the size is capped to the taskbar height.

## Creating the default placeholders

`tools/GenerateCatSprites.ps1` regenerates the placeholder cat sheets.

## Built-in pets (v0.2.0-A)

Built-in pets are listed in StaticPetCatalog: Cat (id cat, default), Tuxedo (id tuxedo) and Ginger (id ginger). A new pet needs a folder named after its id, a pet.json with all 14 animation keys, and a catalog entry. The selection is stored as Pet.SelectedPetId; unknown ids fall back to cat.
