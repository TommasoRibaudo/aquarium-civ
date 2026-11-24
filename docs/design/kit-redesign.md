# The Keeper's kit redesign (#668): inventory and the phase 2 build plan

Phase 1 (this file, `kit-mockups.html`, `kit-words.md`) was done without the Editor. The user picks a mock-up and signs
off the words; phase 2 builds it in the Editor. Folds in #660 (something new goes unnoticed), #661 (the tool in hand
stays visible), #662 (shut jars look closed), and the feedback#70 comment on #668 (the bottom bar looks flat).

## 1. Inventory: every kit state, from the code

All paths are under `AquariumCiv/Assets/Game/`. Line numbers are at `p9/batch3` (bb4378d).

| State | What the player sees today | Code |
|---|---|---|
| The Kit button | "Kit" in a 56 pt tool, beside History (Shape hides in a living-land box) | `UI/GameHud.cs:886-893` (built), `:168-175` (`ShowToolWord`), `:476-486` (`FollowKit`) |
| First tools only | Cloth, Mister and Tweezers as three pill buttons, a note listing them. All three show from the start (the code has no arrival for the tweezers), so "Cloth + Mister" exists only in old P8 shots | `UI/GameHud.cs:236-271` (`BuildKit`), notes `:498-502` |
| Seed | The life slot ("Life" + jar icon) shows; the seed is the first choice in its chooser (#558, #675); a toast on arrival; the Kit button gets the New tag and glow | `UI/GameHud.cs:489-497`, `:357-366`, `:742-752` (`SetShapeNew`) |
| Life ready | A terracotta dot on the Kit button (`hud-kit-dot`) and on the slot; one wiggle and the "new tool" sound, once, the next time the HUD is up | `UI/GameHud.cs:436-460` (`FollowLife`), dot USS `Glass.uss:784-785, 803-804` |
| Chooser open | The row of 140 px round choices replaces the note and hides Cloth/Mister/Tweezers (`SetLifeRow`); always opens, even for one choice (#643) | `UI/GameHud.cs:304-312`, `:330-344`, `:346-394` |
| Shut jars | The same round choice at 40 % opacity after the ready ones; a tap underlines its name and puts its need in `kit-life-note`; then the village line | `UI/GameHud.cs:377-393`, `Glass.uss:796-802`, needs `Sim/Eco/EcoModel.JarNeed.cs:29-65`, village `Sim/SimCore.LifeJar.cs:60-68` |
| A tool in hand | The sheet closes; the Kit button reads "✕ Put away" (cloth: "✓ Done") with a primary ring; a hint toast for 4 s | `UI/GameHud.cs:178-197` (`SetHand`), `Glass.uss:609-614` |
| Placing life or the seed | Glow, ghost, "Tap again..." hint while the ghost waits | `View/KitPlace.cs:83-140` (`GhostTap`), `View/LifePlaceView.cs:59, 165-189`, `View/SeedPlaceView.cs:24-25, 88-120` |

Every string the kit shows, with its file:line, is in `kit-words.md` (old → new).

### What the current look gets wrong (from the shots and the USS)

Shots in the repo: `docs/design/results/p8/polish/final/phone_07_kit.png` (P8: Cloth, Mister, note, 1179×2556),
`docs/design/results/p9/feedback3/516_chooser.jpg` (the chooser with six ready jars and the Life button, 1179×2556),
`docs/design/results/p8/polish/tester2/t2_63_kit_1080x1920.jpg`. No shot of shut jars, the seed or a tool in hand is in the
repo; phase 2 takes them (section 4).

- Four control styles in one sheet: pill buttons (tools), a pill with an icon (Life), 140 px round choices (jars), and a
  text note. Tools have no icons; jars have no line saying what they do.
- With the chooser open, the tools vanish and the Life button floats alone at the bottom with a gap above it
  (`516_chooser.jpg`). The tools and the life are never seen together.
- Shut jars break the spec's own rule for unavailable pieces: "thin solid edge, paler fill, a lock, and full-strength
  text. Never faded" (`Glass.uss:128`, spec.md §4 Button). The Shape tray already has the right look (`.kg-tile--off`,
  `Glass.uss:744`).
- The bottom bar is a flat 92 % paper tint with a hairline (`Glass.uss:602-605`): no depth (feedback#70).

## 2. What is coming into the kit in this batch

| Issue | What it adds | How the design takes it |
|---|---|---|
| #726 | Seeds and bushes earned as the civ grows, at most 3 waiting each | A count badge (×2) on the Seed and Bushes tiles; the tile stays while the count is above 0 |
| #731 | Earned bushes placed one at a time, with refusal reasons | The Bushes tile in Ready; refusals in `kit-words.md` §5 |
| #730 | One plucking tool (name open: Trowel, Shears or Spade); the tweezers stop plucking seedlings | A fourth tool tile; the tweezers' line becomes "pull weeds". Four tools fill one row of A's grid exactly |
| #724 B3 | Later seeds grow ordinary young trees; the first stays the Keeper's tree | The first seed's line is "grows your tree", later ones "a young tree" |

## 3. The three mock-ups

`kit-mockups.html` draws each in every state at 375×667 and 393×852 (add `?state=s5` to the URL for one state).

- **A, Tile shelf ★:** square tiles (the Shape tray's tile, radius 16) in a 4-wide grid under three small labels: Tools,
  Ready, Still to come. One note at the bottom. Nothing hides behind the jar: two taps to place life. Up to about 60 % of a
  short phone when everything is open.
- **B, Coin row:** today's layout made one family. Round coins with a word under each; a Life jar coin opens its contents
  in place, ready ones, a divider, then shut ones; "‹ Tools" goes back. About 30 % (45 % with the jar open).
- **C, Rail:** no sheet. A glass rail of pills rises above the bar; the jar pill unfolds a second rail; a caption carries
  the note. The most land stays in view, but only names fit and the rail scrolls past five pills.

All three share the tile family (available, in hand, new, ready with a count, shut), the #661 bar chip
("[icon] The rabbits · ✕ Put away"), the #660 dot ("Kit ·" with a soft pulse) and toast, and a bevelled bottom bar.

## 4. Phase 2: the build plan (Editor, lane B)

### Shared by every mock-up

1. **`UI/Glass/KitTile.cs` (new):** one control. Icon, name, a line under it, a count badge, a lock badge, the New tag,
   the dot. States as classes: `kit-tile--new`, `--hand`, `--shut`, `--picked`, and a shape class (`--square`, `--coin`,
   `--pill`). Built on `GlassButton` so taps, sound and `GlassReach` stay as they are. Add it to `GlassGallery.cs` in every
   state (the tester opens the gallery).
2. **Icons:** `Resources/GameUI/Icons/cloth.svg`, `mister.svg`, `tweezers.svg`, `trowel.svg` (paths are in the mock-up's
   `<defs>`, white stroke like the others) and `.kg-i-*` rules in `Glass.uss`. Log them in `docs/assets-log.md` as drawn
   for the project. The user signs off the icons.
3. **The bar (#661, feedback#70), `UI/GameHud.cs:168-197, 884-895`:** `ShowToolWord` builds the hand chip: an icon disc,
   the name ("Mister", "The rabbits"), and a Put away pill (cloth: "Done" with the check). The tool widens to about 66 %
   while held. Add `public string KitWord` ("Mister · Put away") for tests. Depth in USS: UI Toolkit has no box-shadow
   or gradient, so use a 9-sliced bevel image (like `GlassShadow.png` under the sheets) behind each tool, a darker bottom
   border, and in A and C a ledge element behind `hud-bottom` (flat tint 38 %, brass-light top edge).
4. **#660, `FollowLife` `:436-460`:** a toast once per opened jar ("The rabbits are ready in your kit.") on the
   quick question's calm-moment rules (not the first run, no sheet, no moment); the dot gets a slow pulse (a scheduled
   class toggle in `GlassMotion`) and "Kit ·"; the wiggle repeats once per visit while the jar waits (track the visit, not
   `AckJarReady` alone).
5. **#662:** the shut look is `kit-tile--shut` (off-fill, timber edge, lock, full-strength words; delete the 40 % opacity
   at `Glass.uss:798-800`); Ready and Still to come are apart with labels; a shut tile shows its short need without a tap.
6. **Words:** apply `kit-words.md` once signed off. Several constants are asserted by tests (`MisterHint`, `SeedHint`,
   `PutAway`, `JarNeed` lines): change the constant, the tests read it.
7. **#726 / #730 hooks:** the Seed and Bushes tiles read the pouch counts and the trowel tile shows once `KitTool` has
   the new tool. If those issues are not merged when phase 2 starts, the tiles hide the count at 0 and skip the trowel;
   nothing else waits on them.
8. **Debug panel (one block, `View/SimDebugPanel.cs` near `:378`): "Kit states (#668)":** first tools only, seed
   waiting, one group ready (fires the #660 toast), full kit (all jars ready + some shut + 2 seeds + 1 bush), and each
   tool in hand. It reuses `DebugJarsReady`, `DebugLockJars` and #726's give seeds / give bushes.
9. **Shots:** a `KitShots` PlayMode class (explicit, like `Fb10Shots`) that sets each state from the cheats and captures
   it at 750×1334 and 1179×2556: first tools, seed, life ready (sheet shut and open), the full kit, each tool in hand, the
   chooser with 1, 2 and 4 ready and shut jars (#662's list), the bar with the longest name.

### Per mock-up

| | A: Tile shelf ★ | B: Coin row | C: Rail |
|---|---|---|---|
| `GameHud.BuildKit` `:236-271` | Rewrite: three labelled flex-wrap rows of square tiles (UI Toolkit has no grid and the USS bans `:nth-child`, so each tile gets a position class for its right margin), one `kit-note` | Keep the structure; tools and the jar coin become `KitTile`s in one row | Replace the sheet with two rail elements in `safe`, above `hud-bottom` |
| Life slot and chooser `:274-394` | The `kit-life` button and the row toggle go; `FillLifeRow` becomes `FillJars`, filling Ready and Still to come each time its key changes; `SetLifeRow` goes | `kit-life` stays as the jar coin; `FillLifeRow` adds the divider, the labels and the "‹ Tools" back | `kit-life` becomes the jar pill; the row becomes the second rail |
| `Open(Kit)` / `CloseSheet` `:1041-1069` | No change | No change | Large: the kit is no longer a `GlassScrim`, so `OpenSheet == Kit`, `SheetChanged`, `PlaceToast` and every `SheetOpen` check in View (`GlassVoice.cs:313`, `CareView.cs:722`) need a rail case |
| `Glass.uss:771-804` | New `.kit-tile--square`, `.kit-sec`, `.kit-shelf`; the old `.kit-life-*` rules go | Restyle `.kit-life-choice` into `.kit-tile--coin` | New `.kit-rail`, `.kit-tile--pill`, `.kit-caption` |
| Test churn | Medium: no "open the jar" tap any more | Small: names and taps stay | Large: no kit sheet |
| Hours [est] | 6 to 8 | 4 to 6 | 8 to 10 |

### Element names the tests use (keep them)

`hud-clean`, `hud-kit-dot`, `kit-scrim`, `kit-sheet`, `kit-cloth`, `kit-mister`, `kit-tweezers`, `kit-life`,
`kit-life-row`, `kit-life-seed`, `kit-life-<species>`, `kit-life-locked-<species>`, `kit-life-note`, `kit-life-dot`.
In A, `kit-life` and `kit-life-row` no longer exist: add a `PlayTestBase` helper `TakeFromKit(string name)` (opens the
kit, taps the jar first in B and C, then the tile) and move every test to it, so a later layout change touches one place.

### Tests

Named in #668's Done when: `LifeSlotPlayTests`, `SeedGhostPlayTests`, `SlicePlaythroughSmoke` (category `SmokePlay`).
Also touch the kit and need updating with the helper or `KitWord`: `EcoPhonePlaythrough` (`:152, 160, 191` read the
Kit button's text), `Feedback3Playthrough` (`:238, 344, 382` read Put away), `Feedback2Playthrough`, `Feedback4Playthrough`,
`Feedback5Playthrough` (`LifeNoteText`), `Playtest1BPlaythrough` (`:321, 1108` read the note), `Tester2FixPlayTests`
(`:209-232` the mister hint), `TweezersPlayTests`, `TelemetryPlayTests`, `FeedbackPillPlayTests`, `GhostFramePlayTests`,
`Fb10Shots`, `NightShots`, `OpenQuestionsSignOff`. Sim tests that pin need lines: `JarNeedTests`, `DuckJarIceTests`,
`PlacedLifeTests` (only the changed "Bushes need your tree planted first" line moves). New: a #660 test (a jar opens →
one toast, the dot pulses, a wiggle on the next visit) and a #662 test (shut tiles carry the lock class, never opacity,
and sit under Still to come). Run them bunched by assembly, then the tester sub-agent before sign-off.

## 5. For the user

1. Pick A, B or C (★ A).
2. Sign off the words (`kit-words.md`), including the trowel's name and "your tree".
3. Sign off the four new icons (cloth, mister, tweezers, trowel) as drawn in the mock-up.
