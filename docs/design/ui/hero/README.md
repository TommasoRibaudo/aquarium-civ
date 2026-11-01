# Hero piece: the wish card in Unity (#107, 2026-10-31)

**Verdict: matches with changes.** The Card stack phone can be delivered in UI Toolkit over the live tank. Geometry, type and
solid colours land within 2 px of a 1179 px wide frame. Getting there took the seven spec changes below. One difference
can't be fixed, and it helps: frost over dark backgrounds comes out lighter than the board.

How it was checked: `KeeperGlass.dc.html`'s own CSS and markup for that phone, rendered in headless Chrome at 3x over the
same tank frame the game drew, with the same font files. The Chrome render differs from the board in three ways: the
background is our capture, the frame is 393 x 852 pt instead of 390 x 790, and the sheet is 30 % of that height with a 34 pt
bottom. Those are the phone's values, so the two line up pixel for pixel. The board's own size is in `12-mock-board-790.jpg`.

Code: `AquariumCiv/Assets/Game/View/PlayTests/Hero/`, `HeroWishProof`, [Explicit], category `HeroWish`. It loads its sheet by
path in the Editor only, so no build carries it. CardStackView and every screen are unchanged. Wording, the year and the
page dots are fixed strings. Nothing is wired to a real wish, and the HUD is hidden as on the board.

| Image | Shows |
|---|---|
| `01-frost.jpg` | The card over the village, 1/4 copy + blur 14 |
| `02-side-home.jpg` | Unity, Chrome, difference x4, home view |
| `03-side-close-blur14.jpg` | Same, camera close so the box is behind the sheet, blur 14 |
| `04-side-close-blur38.jpg` | Same, blur 38.5 |
| `05-flat.jpg` | Flat fallback: no copy, no pass (asserted) |
| `06-dirty-close-frost.jpg`, `07-dirty-close-flat.jpg` | Dirty glass behind the sheet, frost and flat |
| `08-dirty-home-frost.jpg` | Dirty glass, home view. Only the shelf is behind the sheet, so it changes nothing there |
| `09-longest-wish.jpg` | Longest real wish (70 characters), 9-letter name, four buttons |
| `10-frost-over-black.jpg` | The floor: tint over pure black |
| `11-text-outline-03.jpg` | Text with a 0.3 px outline in its own colour |
| `12-mock-board-790.jpg` | The Chrome render at the board's own 390 x 790 |
| `13-edge-band-before-after.jpg` | Left edge of the sheet without and with the sharp copy, 2x |

## Differences

Pixels are on the 1179 wide frame (3 px = 1 pt). Unity first, board second.

| What | Unity | Board | Fix |
|---|---|---|---|
| Type size | Title ink 213 to 718, body 50 to 1041, Skip all 978 to 1110 | 213 to 719, 49 to 1041, 978 to 1111 | Same |
| Type weight | 7 to 16 % fewer dark pixels (title 8.5, body 13, Skip all 16). Reads a little thinner | | **Fixable.** A 0.3 px outline in the text's colour brings it to within 6 % (`11`). Not in the main shots |
| Glyph edges | SDF, slightly softer and unhinted | Browser rasteriser | **Not fixable**, not noticeable at phone size |
| Spacing | Dots, portrait, heading, first body line within 1 px. Year 2 px high. Next 2 px wider, so Show me sits 1 px right | | Fixable with a nudge, not worth one: under 1 pt |
| Top padding | Needed 13 pt, not 12 | CSS counts the 1 pt hairline inside the box | Fixed here |
| Line height | Pitch 60.8 px with `<line-height=55.7px>` | 60.75 px | **Fixed**, see spec change 1 |
| Radius | Sheet 26, buttons 23, no ghost in the difference | Same | Same |
| Portrait | Ring 1867 to 1998, moss 1873 to 1992, #B88A3E | Same to the pixel | Same |
| Hairline | 3 px, 217 184 116 | 217 184 115 | Same |
| Solid colours | Primary and dots exact, moss 1 off | | Same |
| Page dots, off | 192 176 157 with the board's 40 % | 180 159 136 | **Fixed:** 52 % gives 179 162 143 |
| Button edge | 214 190 157 with the board's 53 % | 211 182 132 | **Fixed:** opaque `#D3B684` |
| Shadow | Almost nothing at the board's 20 % (200 above the sheet) | 193 | **Fixed:** tint 45 %, 194, and within 1 level to 30 px up. Softness matches: one texture, sigma 33 panel px |
| Frost tint, home view | 233 224 206 | 232 223 206 | Same |
| Frost tint over dark | Close camera 229 216 194, over black 210 202 185 | 228 213 188, calculated 175 168 154 | **Not fixable** while the project is Linear. Harmless: contrast goes up |
| Blur look | Blur 14 keeps the shelf and glass edges as visible streaks in the close view | Softer | **Fixable:** blur 38.5 is close to the board (`04`). Same Editor cost, 5.75 v 5.71 ms over 300 frames. Phone unknown |
| Sheet edges | Light band down both sides and along the bottom, 7 levels at the left edge | None | **Fixed:** the sharp copy under the blur. Left edge 216, board 214 (`13`) |
| Sheet height | 256 pt on this phone | 237 on the board | Not a bug: both are 30 % |

Why so many colours moved: the project is Linear, so UI Toolkit blends in linear light and CSS in sRGB. Light over light hardly
changes. Dark over light comes out paler, which covers the dots, the edges, the shadow and the rim of every letter. Light over
dark comes out lighter. `PanelSettings.forceGammaRendering` changed nothing on this overlay panel: body ink was 5582 px
with and without it.

## Contrast, measured in the render

Each state was captured twice, with the words and with them transparent. Ink is the darkest 2 % of the changed pixels; the
background is the 1 % darkest pixel in the text's box. Ink rendered at exactly #3D2A1E.

| State | Heading | Body | Year | Skip all |
|---|---|---|---|---|
| Frost, home | 9.38 | 9.97 | 10.04 | 10.07 |
| Frost, close, clean | 8.56 | 8.54 | 9.20 | 9.56 |
| Frost, close, dirty (worst game state) | 8.54 | 8.52 | 9.11 | 9.49 |
| Flat | 10.76 | 10.86 | 10.86 | 10.86 |
| Flat, close, dirty | 10.64 | 10.64 | 10.76 | 10.76 |
| Frost over pure black | 8.33 | 8.33 | 8.33 | 8.33 |

Fills, sampled: button text on its fill 9.65:1, on-primary on primary 4.53:1 (no margin, as the spec says), M on moss 4.72:1.

Everything passes, and the floor is 8.33:1 where the spec calculated 5.73:1. Hour 22 looks like day (spec §6 says so too), so
night proves nothing here. The black frame stands in for it.

## Also proved

- Sheet 30.0 % of the panel, asserted.
- Skip all, Next, Show me and Give are each at least 44 pt in both directions, asserted. A real tap reaches Give.
- Safe area: `GameUiPanel.SafeInsets` with an iPhone 15 inset (34 pt, faked). The bottom padding took the larger of 30 and 34.
- Longest wish: "Constance the stallholder keeps wishing for a grove on the north side." Two lines, 35 pt left above the buttons.
  Back, Next, Show me and Give fit in one row at 393 pt.
- `var()` carries sizes, colours and font URLs. A TTF works directly in `-unity-font-definition`.

## What the spec must change

1. **§1, line height:** `<line-height=135%>` is 135 % of the font's own line, not of its size. It gave 1.79 of the font size, a
   pitch of 73.8 panel px. Use a px tag from the size (`<line-height=55.7px>` for body). Measured on three lines: plain 164.0,
   135 % 202.4, 55.7 px 164.9.
2. **§1, Lora:** don't instance the variable file. Lora's licence reserves the name "Lora", and an instanced file is a modified
   version that couldn't keep it. Upstream publishes static cuts (see Fonts). Use those, and take Lora 500 from there too.
3. **§1, translucent tokens on dark-over-light:** they need linear values. Dots off 52 %, not 40. Button and tool edge opaque
   `#D3B684`, not brass at 53 %. Shadow 45 %, not 20. Add a rule: check any translucent dark token in a render, never
   copy it from CSS.
4. **§1, type:** add `-unity-text-outline-width: 0.3px` in the text's own colour to every class, if #108's phone check agrees.
5. **§2, frost recipe:** paper, then the sharp copy, then the blurred copy, then the tint. `FrostTestPanel` still has the band.
   Change blur to 38.5 panel px (the board's 14 pt) if it passes on a phone. `limits.md`'s "blur 14" is 14 panel px, about
   5 pt. Run the phone test at both.
6. **§4, card stack:** "one sheet, 237 tall" should read "30 %". It's 256 pt on a 393 x 852 phone. Top padding is 12 pt plus the
   hairline.
7. **§6, contrast:** replace the calculated figures with the table above. "Keep `--frost-tint` at 64 %" came from sRGB maths and
   no longer follows. On the home view the sheet covers only the shelf, so dirty glass needs testing with the close camera.

## Not verified

- **No phone.** All of this is the Editor on a desktop GPU. Linear blending on a real device, the frost's frame cost, blur 38.5
  and font atlas memory wait for the final build (limits.md).
- Cost of the sharp copy and of the text outline wasn't measured. Neither adds a render pass; the outline may add text work.
- The fonts ran from TTFs. A built font asset, static atlas or fallback table wasn't tried. #108 needs them.
- Lora Medium and PT Serif Italic weren't imported: the wish card doesn't use them.
- Real wish data, press states, motion, the page turn and tablet aren't covered. 375 x 667 wasn't checked.
- Dark world: no real night exists yet.

## Fonts

Promoted by #108 to `Assets/Game/UI/Resources/GameUI/Fonts/`, with their licences; the proof now reads them from there. Both are SIL OFL 1.1.

| File | Source | Commit |
|---|---|---|
| `Lora-Bold.ttf` | github.com/cyrealtype/Lora-Cyrillic, `fonts/ttf/`, v3.021, static | `990d096` |
| `PT_Serif-Web-Regular.ttf`, `-Bold.ttf` | github.com/google/fonts, `ofl/ptserif/` | `f0f0486` |

`GlassShadow.png` (`HeroShadow.png` until #108) is made in code: rounded rect, sigma 33, white with alpha.

## To repeat

`unity command test run --mode playmode --filter HeroWish --filter_type category --include_explicit true --async_tests true`
writes PNGs to `Temp/agent/hero`. 2/2 pass, about a minute.
