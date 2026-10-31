# One-handed controls: a Clean button

Status: **direction chosen, not built** (2026-10-29). Build after the overnight P3–P6 run lands, since it touches
`OrbitCamera.cs` and `WipeCleaner.cs`.

## Problem
The user played one-handed and it felt uncomfortable. Since P1, every one-finger drag that starts on the tank (glass,
or the world through the open top) is a wipe (`WipeCleaner.BeginDrag`), even on clean glass. In portrait the tank fills
most of the screen, so one finger can only turn the tank from the thin strip beside it. Everything else (rotate, zoom)
needs two fingers, so one-handed play doesn't work.

## Options considered
| Option | Verdict |
|--------|---------|
| **Clean button**: one finger always moves; cleaning is a mode you switch on | **Chosen** (user preference: movement stays unaffected) |
| Dirt-aware claim: wipe only if the stroke starts on grime or a mess piece, otherwise rotate | Fallback if the mode feels clunky. Loses wiping clean glass for fun; strokes starting on clean glass rotate |
| Turntable base: drag the plinth under the tank to spin it | Possible add-on; gives only yaw and needs screen space |
| Hold-to-wipe (press ~150 ms, then drag) | No: slows down the gesture P1 loved |
| Edge swipes rotate | No: clash with the OS back gesture and the safe area |

## The design
- **Default (no mode):** one-finger drag rotates, tap selects/inspects, double-tap on empty space resets the view.
  Two fingers rotate + pinch zoom as today.
- **Clean button** in the thumb zone (lower corner, inside the safe area, ≥ 48 dp). Tap toggles **clean mode**:
  one-finger drags wipe the glass and flick junk, exactly like the P1 wipe. Two fingers still rotate and zoom.
  Taps still select people and buildings.
- **Mode is obvious:** the button is lit and the finger shows a cloth (or the glass gets a faint sheen) while clean
  mode is on. No silent mode errors.
- **Leaving the mode:** tap the button again; also auto-exit after a few seconds with no wipe, or when the tank is
  spotless (tune by playing).
- **One-handed zoom:** double-tap and hold, then drag up/down (the maps gesture), besides pinch and tap-to-focus.

## Trade-offs to watch
- Costs one extra tap per clean, and loses the "just touch the glass" immediacy of P1. Check that the wipe still feels
  as good once you're in the mode.
- A glowing button when the tank is dirty would be a HUD signal (pillar 5: the glass is the interface). Keep the
  button a plain tool; the grime itself says "clean me".
- This changes the touch rule in `game-design-practices.md` §1 ("drag = rotate or wipe" becomes "drag = rotate;
  drag in clean mode = wipe").

## Build notes
- `OrbitCamera.DragTool` only gets the drag while clean mode is on. Outside the mode, one finger always rotates.
- The button registers with `GuiBlocker` so pressing it never rotates or wipes.
- Debug panel: a clean-mode toggle cheat (CLAUDE.md rule).
- Log `clean_mode` on/off (with the reason: button / auto-exit / spotless) and strokes per mode session, so lab data
  shows whether the extra step hurts cleaning.
- Tests: one-finger drag rotates with the mode off and wipes with it on; two fingers never wipe; taps work in both.
- Run the `tester` sub-agent with a one-handed brief (thumb-only, small and tall phones).

## Open questions
- Auto-exit rule (timeout length, spotless exit).
- Left-handed placement: mirror option or a centred button?
- On return to a dirty tank (check-in: chronicle → clean → nudge), should clean mode start on?
