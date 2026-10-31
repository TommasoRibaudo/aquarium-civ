# UI limits: what UI Toolkit can draw over the tank (#164, 2026-10-31)

Unity 6000.3.27f1, URP, GamePanel settings, a throwaway panel over the live Slice scene at 1179x2556. Every verdict comes
from something rendered and looked at, unless the note says "doc" or "not tested". Evidence is in [limits/](limits/).
USS px = panel units, 1080 wide: 1 pt on a 393 pt phone is 2.75 px, so 44 pt = 121 px.

**Phone cost is not measured.** No device was used. "Free" and "costly" below are Editor readings on a desktop GPU, good
for ranking effects and nothing else.

| Verdict | Means |
|---|---|
| **free** | No measurable frame change in the Editor, no added SetPass |
| **costly** | Works, adds off-screen passes or SetPass calls every frame. Use sparingly |
| **not possible** | USS refuses it, and no route was found |

| Effect | Verdict | How | Evidence | Note |
|---|---|---|---|---|
| Custom font, SDF | free | Font asset from a TTF, set by `-unity-font-definition` | 01-font | Lora and PT Serif sharp at 33, 38 and 47 px. Dynamic and static atlas look the same |
| Real bold and italic | free | One font file per style, a class per style | 01-font | Works. Each file costs size and atlas |
| Faked bold and italic | free | `-unity-font-style` or `<b>`, `<i>` on a regular file | 01-font | Fake bold reads well. Fake italic is a slant, not a real italic. A variable font gives its default weight only |
| Font fallback | unproven | `fallbackFontAssetTable` | 01-font | Greek, Japanese and ✓ drew with no table at all, source unknown, on Windows. ⟳ was a box on every line. Check on a phone |
| Drop shadow, `box-shadow`, `filter: drop-shadow` | not possible | USS import warns and ignores them | import log | Use one of the next four |
| Soft shadow, 9-slice | free | Soft PNG behind the panel, `-unity-slice-*` | 02-shadows | **Default choice.** Fixed softness per texture |
| Hard offset shadow | free | A plain offset element | 02-shadows | Good for a flat, cut-paper look |
| Soft shadow, blurred sibling | costly | Dark element behind, `filter: blur(18px)` | 02-shadows, 02b | Must be larger than the panel (16 px wider each side, 34 px lower here). At panel size it barely shows |
| Soft shadow, UI Shader Graph | free | `-unity-material` on a shadow element | 02-shadows | Works, one extra SetPass. Size is hardcoded in the graph ([SoftShadow.shadergraph.txt](limits/SoftShadow.shadergraph.txt)), each size needs a material value |
| Soft shadow, custom filter | costly | `FilterFunctionDefinition`, hand HLSL | 02-shadows | Works on the panel itself. Needs a shader in the build |
| Backdrop blur in USS | not possible | `backdrop-filter` is unknown; `filter: blur` blurs only the element's own subtree | 03-blur (top panel) | Confirms the community report |
| Tinted scrim | free | `rgba()` background | 03-blur | Tank stays sharp behind it |
| **Frosted glass over the moving tank** | costly | URP pass copies the frame to 1/4 size, panel shows it, `filter: blur(14px)` in the panel, tint on top | 03-blur, 03-blur-later | **Deliverable and looks right.** Needs runtime code. 1/8 copy + `blur(6px)` (03-blur-eighth-filter) is close. **Phone verdict pending: run the frost test, see below (#178)** |
| Frosted, copy only, no filter | free in Editor | Small copy, bilinear | 03-blur | Dark glass edges turn to visible steps at 1/8 and 1/16: not good enough alone |
| Frosted, second camera | costly | Low-res camera instead of a URP pass | 03-blur-second-camera | Works, but the world draws twice and the colour comes out warmer than the screen. Ruled out |
| Gradients in USS | not possible | `linear-gradient` is refused | import log | Use the next row |
| Gradients by texture, SVG or vertex | free | 1x256 texture, SVG gradient, or `generateVisualContent` | 04-gradients-masks | All three render. Soft gradients over a wide area band: add noise to the texture (shown) |
| Rounded masks, circles | free | `border-radius` + `overflow: hidden` | 04-gradients-masks | Images clip cleanly. Nested masks and rotated children work. An image with its own radius clips too, no overflow needed |
| Uneven borders, two-colour bevels, uneven radii | free | `border-*-width`, `border-*-color`, `border-*-radius` | 04-gradients-masks | All work |
| Text outline | free | `-unity-text-outline-*` | 05-text | Sits on the glyph edge: 1 to 2 px looks hollow, 4 px clogs small letters, worst on serifs. Max 2 px, large text only |
| Text shadow | free | `text-shadow` | 05-text | Hard and 4 px look good. Blur 12 looked almost the same as 4: no wide glow |
| Letter and word spacing | free | `letter-spacing`, `word-spacing` | 05-text | Work |
| Line height | free | No USS property. Rich tag `<line-height=150%>` or `<line-height=70px>` | 05-text | Two lines at 34 px: 90 px plain, 110 with 150 %, 119 with 70px |
| Paragraph spacing | free | `-unity-paragraph-spacing` | 05-text | Weak: 30 added 10 px. A margin between labels is stronger |
| Wrapping, ellipsis | free | `white-space`, `text-overflow` | 05-text | Work. Labels here didn't wrap until a class set `white-space: normal` |
| Auto-size text | not possible today | `-unity-text-auto-size` needs the advanced generator | 05-text | Logs "not supported with the Standard TextGenerator". Text didn't shrink |
| `text-transform`, `text-decoration` | not possible in USS | Uppercase in C#; `<u>`, `<s>` rich tags | 05-text | Rich tags also do colour, size, superscript |
| Icons, SVG | free | Import as VectorImage, tint with `-unity-background-image-tint-color` | 06-icons | Sharp at 24 and 44 pt and when enlarged. Tint multiplies: draw icons white |
| Icons, sprite | free | PNG, same tint | 06-icons | Source needs 128 px or more for 44 pt. 64 px is soft |
| Transitions | free | `transition` on opacity, translate, scale, rotate, colours, sizes, radius, borders, margins, font size, spacing, outline, tint, flex-grow | 07-motion-a, b, c | All measured interpolating. Moving a plain sheet stayed flat in the Editor |
| Filter transition | costly | `transition: filter` | run log | Interpolates. Every frame pays for the filter |
| What can't transition | not possible | `display`, `height` from `auto`, `justify-content`, `flex-direction`, `position`, `visibility`, font style jump | run log | Animate fixed heights. Show an element, wait one frame, then remove the off-screen class: done in one frame it jumps |
| `@keyframes`, `animation` | not possible | `animation` is refused | import log | Loops need C# or chained transitions |
| Spacing | free | `gap` is ignored, use one-sided margins | 08-layout | |
| `:first-child`, `:last-child` | not possible | Ignored with a warning | 08-layout | Add a class by hand |
| `:nth-child()`, `:not()` | not possible | **One of these empties the whole .uss file** | import log | The first run lost all its styles to one `:nth-child(2)` |
| `calc()`, `vw`, `rem`, `grid` | not possible | Refused. `%` works, `aspect-ratio` works | 08-layout | Flexbox and percentages only |
| Safe area, bottom sheet at 30 % | free | `GameUiPanel.SafeInsets` as padding, `height: 30%` | 08-layout | Sheet measured 30.0 %. Runs under the home bar, content stays above it. Inset is faked here |
| Portrait in a circle, metal ring | free | Mask + thick border | 09-tactile | Clean at 220 and 121 px |
| Paper and brass panels, 9-slice | free | PNG, `-unity-slice-*`, `-unity-slice-type: tiled` | 09-tactile | Procedural stand-ins, not real art. Corners and rivets hold at two widths. A stretched centre shows streaks, so tile or keep it plain |

Not tested: `-unity-text-generator: advanced` is off in Project Settings and logs an error when used, so no ligatures or
complex scripts or auto-size from this pass. Mix blends and `clip-path` are refused by USS.

## Editor frame cost (Editor only, not a phone)

Median of 3 rounds of 240 frames, VSync off, tank + HUD at 1179x2556. Baseline 5.26 ms, 29 SetPass. Within this run rounds
agree to about 0.1 ms; across three full runs a figure moved by up to 0.3 ms, so read small gaps as ties. The GPU column
stayed under 2.1 ms: the desktop GPU is not stretched here, which a phone's will be.

| Scenario | Frame change | SetPass change |
|---|---|---|
| Solid sheet, scrim, rounded mask, text fx, 9-slice shadow, moving sheet | inside noise | 0 |
| UI Shader Graph on a sheet | inside noise | +1 |
| URP copy alone | +0.1 to 0.2 ms | +4 |
| Custom filter on a sheet | +0.2 ms | +4 |
| One USS blur (sheet, or blurred shadow) | +0.2 to 0.3 ms | +6 |
| Moving sheet with a blurred shadow | +0.4 ms | +6 |
| Ten small USS blurs | +0.9 ms | +60 |
| Frosted sheet, 1/8 copy + blur 6 | +0.3 ms | +9 |
| Frosted sheet, 1/4 copy + blur 14 | +0.3 ms | +8 |
| Frosted sheet, second camera | +1.8 ms | +19, 70 more draws |

A filter is not cached. A still panel with a filter pays every frame.

## Designers: do

- Build shadows as a soft shape behind the panel: 9-slice first.
- Treat frosted glass as one surface at a time: the sheet or the card, not every chip.
- Use flat tints for small pieces and keep the world sharp behind them.
- Use real bold and italic files if type carries the look. Plan on two or three files at most.
- Draw icons as white SVG, one colour, then tint.
- Move panels with translate, scale and opacity. Keep to fixed heights.
- Mask anything into rounded shapes and circles freely.
- For a tactile look, supply paper and metal as textures with a plain or tileable centre.

## Designers: don't

- Don't draw `backdrop-filter`, `box-shadow`, CSS gradients, blend modes or `clip-path` as if they were CSS. Each needs a
  route above, or doesn't exist.
- Don't stack blur: no blur on many small elements, and no blurred panels over blurred panels.
- Don't use wide text glows or thick outlines on small or serif type.
- Don't animate `height: auto`, layout direction or font style. Don't plan looping animation without saying who writes it.
- Don't rely on rainbow or multi-colour icons being tinted.
- Don't ship one wide soft gradient without noise: it bands.
- Don't write `:nth-child` or `:not` in a sheet.

## What each look gets

- **Warm tactile:** everything works today without runtime code. Paper and brass by 9-slice, real serif type, soft 9-slice
  shadows, circle portraits, bevel borders. The price is art and texture memory, not frames.
- **Soft glass:** deliverable, but it needs a URP render pass in the game and one filter on each frosted surface. Keep it to
  one surface on screen. It's the first thing that must pass on a phone before #106 commits to it.

## Still to measure on a phone (#99 or a dev build)

1. Frosted sheet, 1/4 and 1/8. The URP copy forces an intermediate texture, and a blur adds render-target changes, which
   tile-based phone GPUs dislike (doc knowledge, not measured). **Most important.**
2. USS blur at sheet size and in numbers, including a blurred shadow.
3. A full-screen scrim and a 30 % sheet over the glass: overdraw at phone resolution.
4. Moving a sheet with a blur in it.
5. SDF font atlas memory and first-use hitches with real text. Fallback glyphs on iOS and Android.
6. 9-slice paper and brass: texture memory.
7. Custom shader and filter survive stripping in a build. Only the Editor saw them.
8. Safe area on real notches.

## Frost test on a phone (#178)

Off by default and gone when closed: `FrostBackdrop` (Assets/Game/UI/Frost) hands a render-graph pass to the main camera's
renderer only while a frost mode is on, so neither renderer asset changed. `FrostTestPanel` exists only while open.

1. On a build, hold the top-left corner for 2 s. The `dbg` button appears. Tap it, then **Frost test (phone)**.
2. The tank stays live under a 30 % Keeper's Glass sheet. Tap **Off**, **Flat**, **1/8 + 6** or **1/4 + 14**. The table shows
   frame, CPU and GPU ms as average/worst, and dropped frames, per mode.
3. **Auto run** gives each mode 20 s, about 80 s in all, skipping the first 2 s of each. Don't touch the phone meanwhile.
4. **Send** files the table as a feedback note. The numbers are also in the note's game log.
5. **Cap 30** / **Cap 60** switches the frame cap. A phone defaults to 30; run Auto once at each for "60 where cheap". **Close**
   restores the cap and removes everything.

Reading it: CPU is the busier thread without the wait for Present. A drop is a frame over 1.5 budgets. The modes run in a
fixed order, so a hot phone flatters the first ones: if the numbers are close, run it again after a rest. Thermal state
isn't read (no cheap route), so watch for GPU ms climbing between runs.

Pass or fail goes here, against 30 fps minimum and steady, 60 where cheap (practices §7): *not measured yet*.

## Fonts used

Lora and PT Serif, both SIL OFL 1.1, from the `google/fonts` repo on GitHub (`ofl/lora`, `ofl/ptserif`). Bundling is
allowed if the licence goes along. Not committed: the files sat in `Temp/agent/fonts`, are deleted, and need an entry in
`docs/assets-log.md` if adopted. Lora's file is variable, so real bold needs static cuts.

To rerun: the `UiLimitsSpike` [Explicit] test (category `UiLimits`); put the fonts back in `Temp/agent/fonts` first.
