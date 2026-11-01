# UI spec: Keeper's Glass (#107, 2026-10-31)

The agents' version of the proposal. The drawn version is the **Proposal** page of
[the canvas](https://claude.ai/artifact/NKQ4MJu5YKCqwFfT2DDGv7): a Design System board, one board per inventory section,
four storyboards and the flat fallback.

Builds on [hierarchy.md](hierarchy.md) (rules and D1 to D6), [limits.md](limits.md) (what USS can draw) and
[../ui-inventory.md](../ui-inventory.md) (pieces and states). The canvas draws with CSS, so where the two differ, this file and
limits.md decide how it's built. The tester's [real] findings are fixed on the boards, and the hero piece is done
(`vs/107-hero`, `docs/design/ui/hero/README.md`: matches with changes, folded in here). **Pending:** the user's sign-off.

Scale: 1 pt on a 393 pt phone is 2.75 USS px in the 1080-wide panel. Sizes below are pt. Multiply by 2.75 for USS.

**The boards are CSS, which blends in sRGB. The project is Linear, so UI Toolkit blends in linear light.** Light over light
hardly changes, dark over light comes out paler, light over dark comes out lighter. Check any translucent dark token in a
render, and never copy it from the boards' CSS.

## 1. Tokens

Name the USS variables as in the first column, in `Game.uss` `:root`.

### Colour

| Variable | Value | Use |
|---|---|---|
| `--ink` | `#3D2A1E` | Text on frost, tint and paper |
| `--ink-soft` | `#4A3324` | Button and tool labels, on flat tint only |
| `--timber` | `#6A4B36` | Pips, dots, sliders, thin edge of unavailable pieces |
| `--dot-off` | timber at 52 % | Page dots, off (the boards' 40 % comes out too pale in Unity) |
| `--paper` | `#F3E9D6` | Base of every light surface; solid on book pages |
| `--frost-tint` | paper at 72 % | One surface per screen, over the blur |
| `--flat-tint` | paper at 92 % | Tools, chips, tags, pin, bubbles, hints, banner |
| `--flat-sheet` | paper at 96 % | Replaces the frost when the switch is off. No blur. 96 from question 31; #108 |
| `--quiet-backing` | paper at 72 %, no blur | Behind the box name and top icons once the camera leaves its home view |
| `--brass` | `#B88A3E` | Portrait ring, slider thumb |
| `--edge` | `#D3B684`, opaque | Hairline on buttons and tools (the boards' brass at 53 % comes out too pale) |
| `--brass-light` | `#D9B873` | Frost edge, gold pin tag, brass plate |
| `--primary` | `#9E5033` | One per moment; selected tab line, armed ring |
| `--on-primary` | `#EFE3CC` | Text on primary, toast and caption |
| `--moss` | `#526E3A` | Portrait until real ones exist, toggle on |
| `--dark-plate` | ink at 96 % | Toast, caption, era card. The boards' 91 % in sRGB; 96 gives the same colour in linear light (#108, measured) |

### Type

Lora and PT Serif only, SIL OFL 1.1. Five files: Lora 700 and 500, PT Serif 400, 700 and 400 italic. **Don't instance
Lora's variable file:** "Lora" is a Reserved Font Name, and an instanced file is a modified version that couldn't keep it.
Use upstream's static cuts from `cyrealtype/Lora-Cyrillic` (`fonts/ttf/`), and take Lora 500 from there too. Nothing
under 13 pt.

Line height is a rich tag, because USS has no property. **Use px, not %:** `<line-height=135%>` is a percent of the font's
own line and gives 1.79 of the size. Use size × 1.35 in panel px, so body is `<line-height=55.7px>`.

Unity's type reads 7 to 16 % lighter than the board. Add `-unity-text-outline-width: 0.3px` in the text's own colour to
every class, pending the phone check in #108.

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
- Soft shadow is a 9-slice PNG behind sheet and peek only, tinted 45 % (the boards' 20 % almost vanishes). Small pieces
  get a hairline, no shadow.

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

### Touch targets

Every target reaches `GameUiPanel.MinTarget` (144 px, 48 dp, 50 pt at 2.88 px/pt on a 375 pt phone; practices §5). The boards draw 44 pt, which is 121 px and
only 42 pt on that phone, so the drawn sizes here stay and the hit area is larger (#108):

- **Clear pieces that may touch their neighbours are 144 tall:** chip slot, tab, toggle, slider strip, settings row. What
  shows inside is unchanged, and the tab's underline sits 4 pt lower.
- **Drawn pieces keep their size and reach 144 with `GlassReach`:** button (46, quiet and icon 44, way back 44), pin chip,
  Stories chip. Space them apart, because a reach mustn't overlap another target. Tools (56) and big prompts (56) need none.

`GlassUiPlayTests.EveryTargetReachesMinTarget` holds this over the gallery and the HUD.

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

**The switch:** `FrostSwitch.Default` in `Assets/Game/UI/Glass/FrostSurface.cs`. Change that one constant to `false` and every surface is
flat, with no copy, no filter and no pass. The debug panel flips it for a session (`Sheets: frost`), and trades the blur sizes.

- **Frost** = URP pass copying the frame to 1/4 size, then in the panel, bottom to top: paper, the sharp copy, the blurred
  copy, `--frost-tint`. Without the sharp copy, a light band shows along the sheet's edges. One surface on screen. It
  needs runtime code and is costly (limits.md).
- **Blur size:** limits.md's "blur 14" is 14 panel px, about 5 pt, and it leaves shelf and glass edges as streaks on a close
  camera. `blur(38.5px)` matches the board's 14 pt at the same Editor cost. Test both on the phone.
- **Flat** = `--flat-sheet`, no copy, no filter. Frost surfaces use a single class, `.surface`; the switch swaps it for
  `.surface--flat` in one place (#108). Layout doesn't change. Every frost surface is on the Flat fallback board.
- Frost surfaces: peek, card stack, person card, tray, recall, time-lapse panel, naming, first-year card, setting picker,
  shelf list, settings, licences, reset, history sheet, feedback.
- Flat tint, never frost: tools, chips, tags, pin, bubbles, hints, banner, tiles.
- Paper, solid: book pages.
- Sheets sit on the bottom edge at 30 % of the screen (`height: 30%`): 237 pt on the boards, 256 pt on a 393 × 852 phone.
  Top padding is 12 pt plus the hairline. Longer content scrolls inside, and the scroll view's height snaps to whole lines so
  no line is cut. Above 30 %: setting picker 33 %, licences 35 %, settings and feedback 59 %, settings with the silent notice
  63 %. None of those talk about the world, so rule 5 holds.
- **Short phones (375 × 667):** the sheet stays 30 %, 200 pt. The card needs about 216 pt at full layout, so under 700 pt
  tall the year joins the dots row and bottom padding drops to 14, which gives 193 pt. Longer text scrolls. Drawn at the
  end of 3.3, built in #166. A locked Give's reason moves above the body there, so it can't scroll out of sight.
- **Feedback under 700 pt tall:** its 470 pt doesn't fit 59 % of a 667 pt phone (394 pt). It's test-only, so it grows to full
  height over the box, top under the safe area, with Cancel and Send on the bottom edge. Not drawn.
- **375 × 667 is drawn for the wish card only.** #165 to #170 and #95 must check it screen by screen.
- **Keyboard:** a text-entry sheet rides on top of the keyboard, by translate, using `TouchScreenKeyboard.area`. Keep and
  Send stay above it. Naming and rename keep their layout, with bottom padding 12. Feedback folds to heading, text and
  buttons, because its full layout wouldn't fit. Drawn on 3.7 and 3.8. Check Unity's mobile input bar on a device.
- A scrim appears only behind the reset confirm, and the URP copy won't include it, so that sheet comes out lighter than
  drawn. Nothing dims before Watch.

## 3. Components

Built in #108: `Assets/Game/UI/Glass/`, one sheet `Resources/GameUI/Glass.uss`, tokens in `Game.uss`. Open the gallery from the debug
panel (`UI gallery`). Each state is named `gal-<component>-<state>`. Retrofits: build screens from these and drop the old classes as they go.
Old `Game.uss` tokens that clashed are now `--old-moss` and `--old-ink-soft`.

One component each, per inventory §4. Every element name stays the same: the play tests find controls by name.

| Component | Replaces | States | USS route |
|---|---|---|---|
| **Button** | Button classes across views | Primary, secondary, quiet, icon; default, pressed, unavailable | Primary `--primary`. Unavailable is a thin solid `--timber` edge, a paler fill and a lock, with full-strength text, never faded |
| **Chip** | Tie chips, kind chips | Label; choice off, on (check mark) | Visible 30, inside a transparent parent of 144 px that takes the tap. Slots touch, with no gap between rows. Drawn on Design System, used in the person card |
| **Pin chip** | `hud-pin` | Hidden, pinned, long label | 40 portrait + name, `max-width`, ellipsis only past 220 |
| **Stories chip** | `hud-history-dot` | Hidden, shown | Dot + word, never a number |
| **Sheet + scrim** | `GameHud.Sheet`, `cards-sheet`, `recall-sheet`, `shelf-sheet`, `fb-sheet`, naming sheet | Closed, open, scrolled | `.surface`, translate on open, `SafeInsets` as padding |
| **Card** | Chronicle, person, first-year, recall, feedback cards | Head, body, actions | Content of a sheet, never its own surface |
| **Tab** | Best, Recent; music frequency | On, off | Selected is bold and underlined |
| **Toggle** | Settings pills | On, off | Word inside, knob moves |
| **Slider** | New | Value | Hit strip of 144 px, 4 line, 22 ring |
| **Bubble** | `firstrun-`, `moment-`, `showme-`, `shaping-`, `boxcomplete-bubble` | Who + line, chatter symbols | Tail is a rotated square. One at a time, second waits. Longer lines become a caption |
| **Label** | `pc-name-tag`, `pc-pin-tag`, tie labels, brass plate | Name, gold + pin mark, tie + mark | Line style and mark per tie |
| **Badge, dot** | `-dot`, pips | Dot, page dots, charge pips, New tag, ribbon | Dots never count |
| **Toast** | `hud-toast`, `tl-toast`, `fb-toast` | One at a time, queued | One queue, in `GameUiPanel`. Prompts outrank it |
| **Caption** | `moment-caption`, `boxcomplete-caption`, `firstrun-year-caption`, `tl-caption` | Line, line + who; in panel for the time-lapse | `--dark-plate`, or plain text inside the time-lapse panel |
| **Hint** | `firstrun-hint`, `moment-hint`, `boxcomplete-hint` | Word, ghost finger | Light pill, at most three words |
| **Banner** | `shaping-banner`, `visit-banner` | Tool in hand, visiting | Fixed exit, bottom row |
| **Big prompt** | `moment-watch`, `complete-prompt`, `firstrun-rain` | Hidden, ready | Prompt lane, no timer |

USS has no dashed border, so nothing in the UI is dashed. Locked tiles use the same solid edge as unavailable buttons.
The footprint's dashes are a world mesh, not USS.

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
- **Card stack:** one sheet, 30 %. Order is Back, Next, Show me, then the one primary at the right: Give, Watch,
  Remember or Done. Next is never primary. A story card has no filled button; its headline leads. Long cards scroll, snapped to whole
  lines. The last page drops "and 21 more" (`CardStackView.cs:469`): rule 8.
- **Person card:** name, Pin and Close in one row, the subtitle on its own row below, so it can't run under Pin. Tabs
  are About, Recent, Best. Tie labels sit at the far end of each tie line, off the name tag. The tools and the top row step
  back while it's up, as drawn. About ends in a story (question 35), so the sheet is never half empty. The camera follows
  a person who walks under the sheet, unless the player has moved it. Marks per tie: person for family, star for friends,
  alert for rivals, book for teachers, scroll for shared stories. Lines: moss heart for family, plain for friends and
  teachers, dotted for rivals, thin for shared stories. Under 700 pt tall, bottom padding drops to 14 as in §2. Built in #165.
- **Tray:** a low row of four tiles that fit the width, 30 %, no dim. A fifth tool makes the row scroll. Charges sit in the head. The reason for a locked or capped tool is a note
  under the row. Picking a tool removes the sheet.
- **Placing:** footprint on the ground, tool in hand, charges, Cancel. **Cancel's place is the user's call, see §7.**
- **Time-lapse:** the HUD leaves, so the only year is the tag on the playhead. A hairline band with era names as text, a
  44 pt hit strip, pin marks. The caption lives inside the panel (27 %), and its controls replace the play row.
- **Era card:** always the era at the playhead. Check the timing bug in captures/README.md when building it.
- **Feedback, recall:** sheets from the bottom edge. Feedback's Send is unavailable until there's text.
- **Notification ask:** at the close, first era only. The HUD stays down, the caption leads, and both buttons are
  secondary.
- **Visiting:** box name and years in the banner, with "Back to your box" beside them.
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
| Hint over a bubble on the wipe repeat | The hint rests while the reward line shows |
| Tie labels over the name tag | Labels at the far end of the tie |
| Two years on the time-lapse | HUD leaves, one tag on the playhead |
| Sheets out of the thumb zone | Every sheet sits on the bottom edge |
| The tall tray | Low tile row at 30 %, no dim |
| Time-lapse caption at 45 % | Caption inside the panel, 27 % |
| Controls with no link to their area | Skip row is a test-only group, Shelf is a labelled chip |

## 6. Contrast

Measured in the Unity render of the hero card (hero/README.md), so these replace the first pass's calculated figures. Each
state was captured with and without the words: ink is the darkest 2 % of the changed pixels, background the 1 % darkest
pixel in the text's box. No real night exists yet, so pure black stands in for it.

| Pair | Measured | Result |
|---|---:|---|
| Ink on frost, home view | 9.38:1 lowest | Pass |
| Ink on frost, close camera, dirty glass (worst game state) | 8.52:1 | Pass |
| Ink on frost, over pure black | 8.33:1 | Pass |
| Ink on flat, close camera, dirty glass | 10.64:1 | Pass |
| On-primary on primary | 4.53:1 | Pass, no margin: don't lighten |
| M on moss | 4.72:1 | Pass |

Frost stays at 72 %. On the home view the sheet covers only the shelf, so **dirty glass must be tested with the close camera.**

Not measured in Unity, so still sRGB calculations on our captures: ink on the gold tag 7.14:1, on-primary on the dark plate at
least 8.08:1. Ink directly on the world passes at 8.05:1 on the default view and falls to 1.0:1 over the glass frame on a close
camera, so it needs the quiet backing. That's the frost tint with no blur, which should behave like the black row above.

## 7. Open, for the user

**Placing's Cancel.** Principle 1 in research/ui §8 says controls sit near the action. The signed-off wireframe has
Cancel in the bottom row. Both are drawn on the 3.4 board.

| | A: bottom row (wireframe) | B: beside the footprint (principle 1) |
|---|---|---|
| Where | Fixed, bottom right, 108 × 56 | 44 tall, up and right of the footprint, clamped to the screen |
| For | One exit in one place (rule 7, research rec 4). Never moves, always in thumb reach | Short travel, near the thing it cancels |
| Against | The far end of the screen from the footprint, a longer move | Moves with the footprint, and a tap meant for the ground can land on it |
| Recommendation | **A**, with the tool bar joined to it. A wrong tap on B would place or cancel by accident (#145) | |

Also open. Each has a recommendation; none blocks the build order.

1. **Quiet backing:** only off the home view, as drawn. Always on is safer but close to the pill D3 removed.
2. **Long cards:** keep the scroll inside 30 %, snapped to whole lines.
3. **Story cards** with no filled button: keep.
4. **Shelf and Stories** share a slot: Stories wins, Shelf moves into Settings while it's up.
5. **Sheets over 30 %:** accept them, and move the silent notice into the Sound row so settings stays at 59 %.
6. **Setting picker:** two settings, desert oasis preselected. Give Mountain village a sub-line.
7. **New words:** approve them, or replace them: "Stories", "Back to the story", the silent notice, the reset body, "The next box",
   "Begin", "Rename your box", tab "About". Change "How often" to "Music plays". Samples, not game text: crowd "Tob", "The
   Guild Years", "The Quarry family", the snapshot file name, licence rows. Doc lines: "Mira and Tob…", "They'll wait for you…".
   The invalid-name sample stays masked.

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

- The tester's [polish] findings wait for the user.
- Legibility at 375×667 beyond the wish card, and a night background once one exists. Tablet isn't drawn.
- Frost on a phone, in the final build (limits.md list). If it fails, flip the switch.
