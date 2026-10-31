# UI spec: Keeper's Glass (#107, 2026-10-31)

The agents' version of the proposal. The drawn version is the **Proposal** page of
[the canvas](https://claude.ai/artifact/NKQ4MJu5YKCqwFfT2DDGv7): a Design System board, one board per inventory section,
four storyboards and the flat fallback.

Builds on [hierarchy.md](hierarchy.md) (rules and D1 to D6), [limits.md](limits.md) (what USS can draw) and
[../ui-inventory.md](../ui-inventory.md) (pieces and states). The canvas draws with CSS, so where the two differ, this file and
limits.md decide how it's built. **Pending:** the user's sign-off, the hero piece in Unity and the tester review (see the
end of this file).

Scale: 1 pt on a 393 pt phone is 2.75 USS px in the 1080-wide panel. Sizes below are pt. Multiply by 2.75 for USS.

## 1. Tokens

Name the USS variables as in the first column, in `Game.uss` `:root`.

### Colour

| Variable | Value | Use |
|---|---|---|
| `--ink` | `#3D2A1E` | Text on frost, tint and paper |
| `--ink-soft` | `#4A3324` | Button and tool labels, on flat tint only |
| `--timber` | `#6A4B36` | Pips, dots, dashed locked edge, sliders |
| `--paper` | `#F3E9D6` | Base of every light surface; solid on book pages |
| `--frost-tint` | paper at 72 % | One surface per screen, over the blur |
| `--flat-tint` | paper at 92 % | Tools, chips, tags, pin, bubbles, hints, banner |
| `--flat-sheet` | paper at 94 % | Replaces the frost when the switch is off. No blur |
| `--quiet-backing` | paper at 72 %, no blur | Behind the box name and top icons once the camera leaves its home view |
| `--brass` | `#B88A3E` | Hairlines at 53 %, portrait ring, slider thumb |
| `--brass-light` | `#D9B873` | Frost edge, gold pin tag, brass plate |
| `--primary` | `#9E5033` | One per moment; selected tab line, armed ring |
| `--on-primary` | `#EFE3CC` | Text on primary, toast and caption |
| `--moss` | `#526E3A` | Portrait until real ones exist, toggle on |
| `--dark-plate` | ink at 91 % | Toast, caption, era card |

### Type

Lora and PT Serif only, SIL OFL 1.1. Lora's file is variable, so import static cuts. Files: Lora 700, Lora 500, PT Serif 400,
700, 400 italic; drop Lora 500 if the atlas budget needs it (the selected-tab weight then falls to fake bold on regular).
Nothing under 13 pt. Line height is a rich tag (`<line-height=135%>`): USS has no property.

| Class | Font | Size | Use |
|---|---|---|---|
| `.type-title` | Lora 700 | 26 | Book title |
| `.type-sheet` | Lora 700 | 21 | Sheet heading |
| `.type-box` | Lora 700 | 19 | Box name |
| `.type-big` | Lora 700 | 17 | Tools, big prompt, caption |
| `.type-head` | Lora 700 | 16 | Peek headline, banner title |
| `.type-small-head` | Lora 700 | 15 | Tile, selected tab, hint |
| `.type-btn` | PT Serif 700 | 16 | Buttons |
| `.type-body` | PT Serif 400 | 15 | Body, 135 % |
| `.type-sub` | PT Serif 400 | 14 | Sub, year, toast |
| `.type-quote` | PT Serif italic | 14 | Quote, teaser line |
| `.type-note` | PT Serif 400 | 13 | Notes, chips, smallest text |

### Spacing, radii, shadows

- Spacing: 4, 6, 8, 10, 12, 16, 24. Use one-sided margins and a class per position (`gap`, `:first-child`, `:last-child`,
  `:nth-child`, `:not` all fail; the last two empty the whole file).
- Screen sides 16. Bottom safe area 30 or `GameUiPanel.SafeInsets`, whichever is larger.
- Radii: chip 14, tile 16, toast and caption 18, pin 22, button 23, peek 24, sheet 26, tool 28.
- Soft shadow is a 9-slice PNG behind sheet and peek only. Small pieces get a hairline, no shadow.

### Lanes (distance from the bottom edge)

One piece per lane, so nothing collides. `GameUiPanel.PromptBottom` and `ToastBottom` hold the moving values.

| Lane | Bottom | Holds |
|---|---|---|
| Tools or banner | 30, 56 tall | History, Clean, Shape; or the tool in hand |
| Pin and Stories | 96, 44 tall | Pin chip left, Stories or Shelf chip right |
| Peek | 98 | Replaces the pin lane while shown |
| Toast | 146 | Eight above a sheet when one is up |
| Prompt | 204 | Watch, Finish, Give them rain, the two asks |
| Caption | top 52 (100 with the HUD up) | Caption, era card |

### Motion

Only what limits.md allows. Fixed heights, no keyframes, no loops, no filter transition. Show the element, wait one frame,
then remove the off-screen class.

| Token | Time, easing | Property | Used by |
|---|---|---|---|
| `press` | 80 ms ease-out | scale .97, colour | Buttons |
| `fade` | 160 ms ease-out | opacity | Toast, hint, tag, bubble |
| `slide` | 280 ms, `ease-out-cubic` in, `ease-in-cubic` out | translate Y | Sheets, peek, banner |
| `ui-away` | 320 ms `ease-in-cubic` | translate, opacity | HUD leaving for a moment |
| `caption` | 400 ms in, 600 ms out | opacity | Captions, era card |
| `glow` | 3 steps of 600 ms, then still | border colour, chained in C# | New tool, once. Who writes it: `GameHud` |
| `turn` | 240 ms ease-out | translate X | Card and book pages |
| `switch` | 160 ms ease-out | translate X, colour | Toggle |

### Sound and haptics

Components call a role, which calls `Sfx.Play` with a `CueLibrary` id. Nothing is carried by sound alone.

| Role | Cue | Haptic |
|---|---|---|
| Tap | `ui_tap` | Selection |
| Toggle | `ui_toggle` | Selection |
| Open, close, back | `ui_open`, `ui_close`, `ui_back` | none |
| Confirm (Give, Keep, Done) | `ui_confirm` | Light |
| Ask | `ui_question` | none |
| Scrub tick | `ui_scroll` | none |
| Card in, out | `card_arrive`, `card_dismiss` | Light, none |
| Book | `book_open`, `book_close`, `book_page` | none |
| Show me | `show_me` | none |
| Refused | `shape_fail` | Warning |
| New tool | `chime` | Success (new; the cue has none today) |
| Found | `discovery` | Success |

## 2. Surfaces and the one switch

- **Frost** = URP pass copying the frame to 1/4 size, a panel showing it, `filter: blur(14px)`, `--frost-tint` on top. One
  surface on screen. It needs runtime code and is costly (limits.md).
- **Flat** = `--flat-sheet`, no copy, no filter. Frost surfaces use a single class, `.surface`; the switch swaps it for
  `.surface--flat` in one place (#108). Layout doesn't change. Every frost surface is on the Flat fallback board.
- Frost surfaces: peek, card stack, person card, tray, recall, time-lapse panel, naming, first-year card, setting picker,
  shelf list, settings, licences, reset, history sheet, feedback.
- Flat tint, never frost: tools, chips, tags, pin, bubbles, hints, banner, tiles.
- Paper, solid: book pages.
- Sheets sit on the bottom edge at 30 % (`height: 30%`, 237 pt on a 790 pt screen). Longer content scrolls inside. Above
  30 %: setting picker 33 %, settings and feedback 59 %. None of those talk about the world, so rule 5 holds.
- A scrim appears only behind the reset confirm. Nothing dims before Watch.

## 3. Components

One component each, per inventory §4. Every element name stays the same: the play tests find controls by name.

| Component | Replaces | States | USS route |
|---|---|---|---|
| **Button** | Button classes across views | Primary, secondary, quiet, icon; default, pressed, unavailable | Primary `--primary`. Unavailable is a dashed edge and a lock with full-strength text, never faded |
| **Chip** | Tie chips, kind chips | Label; choice off, on (check mark) | Visible 30, 44 slot when tappable |
| **Pin chip** | `hud-pin` | Hidden, pinned, long label | 40 portrait + name, `max-width`, ellipsis only past 220 |
| **Stories chip** | `hud-history-dot` | Hidden, shown | Dot + word, never a number |
| **Sheet + scrim** | `GameHud.Sheet`, `cards-sheet`, `recall-sheet`, `shelf-sheet`, `fb-sheet`, naming sheet | Closed, open, scrolled | `.surface`, translate on open, `SafeInsets` as padding |
| **Card** | Chronicle, person, first-year, recall, feedback cards | Head, body, actions | Content of a sheet, never its own surface |
| **Tab** | Best, Recent; music frequency | On, off | Selected is bold and underlined |
| **Toggle** | Settings pills | On, off | Word inside, knob moves |
| **Slider** | New | Value | 44 hit strip, 4 line, 22 ring |
| **Bubble** | `firstrun-`, `moment-`, `showme-`, `shaping-`, `boxcomplete-bubble` | Who + line, chatter symbols | Tail is a rotated square. One at a time, second waits. Longer lines become a caption |
| **Label** | `pc-name-tag`, `pc-pin-tag`, tie labels, brass plate | Name, gold + pin mark, tie + mark | Line style and mark per tie |
| **Badge, dot** | `-dot`, pips | Dot, page dots, charge pips, New tag, ribbon | Dots never count |
| **Toast** | `hud-toast`, `tl-toast`, `fb-toast` | One at a time, queued | One queue, in `GameUiPanel`. Prompts outrank it |
| **Caption** | `moment-caption`, `boxcomplete-caption`, `firstrun-year-caption`, `tl-caption` | Line, line + who; in panel for the time-lapse | `--dark-plate`, or plain text inside the time-lapse panel |
| **Hint** | `firstrun-hint`, `moment-hint`, `boxcomplete-hint` | Word, ghost finger | Light pill, at most three words |
| **Banner** | `shaping-banner`, `visit-banner` | Tool in hand, visiting | Fixed exit, bottom row |
| **Big prompt** | `moment-watch`, `complete-prompt`, `firstrun-rain` | Hidden, ready | Prompt lane, no timer |

Icons: 24 grid, stroke 2, one colour. Import as VectorImage, draw white, tint with
`-unity-background-image-tint-color`. The set is on the Design System board. Portraits: `border-radius` mask + 2 pt
`--brass` border, initials on `--moss` until real ones exist.

## 4. Screens

Rules that change today's layout. Everything else is as drawn.

- **HUD (D3 to D6):** box name and year as quiet text, settings mark beside it, Photo and a quieter Feedback (test builds)
  top right. Three tools of equal weight. Clean on reads "Done" with a check and a ring (rule 7).
- **Quiet backing:** ink on the world passes on the default view (8.05:1 or better) and fails on any closer camera, down
  to 1.0:1 over the glass frame. Add `--quiet-backing` to the name and top icons whenever the camera is off its home view.
- **D1:** History always opens the time-lapse. The unread dot moves to the Stories chip after a peek is swiped away.
- **Card stack:** one sheet, 237 tall. Order is Back, Next, Show me, then the one primary at the right: Give, Watch,
  Remember or Done. Next is never primary. A story card has no filled button; its headline leads. Long cards scroll.
- **Person card:** name, Pin and Close in one row, the subtitle on its own row below, so it can't run under Pin. Tabs
  are About, Recent, Best. Tie labels sit at the far end of each tie line, off the name tag.
- **Tray:** a low row of tiles, 30 %, no dim. Charges sit in the head. The reason for a locked or capped tool is a note
  under the row. Picking a tool removes the sheet.
- **Placing:** footprint on the ground, tool in hand, charges, Cancel. **Cancel's place is the user's call, see §7.**
- **Time-lapse:** the HUD leaves, so the only year is the tag on the playhead. A hairline band with era names as text, a
  44 pt hit strip, pin marks. The caption lives inside the panel (27 %), and its controls replace the play row.
- **Era card:** always the era at the playhead. Check the timing bug in captures/README.md when building it.
- **Feedback, recall:** sheets from the bottom edge. Feedback's Send is unavailable until there's text.
- **Reset:** "Reset Mossbarrow?", says what's lost, "Keep playing" is primary, "Reset box" is small, far and marked.
- **Book:** full-screen paper, swipe to turn, Back, Next and Close at the bottom. Era look changes with paper colour and
  type treatment; the stone and bark art doesn't exist yet.

Test builds only: Feedback icon and sheet, recall prompt, skip row. Dev only: debug dock, stays IMGUI, not redesigned.

## 5. Bugs in captures/README.md

| Bug | Fix |
|---|---|
| Overlapping bubbles | One bubble at a time, one message per moment |
| Toast collisions | One toast queue, one lane |
| Subtitle under Pin | Subtitle has its own row |
| Clipped pin chip | Wider chip, ellipsis only past 220 |
| Two primaries side by side | Next is always secondary |
| Tie labels over the name tag | Labels at the far end of the tie |
| Two years on the time-lapse | HUD leaves, one tag on the playhead |
| Sheets out of the thumb zone | Every sheet sits on the bottom edge |
| The tall tray | Low tile row at 30 %, no dim |
| Time-lapse caption at 45 % | Caption inside the panel, 27 % |
| Controls with no link to their area | Skip row is a test-only group, Shelf is a labelled chip |

## 6. Contrast

Measured on the real backgrounds at phone size: a Gaussian blur of 14 on each image, tint on top, lowest pixel in each
region. Worst case is pure black, since no night capture exists (`extra-hud-default-night` looks like day). This is a
calculation, not a Unity render; the frost's URP copy still needs checking in the game.

| Pair | Lowest on real backgrounds | Over black | Result |
|---|---|---:|---|
| Ink on frost, 72 %, sheet | 7.07:1 (close camera) | 5.73:1 | Pass, frost not raised |
| Ink on frost, peek | 8.51:1 | 5.73:1 | Pass |
| Ink on frost, tall sheet 62 % | 7.08:1 | 5.73:1 | Pass |
| Ink on flat tint 92 % | | 9.44:1 | Pass |
| Ink on flat sheet 94 % | | 9.88:1 | Pass |
| Ink soft on frost | | 4.96:1 | Pass, but not used on frost |
| Primary text on primary | 4.53:1 | | Pass, no margin: don't lighten |
| Paper on moss portrait | 4.78:1 | | Pass |
| Ink on gold tag | 7.14:1 | | Pass |
| On-primary on dark plate | 8.08:1 over white | 11.31:1 | Pass |
| Ink on the world, no backing | 8.05:1 default view | 1.0:1 close camera | **Fails, needs the quiet backing** |
| Ink on quiet backing, 72 % | | 5.73:1 | Pass |

Keep `--frost-tint` at 64 % or higher: below that, ink over black drops under 4.5:1.

## 7. Open, for the user

**Placing's Cancel.** Principle 1 in research/ui §8 says controls sit near the action. The signed-off wireframe has
Cancel in the bottom row. Both are drawn on the 3.4 board.

| | A: bottom row (wireframe) | B: beside the footprint (principle 1) |
|---|---|---|
| Where | Fixed, bottom right, 108 × 56 | 44 tall, up and right of the footprint, clamped to the screen |
| For | One exit in one place (rule 7, research rec 4). Never moves, always in thumb reach | Short travel, near the thing it cancels |
| Against | The far end of the screen from the footprint, a longer move | Moves with the footprint, and a tap meant for the ground can land on it |
| Recommendation | **A**, with the tool bar joined to it. A wrong tap on B would place or cancel by accident (#145) | |

Also open:

1. **Quiet backing:** only when the camera is off its home view (drawn), or always on, which is the safer guarantee but
   brings back something close to the pill D3 removed?
2. **Long cards:** relic and end-line cards scroll inside 30 %. Fine, or let those grow to about 34 %?
3. **Story cards** have no filled button, on the reading that the headline is the primary. Is that calm or too flat?
4. **Shelf and Stories** share the chip slot. Once a box is finished both can want it. Proposed: Stories wins, and Shelf
   moves into Settings while it's up.
5. **New words** with no game text behind them yet: "Stories", "Back to the story", the silent notice, the reset body,
   "The next box", "Begin", "Rename your box", tab "About". Samples on the boards: crowd card "Tob", "The Guild Years",
   "The Quarry family", the snapshot file name, licence rows.
6. **Setting picker:** draw two settings only, as decided (desert oasis, mountain village), with desert oasis preselected?

## 8. Which issue builds what

| Issue | Builds |
|---|---|
| **#108** | Tokens, fonts, every component in §3, motion helpers, safe area, lanes, the frost pass and the one switch, HUD shell, shared sheet, the gallery |
| **#165** | Person card, portrait, tabs, tie labels, crowd card (#148) |
| **#166** | Peek, Stories chip, card stack, Also…, wish card, recall, Show me bar |
| **#167** | Tray, tool bar, Cancel, Take back chip and its timer (#149), footprint |
| **#168** | Time-lapse panel, timeline, caption in the panel, era card, snapshot label |
| **#169** | Gesture hints, naming, plate, first rain, first year, first-year card, skip row |
| **#170** | Watch and asks, moment overlays, new tool glow, Finish, last moment, shelf, visiting banner, setting picker |
| **#77** | Evening talk: gather, speak, shelf view, teaser caption |
| **#80** | Book tab, ribbon and all book pages |
| **#95** | Settings, volumes, frequency, silent notice, rename, reset, licences, feedback sheet |

Two pieces need code outside UI Toolkit: the footprint is a world mesh (`ShapingFootprint` is core only today), and the
tie lines stay world-space. #115 owns the settings' terrain, not the picker's UI, so the picker sits with #170 unless the user
moves it.

## 9. Still to do

- Hero piece in Unity over the live tank: the frost is what the mockup can't prove. Proposed: **the wish card in the card
  stack**. It proves the URP frost at 30 %, real Lora and PT Serif, a 9-slice shadow, the portrait ring, the safe area
  and one primary in a single piece, and #166 needs it anyway.
- Tester review against game-design-practices.md.
- Legibility at 375×667, and a night background once one exists. Tablet isn't drawn.
- Frost on a phone, in the final build (limits.md list). If it fails, flip the switch.
