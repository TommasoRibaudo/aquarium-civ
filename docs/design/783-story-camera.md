# #783: the camera and a held story scene

Status: **spec, waiting for the user's sign-off** (2026-11-25). No Unity was run for this file. Every number is [est] until a
screenshot at 750x1334 has been looked at.

## The problem
A held moment (a #722 story, a wedding, a ritual) plays where the people are, with no camera move. At the default view a doll is
~55 px on a 2556-px-tall phone, so on a 750x1334 phone it is **~29 px**: the caption reads, the scene is a speck, and the player
has to zoom in by hand to see what the caption is about. Seen, not read: the scene is the point.

## Rules this has to sit inside
- #672 / feedback #34: a camera the player set stays where they set it; whatever moves it eases **back to the player's own view**,
  never to home. #560: a glide back after a fly-in.
- 2026-11-08 (P9 arrivals): the camera **leans in** for placed life and eases back; a two-finger move or a pan cancels the lean.
- 2026-11-08 (#426): no lean for the hunt, "the player zooms in to watch". This spec does not reopen that; hunts stay out.
- 2026-11-14 (#673): photo mode never jumps the camera by itself.

## Options
**A (recommended, ★) The camera leans in at a held scene's start, and eases back after.** Reuses the lean the arrivals already
have. Nothing to tap. The player can always take the camera.

**B A "Show me" button on the caption.** The player taps, the camera frames the scene (ShowMe's fly-to, with its saved view and
return). Safe against fighting the player, but the scene is already part over when a tap lands (scenes are 10-20 s), and it is one
more button on a caption that is meant to be watched, not operated.

**C Nothing moves; the caption pulses a small marker and the player zooms.** Gentlest, but it is today's problem with a hint: the
player still has to pinch in on a 29 px scene they are trying to watch.

## ★ A: exact behaviour
**When.** When `HeldMomentStage` starts a staging (the `StagedMoments` kinds and Story, Wedding, Callback) and
`GlassVoice.CameraFree()` is true (no sheet, card, wipe, tool, photo mode, Show me: the gate the nudges use). Not for hunts (#426),
not for the box-wide festival (no focus), not for echoes with no cast drawn.

**Where.** `CampLife.FocusNow()` (the scene's thing pulled toward its lead actor), re-read each second while the lean holds so a
walking scene stays framed. A scene whose cast already sits inside the frame at the target zoom does not move the camera.

**How close.** The zoom at which a standing doll is `ScenePx` = **5 % of the screen's height** (67 px on 1334, 128 px on 2556: the
same share on every phone, the CampFrame rule). Solved like `StoryEventShots.Frame`: doll height `CampFrame.DollHeight` x
`CameraReach.DollScale(zoom)` projected, then `zoom = zoom_now x px_now / ScenePx`. Widened if the cast does not fit the middle 80 %
across and 70 % up. **Never zooms out**: `LeanIn` takes the smaller of the player's zoom and the target, so a player already closer
keeps their zoom and only the pan moves (and not even that if the scene is framed). Floor: the player's own zoom floor
(`CameraReach.MinZoom`); the lean's 0.15 floor stays for rabbits, ducks and fish only.

**How fast.** `BoxCamera.LeanSeconds` (1.4 s) in, smoother-step. Pitch is raised only as far as the lean already does
(`LeanPitch` 52 at most); **yaw is never touched** (a yaw move is what makes people feel the room turn). The scene's first beat waits
up to 1.0 s so the camera arrives before the action. Return: `GlideBack` (1.2 s wait, 1.1 s ease) to **the pose saved at the lean's
start**, the player's own (`ReturnAfterFlight`, as the arrivals do).

**The player always wins.** A two-finger move, a pan drag, a scroll or a pinch cancels the lean at once (`LeanCancels`); nothing
returns the camera afterwards (the player has taken it) and **no second lean is offered for that scene**. Following a person or camera
mode (`GlideHeld`) holds the return until it ends, as today. A one-finger tap does not cancel (the life tool's taps).

**Two moments queued.** `HeldMomentStage` plays one at a time, so the second starts after the first ends. The glide-back is armed
with the first lean's saved pose and the second lean chains on it (`LeanIn` keeps the first lean's way home), so the camera goes scene
to scene without bouncing back to the player's view between. If the glide has already begun, the second leans from where the camera
is; the way home is still the first saved pose. A 20 s game-time cooldown between two leans keeps a busy day from feeling like a tour.

**Home view versus already zoomed.** From home (the camp frame, ~29 px on 1334): leans to ~67 px, about 2.3x closer. Already zoomed on
the scene: nothing moves. Zoomed in on somebody else: pans over (1.4 s) at the player's zoom, then back. Zoomed out wide: leans in,
back out after (the one big move). No hint text: the move explains itself.

**The glass.** The near pane and its frame are already gone at the camp frame (`NearGlassFade.CampCloseness`), and a closer camera
only adds to that, so no rim appears in front of the scene. Far, side and back panes are unchanged. A scene at the very front edge
(kids at the pond glass) keeps today's fade; nothing new.

## Code it reuses
`BoxCamera.LeanIn(world, zoom, hold)` and `ReleaseLean` (arrivals, `NewStartView`), `GlideBack`, `GlassVoice.CameraFree`,
`CampLife.FocusNow`, `CampFrame.DollHeight` + `CameraReach.DollScale` for the zoom solve (lift `StoryEventShots.Frame`'s maths into a
pure `StoryLean.ZoomFor(dollPx, screenH, zoomNow, castExtent)` that dotnet test can cover). New: a `HeldMomentStage` hook at a
staging's start, and a `HoldLean` that is true until the staging is Done. No new gesture, no new UI.

## Risks
- **Motion sickness, being pulled.** One slow ease, no yaw, no shake, never zooms out, one lean per scene, the 20 s cooldown.
- **Fighting the player.** Any camera gesture cancels and the scene is then left alone; never starts while a finger is moving the camera
  (`LeanIn` refuses); never while following or in camera mode.
- **Losing the player's place.** Returns to the saved pose, not home (#672); after a cancel, no return.
- **Scenes that walk off.** `FocusNow` follows the lead only to `followReach`; a scene that leaves the frame ends the lean early.
- **Test flake.** PlayMode tests that count dolls at the default view need the lean switched off (a static flag, as the arrivals have).
- **Needs the user's call:** every held scene (the issue's "every held moment", the ★), or only #722 stories?

## Tests and screenshots
- Pure (dotnet): `ZoomFor` gives the same px share at 750x1334, 1179x2556 and a tablet; never above the player's zoom.
- PlayMode: zoom in on a person, play a story: the camera does not zoom out and returns to that view. A two-finger move mid-lean:
  `LeanCancels == 1`, no return. Two queued stories: one way home. A card open: no lean.
- Screenshots at 750x1334 (`unity command screenshot --view game`), read and measured: **before** (today, default view, a held story
  mid-scene: doll ~29 px), **after** (same story, lean landed: doll ~67 px, caption not covering the cast), **after the return** (back
  at the default view).
- `SlicePlaythroughSmoke` and the `tester` pass, since this touches every held moment.
