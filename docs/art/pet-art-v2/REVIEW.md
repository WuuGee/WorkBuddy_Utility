# Pet Art V2 - Review Report

Status: art regenerated and running; **visual sign-off pending (owner review required)**.

## What changed
- `tools/Generate-CatSprites.ps1` rebuilt (the earlier copy was deleted by mistake). It now draws procedural
  shapes (ellipses, polygons, tapered chains) with a 2px dark outline plus a faint light rim so the outline
  survives a dark taskbar. V1 hand-placed 32x32 pixels upscaled 2x; V2 draws directly on the 64x64 frame.
- `tools/Render-ArtPreview.ps1` renders V1 vs V2 at 32 / 44 / 48 px on light and dark backgrounds:
  `docs/art/pet-art-v2/comparison.png`.
- V1 sheets and manifest are kept in `docs/art/pet-art-v1/`.
- `pet.json` is unchanged. Same animation names, sheet names, frame counts and 64x64 frames. No engine change.

## Measured (sit pose, frame 0, `-Metrics`)
| Part | V2 | Target |
|---|---|---|
| Head (without ears) | 45% of height | 40-45% |
| Body | 41% | 40-45% |
| Tail | 50% (visible length of an upright, curled tail) | 10-15% |

The tail is **outside the target**. It is a tall upright tail, as in the reference direction, so its share of
height is far above 10-15%. I did not shrink it further because that would lose the upright-tail silhouette.
Treat the tail target as needing your decision.

## Redesign by animation
- Idle / Sit: oversized round head with cheeks, ears with pink inner, cream muzzle, blush, large glossy eyes,
  orange cap and body patch on cream, upright tail with an orange tip, visible paws.
- Walk: 4 legs on a sine phase (far legs shaded), foot lift, body bounce at 2x step rate, tail sway, small head bob.
- Blink: open, half-lid, closed, open, with a slight head dip.
- TailFlick: tail swing +/-6px with an ear twitch.
- LookLeft / LookRight: face shifts and eye spacing narrows (head turn illusion), head also moves.
- Groom: raised paw to the face, eyes closed, tongue visible on frames 3-6, head tilt.
- Stretch: 8 frames, rear raised, front lowered, body tilt up to 16 degrees, yawn on the peak frames.
- Thinking: head tilted up and to the side, ear droop, growing thought dots.
- Happy: jump of 4px, happy-arc eyes, open mouth, tail wag, rising heart.
- Sleep: new curled posture (wide low body, head tucked on forepaws, tail wrapped close), breathing as a
  body height change over 6 frames, soft tail twitch on one frame, rising Z letters.

## Verified by tooling (no visual judgement)
- All 13 sheets have the right pixel size, and every frame is inside 64x64 (no edge clipping after fixes).
- Feet touch row 63 in every sheet (groundOffset 0 still correct).
- App built and ran: 14 of 14 animations loaded at 64x64, pet window 44x44 inside the taskbar.
- 60 of 60 unit tests pass.

## NOT verified
- **Cuteness / appearance.** I cannot view images. Please open `comparison.png` and look at the cat
  on the real taskbar. Judgements about "Aww" factor, face readability at 44px and light/dark contrast are yours.
- Whether the sleep silhouette reads clearly as "sleeping" and whether the walk reads clearly at 32px.

## Side finding (z-order)
At launch (15:01:17) the fallback poll corrected the pet once (petZ 6 vs trayZ 5), with 0 ZOrderGuard
corrections. This is the first recorded fallback activation, so the polling fallback should stay for now.

## Recommendation
Pet Art V2: **implemented, awaiting owner visual review**. Not marked complete.
