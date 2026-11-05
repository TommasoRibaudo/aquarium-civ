# Where the people's words show: review and proposal (#327 phase 1, draft, 2026-11-04)

Status: **for the user to pick an option.** Nothing changes in code until one is picked. Phase 2 builds it.
**[est]** = a guess to tune. **[measure]** = a number the Editor phase reads off a real frame.

Why (the user, 2026-11-04, from the tester's finding on #318): at the normal zoom the people are too small to carry a
bubble, so tree lines and other lines go unseen. *"Shouldn't these be lines that happen at the top like other text? For
some reason we have two ways of showing these bubbles."*

Zoom words used below. `BoxCamera.zoom`: 1 = the whole box fits the width (the default view, "home" is anything
>= 0.92), 0.2 = closest, 1.3 = farthest (`CameraReach`). The chatter and voice code use `zoom01 = InverseLerp(1.3, 0.2,
zoom)`: 0 = farthest, 1 = closest. **The default view is zoom 1 = zoom01 0.27.**

| zoom | zoom01 | what the code does there today |
|---|---|---|
| 1.3 .. 1.08 | 0 .. 0.2 | chatter: 1 mark alive at most (`ChatterRules.zoomedOut`) |
| 1.08 .. 1.025 | 0.2 .. 0.25 | voices: far band (a lone muffled shout every 20 to 40 s, group marks silent) |
| **1.0 (default)** | **0.27** | voices: mid band, muffled; marks: up to 4 |
| 0.86 | 0.4 | voices stop being muffled |
| 0.75 | 0.5 | the old babble ramp's start (`ChatterPlan.BabbleVolume`, read only by tests now) |
| 0.695 | 0.55 | voices: near band (exchanges of 2 to 3 turns) |

The ambient bubble does not use zoom at all: it uses the speaker's height on screen (`ShowMeDirector.DollPixels`, in
pixels of a 2556-tall frame): no bubble under 12 px (`TinySpeakerPx`), the bubble narrows from 640 to 470 panel units
between 100 and 30 px. Ritual symbols hide under 14 px (`RitualBubbleView.TinyDollPx`). So "too far" has five
different thresholds today. **Measured (2026-11-04, 1179x2556, Marta, midday box, living land):** the doll is **28 px** tall at zoom 1, **52 px** at zoom 0.45 and **22 px** at zoom 1.25 (about 28 / zoom).

## 1. Inventory

Channels: **B** = the over-head bubble (`showme-bubble`, a tail to the head, `SpeakerRing` under the feet), **C** = the
ShowMe caption plate (`showme-caption`, top lane, line + optional name, **no ring**), **MB/MC** = the moment overlay's
own bubble (fixed board spot, no tail) and caption (`moment-caption`), **FB** = the first run's bubble
(`firstrun-bubble`), **M** = chatter marks (… ♪ ♥ ! ?, one mesh, ~20 pt), **R** = ritual symbols (sun, doll, eye,
notes, ~24 pt), **T** = HUD toast (bottom lane). The word = `BubbleHost` (ranks World < Line < Moment, one holder).

| # | Source (code) | Trigger | Shows on | Reads at | How often | Sound | Priority / stacking |
|---|---|---|---|---|---|---|---|
| 1 | Ambient lines: moment, stories, bank, traits (`WorldBubbles.Next`, `ShowMeDirector.StepBubbles`) | checked every 1 s, a speaker in the screen's middle band (viewport x 0.12..0.88, y 0.42..0.88) | B; C when > 48 chars (`GlassBubble.TooLong`) | B only near (doll >= ~30 px); C anywhere | 8 s warm-up, then >= 20 s apart (6 s for an evening line), same speaker >= 90 s; 3.5..7 s each; never repeats (400 kept) | none | World rank; waits for C, echo, quiet reasons; a line shown to nobody is still counted said |
| 2 | Want lines (#181) (`WorldBubbles.Wants`) | every 3rd bubble, the case is true | B over a random **crowd** person, **no name**; C with no name | as #1 | same want >= 300 s | none | as #1 |
| 3 | Tree lines (#318) (`PeopleVoice.TreeLinesNow`) | tree planted + misted, every 3rd bubble | B, mostly C (most lines are 50..75 chars); crowd lines (speakerId -1) get **no name** on either | as #1 | as #1 | none | as #1; debug "Tree line" |
| 4 | Show me echo line (`SayEchoLine`, `Say`) | Show me on a card | B (camera has flown close); C when long or no doll | close (the camera flies to `ShowMeCore.PersonZoom`) | per tap | ShowMe cue at the flight | Line rank, takes the word from #1 |
| 5 | A death's words on Show me (`StartEcho`) | Show me on a death | B / C | close | per tap | Flower cue | Line |
| 6 | Relic moment lines (`GlassVoice.SayLines`) | relic found while watched | B; when B can't (word busy, no doll): **MB/MC** in quotes, with name | wherever the camera is (a nudge toward the spot) | per relic | Discovery cue at the start | Line; **falls to a second bubble system** |
| 7 | Crumb landed line (`GlassVoice.CrumbLanded`) | the Sky Crumb lands | B / C, 3 s | wherever the camera is | once | MessDrop | Line; **dropped silently** if the word is busy |
| 8 | Wish thanks (#191) (`WishPayoffView.Say`) | a wish Given/Met | B / C | camera nudged toward the wisher if off screen | per wish | WishGiven / CheerSmall | Line; held during the first run, else **dropped silently** if the word is busy |
| 9 | Choice scene ask (#224) (`ChoiceSceneView.SayAsk` → `SayLine`) | Show me on a dilemma | B / C, 75 s | framed (camera holds both sides) | per dilemma | ShowMe cue; Cheer on answer | Line; quiets #1 and marks while it holds |
| 10 | First run: wish over the wisher, reward line (`FirstRunView`) | first-run steps | FB with tail; "Their first year" caption | the first run's own camera | once | Discovery, CardArrive | Line, asked each frame; quiets #1 |
| 11 | Era / last moment lines and beat captions (`MomentPlayer`, `MomentOverlay.SayMoment`, `SetCaption`) | era change, the last moment, festival | MB (board spot, leader's name), MC | staged camera | per era | moment cues | Moment rank: takes the word |
| 12 | Evening talk teaser (`EveningTalkView`) | the close of a long sitting | MC (the teaser) + a name tag `close-speaker` over the role-holder | staged (zoom 0.62) | once a sitting | (music) | nothing competes |
| 13 | Chatter marks (#144) (`ChatterPlan.Tick`, `ChatterView.Draw`) | two or more awake people standing within 0.5 | M over the speaker | any zoom, ~20 pt | each group every 3..6 s, 2.2 s each (! 1.2 s); 4 alive (1 when zoom > 1.08) | only some, see #14 | fade when any real line is up (B or C) or a quiet reason holds |
| 14 | Voices by distance (#316) (`VoicePlan`) | far: lone timer; mid: group turn + its own 9..18 s gate; near: exchanges | lone voice and replies add their own M (1.6 s) | by band | far 20..40 s, mid 9..18 s, near: rest 8..16 s, 2 exchanges at once; none at night for lone voices | babble, muffled above zoom 0.86 | stops while a real line is up |
| 15 | Knock reaction ! (#161) (`ChatterView.Knock`) | the Keeper taps the glass | M (!) over up to 4 nearest | any | per knock | the Keeper's TapGlass, **no voice** | kept through a real line |
| 16 | Ritual symbols (#276) (`RitualBubbles`, `RitualBubbleView`) | relic moment, festival evening | R over up to 5 | hidden under 14 px | per beat, <= 5 s | **none** (Discovery at the start only) | chatter is quiet during a relic moment; B lines (#6) can sit with R |
| 17 | Cards: story quote (`CardStackView` `cards-quote`, name) | a card opens | in the card | n/a | per card | card cues | quiets #1 (sheet open) |
| 18 | Person card line, wish (`PersonCardView`) | tap a person | in the card | n/a | per tap | n/a | quiets #1 |
| 19 | Toasts (`GameHud.Toast`) | hints, "Welcome to X." | T | n/a | on events | n/a | its own lane, not people's words |
| 20 | Time-lapse caption (`tl-caption`) | time-lapse | in its panel | n/a | per story | n/a | the world is quiet |

### Mismatches

1. **A line with no speaker you can see.** Long lines (> 48 chars) go to C, and C has no `SpeakerRing`
   (`Speaking` follows only the bubble element). Crowd lines (wants, most tree lines, crowd ambient) carry no name on B
   or C. Together: the debug "Tree line" at the default view is a nameless caption, nobody marked. Same for every long
   echo, relic or wish line.
2. **Two ways for the same line, chosen by length, not by what the player can see.** Short lines go over a head that
   may be 25 px tall; long ones go to the top. The zoom never enters it. And for ambient lines the bubble gate (12 px) also
   blocks the caption: a long line whose speaker is tiny is dropped, though the caption doesn't need the doll. (The
   choice scene's halos scale with raw zoom over 0.3: a sixth "distance" rule.)
3. **Two caption plates and two bubble systems** for spoken lines: `showme-caption` and `moment-caption`,
   `showme-bubble` (tail) and the moment bubble (board spot). Relic lines (#6) fall from one to the other. And the
   ShowMe caption never takes the word (it checks `Free`, it doesn't `Ask`), so a first-run or moment-overlay line can
   come up while it shows: two lines on screen at once (spec §5's one-message rule).
4. **Marks and voices out of step.** Group marks fire every 3..6 s per group, but in the far band no group mark is
   voiced and in the mid band (the default view) only one per 9..18 s is. Most marks at the default view are silent;
   lone voices bring their own mark, so the same "…" sometimes sounds and sometimes doesn't. Ritual symbols and the
   knock's ! never have a voice. At night marks keep their pace while voices hush. `ChatterShow.babble` is a dead
   field from #144.
5. **Spoken lines are silent, wordless marks babble.** The real line, the more important of the two, has no sound of
   its own (§10: same event, same sound). Lines dropped while the word is busy (#7, #8) and lines counted said with no
   one to see them (#1) burn content unseen.

## 2. The options

Common to every option (phase 2 builds these whichever is picked):

- **One word.** Every spoken line (B, C, MB, MC, FB) asks `BubbleHost` for the word; C becomes a real holder. Ranks stay
  Moment > Line (echo, ask, relic, crumb, wish, first run) > World (ambient, wants, tree). Marks and voices are below
  World and never hold the word.
- **One caption.** ShowMe lines and relic lines share one caption element and one "who" format; MB stays only for the
  era / last moment, which have their own staged camera.
- **A line waits instead of vanishing.** Lines #7 and #8 wait for the word up to 10 s **[est]**; an ambient line that
  nobody saw is not counted said.
- **A line has a voice.** When a line shows, the speaker's own babble plays once (one clip from their voice, the
  `ChatterBabble.Chat` kind, at the voice band's level); the marks fade as today.
- **Marks in step with voices.** A group mark shows only when a voice plays for it (so at the far band group marks
  stop and only the lone voice's mark shows); at night marks keep only the soft kinds and the night pace. Ritual
  symbols: one soft cue per beat (the festival's or the relic's), not per symbol. The knock's ! stays voiceless (the
  sound is the Keeper's knuckle).
- **Pacing between channels.** One line at a time across the box; after a line ends, 3 s **[est]** before marks come
  back (the staggered restart already exists) and the 20 s ambient gap counts from the end of any line, staged or not.
- **One "too small" number.** One `SpeakerScale` read (the doll's height in px) replaces `TinySpeakerPx`, `TinyDollPx`
  and the `FitToSpeaker` ramp, so the thresholds below are one place.

Closed-world check (all options): the people's words stay theirs; the name line under them is the narrator's
("Mira, by the tree"). Crowd speakers need a narrator label, not a name: "a child, by the tree", "someone at the fire"
(place words from the box only; never "tank" / "glass" in the people's line, which `PeopleVoice.Allowed` already
checks). Phase 2 adds the label words to the closed-world content test.

### Option A: the zoom decides (the issue's example)

- **Far and default (doll under 45 px, measured: about zoom > 0.62, since 28 px / zoom = 45 px):** every spoken line goes to the top caption: the
  line, then "Name, at the place" (or the crowd label). The `SpeakerRing` goes under the speaker for as long as the
  line shows, bigger and pulsing once at far zoom (its 0.18 radius reads as an ellipse about 40 px wide and 12 px tall at the default view, measured on the 0b frame). A
  speaker off screen: the caption's name line gets a small arrow toward them (left/right/down), no camera move.
- **Close (doll 45 px and up):** over the head as today, any length up to the bubble's 4 lines; longer still goes to
  the caption, with the ring.
- **Marks:** wordless ambience in step with voices (above). They fade while a line is up.
- Hysteresis: the switch flips only at the start of a line, never mid-line.
- §2: works at the default zoom and zoomed out (the line no longer needs the doll). §6: the caption sits in its lane,
  under the HUD's top row, never over the speaker (the ring is in the world). §10: same event, same sound. §12: the
  ring + name + arrow: colour is not the only signal; text at the caption's 17 pt.
- **Cost:** `ShowMeDirector` (routing, ring on captions, arrow), a pure `LineRoute` in `View/ShowMe/Core` (size →
  channel, hysteresis), `SpeakerRing` (scale by zoom, a pulse), `GlassCaption` (an arrow slot, outside the fence:
  `UI/Glass`), `ChatterView` / `VoicePlan` (in step), `GlassVoice` + `WishPayoffView` (wait, outside the fence),
  `MomentOverlay` (relic fallback to the one caption). Tests: `LineRouteTests` (pure, also in SimTests), VoicePlan
  in-step tests, a PlayMode routing test at zoom 1 and zoom 0.4 (caption + ring + name at 1; bubble + ring at 0.4),
  the stacking test (a first-run line while a caption is up). Cheats: "Line: caption here", "Line: over head here",
  "Zoom: home / close / far", "Speaker off screen line". **About 2 days.**

### Option B: the caption for every line

- Every spoken line, at any zoom, goes to the top caption with the name (or the crowd label) and the ring under the
  speaker, plus a small speech mark (a quote glyph in the chatter style) over the speaker's head while the line shows,
  so the tie also reads in the world. The over-head bubble is retired for spoken lines (it stays only for the first
  run's wish, which points at a person to tap).
- Marks as in the common part.
- §2 and §12: the most readable everywhere, one place to look. §6: one more fixed piece at the top, always the same
  place. Against pillar "the glass is the interface" and research 12: close up, the words leave the people; a close
  look at two friends talking reads like subtitles.
- **Cost:** the smallest: `ShowMeDirector` (route all to the caption, ring on captions, speech mark through
  `ChatterView`'s mesh as a sixth shape), `ChatterView`, `GlassVoice` / `WishPayoffView` (wait). The bubble's tail code
  stays for the first run. Tests: routing (always caption), ring + mark up with a caption, stacking. Cheats: "Line:
  caption here", "Speech mark". **About 1 day.**

### Option C: the size decides, with a speech mark between

Three tiers by the speaker's size on screen, decided at the start of each line:
- **Close (doll >= 45 px, about zoom <= 0.62):** over the head (as A).
- **Middle (the default view: 15 to 45 px):** the line on the caption with the name, and over the speaker a speech
  mark (a quote glyph, the same size and style as the chatter marks, in the ring's warm white) with the ring under them:
  the mark is where you look, the caption is where you read.
- **Far or off screen (under 15 px, or off screen):** caption with the name and an edge arrow; no mark (it would be
  noise at that size, like the zoomed-out mark cap).
- Marks as in the common part; the speech mark is the one mark that isn't wordless ambience, so ambient marks near the
  speaker fade while it shows.
- §2: the best tie between line and person at every distance. §6: the most pieces (caption, mark, ring, arrow). §12:
  shape + position + text.
- **Cost:** A's cost plus the speech mark (a sixth shape in `ChatterView`), a three-tier `LineRoute`, one more PlayMode
  case (the middle tier at zoom 1). **About 2.5 days.**

### Recommendation

**Recommended: Option A**, with the switch on the speaker's size (one number, 45 px, confirmed by the measure above: 28 px at the default view is well under it, 52 px at zoom 0.45 is over), not raw zoom, so
a tall phone and a tablet agree. It answers the user's question directly ("lines at the top like other text"), keeps
the words on the people when the player has come close to look (the reward for zooming in), and reuses what exists:
the caption lane, `SpeakerRing`, the evening talk's caption + name-over-speaker pattern (#12 above, already approved).
If the Editor shots show the ring alone is too faint at the default view, adding C's speech mark is a small step on top
of A (one shape), so A doesn't close that door.

## 3. Shot list for the Editor phase (1179x2556, portrait)

Staging, for every shot: play mode, a box past the first run with notables named and the tree tended (if the tree
isn't tended, the debug "Tree line" tends it and says so), daytime ~10:00 (SimDebugPanel → Time), the HUD up, no sheet.
No zoom cheat exists (the "Voice band" button only forces the sound's zoom): set it with `eval`, e.g.
`var c = UnityEngine.Camera.main.GetComponent<AquariumCiv.View.BoxCamera>(); c.EaseTo(c.target, c.yaw, c.pitch, 1f, 0f);`
(1 = default; 0.45 = close; 1.25 = far). First **[measure]**: the doll height at zoom 1 with `PeopleView.TryGetDollTop`
/ `TryGetDollFeet` → `WorldToScreenPoint`, in px of 2556; it sets A's and C's thresholds.

Caption geometry for mock-ups (from code and `ui/spec.md`): panel 1080 wide → 1179 px is x1.092; caption top 275
panel units with the HUD up (= 300 px), dark plate (ink 96 %, radius 18 pt), line Lora 700 17 pt in `#EFE3CC`, the
name below it in the quote style. Ring: warm white (1, 0.95, 0.78), 0.8 peak, an ellipse on the ground at the feet.

| Shot | State to stage | Cheat | The mock-up must show |
|---|---|---|---|
| 0a today | zoom 1, tree tended | SimDebugPanel → Show me → **"Tree line"** | as is: the nameless caption, no ring (the problem) |
| 0b today | zoom 1, a named notable walking mid-screen | SliceDebugPanel → **"Line on walker"** | as is: the small bubble at the default view |
| A1 | as 0a, but a line with a named speaker standing mid-screen (pick a tree line from a trait, or "Line on walker" for the base frame) | "Line on walker", then hide the bubble in the mock | caption: the line + "Mira, by the tree"; ring under Mira, a little bigger; no bubble; marks faded |
| A2 | zoom 0.45 on the same person | "Line on walker" | the over-head bubble with tail and ring (today's look; real frame, no mock needed) |
| A3 | zoom 1, the speaker off the screen's left edge (pan) | "Tree line" | caption with the name and a small arrow pointing left on the name line |
| A4 | zoom 1, a crowd line (a want) | SliceDebugPanel → a **WantCase** button | caption with a crowd label ("someone at the fire"), ring under that person |
| B1 | = A1's base frame | as A1 | caption + name + ring + a small quote-glyph mark over Mira's head |
| B2 | = A2's base frame (zoom 0.45) | as A2 | the same caption at the top, no bubble, the mark over the head (shows B's cost close up) |
| C1 | = A1's base frame | as A1 | caption + name + ring + the quote-glyph mark (C's middle tier) |
| C2 | zoom 1.25, the same speaker | "Line on walker" | caption with the name, no mark, ring (or arrow if off screen) |
| C3 | = A2 | as A2 | the bubble (same as A2) |
| M1 (all) | zoom 1, a group of 3 at the fire, ~18:30 | **"Gather 6"**, **"Force chat"**, **"Voice exchange here"** | marks over the group; for the in-step proposal, a label or caption on the sheet: "each mark here has a voice" (it's a sound change: no mock, a short screen recording with sound if possible) |

**Shots (done 2026-11-04).** Real frames from the game, mock-ups composited from them with the game's own caption look
(plate `#4C3C2D`, Lora Bold 52 px, name in Lora Medium). All in `docs/design/results/p8/feedback2/`: today
[`327_today.jpg`](results/p8/feedback2/327_today.jpg) (0a + 0b), A [`327_option_A.jpg`](results/p8/feedback2/327_option_A.jpg)
(A1 mock, A2 **real** frame at zoom 0.45, A3 mock with the arrow), B [`327_option_B.jpg`](results/p8/feedback2/327_option_B.jpg)
(B1 + B2), C [`327_option_C.jpg`](results/p8/feedback2/327_option_C.jpg) (C1 + C2 at zoom 1.25), plus the want line A4
[`327_extra_A4.jpg`](results/p8/feedback2/327_extra_A4.jpg) (today's want line is a nameless caption and no ring; the mock adds the
crowd label and ring). C3 = A2. **M1: hear it in the build** (a sound change, no shot). Limits: the speech mark and ring in the
mocks are drawn, not the game's; the stray chatter mark (note, dots) over another person is a real mark of the base frame.
At zoom 1 the ring and the mark are small (a 28 px doll): they read as a hint, which is the point to judge.

Present to the user as three pairs side by side: A (A1 + A2, A3 as an extra), B (B1 + B2), C (C1 + C2), with 0a and
0b as "today".

## 4. Open questions for the user

1. Which option (A, B or C)?
2. Should a spoken line have its own short voice (the speaker's babble once at the start)? The proposal says yes.
3. Crowd lines: a narrator label ("a child, by the tree") or always pick a named person on screen to say it?
4. The far-band group marks: drop them (in step with the voices), or keep them silent as a visual hint of talk?
