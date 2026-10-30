# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-10-30._


## Overnight run 2026-10-29/30 (agents picked the ★ option and went on; change any of them)

### #82 Shaping charges and caps (sim, `SimCore.Shaping.cs`, `SimConfig.shaping*`)
- ★ Charges start at 2, cap 3 + 1 per era; refill +0.5/year while mood ≥ 0.6, plus by cleaning (full glass 0.5, all
  overgrowth 0.4, 0.08 per trash heap). First guesses for E2 to tune.
- ★ Caps per box: 1 rain until answered, 4 groves, 3 channels (doc says "≤3 [est]"), 2 herds with at most 1 fish shoal
  (one pond).
- ★ Take back is allowed until the people answer (not only until they "use" it).
- Era names only for now (orchard, mill race, goats/wool flock): their sim effect is the old answer logic.

### #86 Era change, festival, peak (sim, `SimCore.EraMoment.cs`)
- ★ The festival year lifts the mood target by +5 % (changes the economy a little after each era). Alternative: party
  only, no lift.
- Peak rule: town ≥ 40 years old + all 7 town seats held once + box full 3 years. On 12 seeds only 5 peak by year 120
  (at years 71–110); the design expected ~100–120. ★ Keep 40 for now; tune with E2 (lower the town-age or "full" bar if
  too many boxes never peak).
- ★ Player-facing years start at 1 ("years 1–118" on the plate).
- The cleared-hearth line says "old fire ring", not the design's "old camp ring" (a test forbids "camp" in town text).

### #74 Relics and the people's quotes (sim, `SimCore.Relics.cs`)
- ★ Sky Crumb fallback (the design has none): the first shaping after the camp.
- ★ The Left Sandal can also be lost in the town if no "lost" story came earlier, so it can't be missed.
- ★ In the camp, line 28 says "Ask the sky" instead of "the window".
- ★ A wipe counts once per game month for the Keeper Doll's prop, so the cloth will often win. Alternative: weight
  shapings higher.

### #67 Open jobs get a notable (sim, `SimCore.Jobs.cs`)
- The issue said "no effect on the economy", but filling a job shifts who holds the crafts, which feeds the role contests
  and who rises from the crowd, so the economy hashes moved (food and wood rules untouched; sweepers excluded because
  they do change the economy). ★ Accept and re-pin (done). Alternative: fill only from people outside the contests.
- ★ A job change (also one forced by a lost building) starts a 3-year cooldown before the next switch.

### #83 Wishes in cards (sim, `SimCore.Wishes.cs`)
- ★ About 12 % a month with an 8-month gap after each wish (≈ one wish every 1–2 years), one open wish at a time,
  lapses quietly after 24 months. Estimates for E2.
- ★ Job → tool table: farmer rain/channel, hunter deer, fisher fish (always the pond), woodcutter grove.
- ★ A wish only needs the tool unlocked and under its cap; with no charge, Give fails and the wish stays open.
- ★ Placing the same tool yourself, anywhere, counts as meeting the wish.

### #30 / #109 Role holders with the broom
- #67's job filling can give the broom to a role holder (seed 7: the Master Builder). In the planner they work the role
  in the morning and sweep in the afternoon, while the sim counts them as a full sweeper. ★ Keep (it reads fine).
  Alternative: #67 skips role holders when filling the sweeper job.

### Scope added overnight (orchestrator)
- Many listed tickets depend on issues that weren't in the list. ★ I did the sim prerequisites #73 (history records)
  and #87 (shelf / box 2) so #78, #76 and #93 can go ahead. UI prerequisites (#108 design system, #75 cards, #84 shaping
  tray, #95 settings, #79 time-lapse screen, #80 book) are not started; the tickets that need them are built against what exists (the #36 HUD and sheets, the debug
  panel, GameSettings) and note what's left for when #108/#75/#84/#95/#79/#80 land: #39 (pin + card in the #36 sheet
  style), #55 (verify), #76 ("Show me" callable from the debug panel until #75's cards), #85 (tools placed from the
  debug panel until #84's tray), #91 (the still is saved; the book page comes with #80), #94, #96 (settings toggles
  minimal until #95), #81/#100 (hooks wired where UI exists).

### #87 The shelf and box 2 (sim + save v3, `Shelf.cs`, `SimCore.Museum.cs`)
- ★ A museum visit lives through at most 40 game years; a museum box keeps at most 120 new lines and 40 dead
  museum-born people.
- ★ Box 2's setting (`desert_oasis` / `mountain_village`) is only a label for now; the view/terrain has to read it.
- ★ Founders carry age, name, trait and lifespan, not jobs or roles; fewer than 2 founders → random notables are added.
- ★ All found relics go on the old box's plinths; the carried ones are flagged `carried`.
- Not wired in Unity yet (no UI calls `StartNextBox` / `Visit`): that's #93.

### #73 History records (sim, `SimCore.History.cs`)
- ★ Low-salience chronicle lines older than 30 years are pruned once a year (book-worthy and top-five entries kept);
  pruned lines still count as "told" so wording doesn't restart. Saves are ~6 % bigger than with no records.
- ★ A building's "look" is its era for now; finer looks later.
- ★ An era's title comes from its top-salience entry (relic name, a kind→phrase table, or a person), fallback "The Camp
  Years". Pin time counts game months, so offline catch-up counts at the slow pace.

### Tester batch 1 (2026-10-30): [polish] items for you to decide
Report and screenshots: `AquariumCiv/Temp/agent/tester-batch1/` (local). The [real] findings are being fixed.
- #30: people heap up at meals (9 dolls within 0.12 u at the tavern; 3–4 on one spot at homes). Reads as a blob at phone
  size. ★ Spread meal/home slots like work slots.
- #30: work poses are hard to read at phone size (dolls ~15–20 px tall at default zoom, ~40 px after fly-to), and at
  midday/winter most people eat, sit out or repair at home, so a 60 s glance shows little work. ★ Bigger/bolder poses and
  props, and more outdoor work in the day; or accept until the camera work in M3.

### Tester batch 1 fixes (sim + life)
- ★ A shaping answered while the town is at the building cap gets a harvest line credited to a person ("Ada Lund's
  flock grazed the new herd"), no building. Alternative: upgrade and name an existing building (needs design).
- ★ The bread quarrel needs a Windmill (there's no bakery building); the well lines need a Well.
- Walks (#109): plain stays/carries a person can't reach in time are now skipped; a late walk may eat up to 3/4 of the
  next stay; one shelter per storm. Remaining worst walk 1.88× the pace, at dawn (the clock runs faster then): timing
  walks in real seconds everywhere would fix it — a bigger change, not done.
- Seen, not fixed: `Settlement.HomeOf` picks a home by hash over the yard's home count, so a new home can move a
  household; and one sweep case depended on query order (maybe lazy settlement growth). ★ File as issues.

### #37 Care in the slice (View, `CareView.cs`, `GlassMask.cs`, `MessProps.cs`)
- ★ A wipe cleans every pane the finger crosses (far panes from the front); the soil strip never gets grime.
- ★ "Spotless" = mean grime < 4 % (faint streaks snap clean); a full glass takes ~3 passes (≈ 3 s of finger path).
- ★ A tap on glass in clean mode is the wipe's, so it doesn't select a person (the design wanted taps to still select).
- ★ Mess seen through the open top is reachable without wiping first.
- ★ Clean mode ends 2.2 s after the last mess goes, or after 30 s with no touch (guesses to tune).
- Sounds are procedural placeholders (rub, chime, swish, pop); no haptics yet. Trash/weed props are small at default
  zoom — your call on their look and size.

### #70 Camera mode
- ★ A short tap anywhere brings the UI back (plus a fading "Tap to show the UI" pill). Alternative: only the pill.
- ★ The Photo button sits top right under Feedback. Alternative: bottom row or Settings.
- ★ A slow auto-orbit starts after 4 s idle, on by default (not saved in settings yet).
- Open: should a tap on a person in camera mode also select them?

### #34 Town kit — look sign-off is yours
Screens: `AquariumCiv/Temp/agent/t34/` (local; town day/night, zoomed, kit sheets, before/after).
- The slice has **no night lighting yet** (the clock leaves the lamp level at 0). ★ File an issue for day/night light.
- The sim never builds Guildhall, Shop or Cottage lots; workshops come only from town shapings or the debug panel.
  ★ Let the town's economy build a guildhall and workshops (sim issue).
- Cobbles are a quiet grey at default zoom; a stronger pattern needs a finer ground grid or decals.

### #78 Time-lapse replay (phase 1, `Sim/Replay/TimeLapse.cs`)
- ★ Story pins: the top 24 chronicle entries with salience ≥ 0.5 (no term lines, no era-ready). Add shapings and the
  remembering stone as pins?
- ★ The timeline ends at "now" (the open year scrubs up to the current month).
- ★ Play speeds 1/2/4/8/16 years per second, default 4 (120 years ≈ 30 s).
- ★ An era change during replay shows as each building re-looking; alternative: replay the era's rebuild wave.

### #88 Moment director + era-ready waiting state
- ★ The town moment orbits 85° (BoxCamera allows yaw −30..55), not the doc's ~120°. Alternative: let the rail leave the
  player's range during a moment.
- ★ With no leader the vote line is generic ("The village chose its headman."); naming the winner needs a sim preview of
  the vote.
- ★ Leaving after the cheer drops the remaining beats; the notification question is then asked at the next moment.
- Guesses to tune: waiting piles at 12/72/288 months waited, "Wipe for a clear view?" at grime ≥ 60, the camera keys.
- A stationary press hurries ×2, a moving one orbits.

### #92 People at the glass and relic moments (phase 1, Life)
- ★ People stand at the glass on the camera's wall; the side changes only when a view day turns (live following would
  re-plan the day). Alternative: follow the camera live.
- ★ The Sandal turns up at the sim's spot (old farm/camp), not under the vine actually pruned.
- ★ The tap-back scholar is the sim's first scholar, who may walk a while before knocking.
- ★ Relics found while you're away get only the "Show me" echo, not the full moment. Alternative: replay on return.
- Visit rates are first guesses (stage 2 kids 70 % of afternoons, adults at dusk 50 %, stage 3 measuring 70 % of
  mornings, stage 1 after the crumb 50 %).

### #81 / #100 Telemetry (phase 1, `Runtime/Telemetry`)
- ★ D1/D3/D7 = opened on calendar day install+N in the phone's timezone (alternative: rolling 24 h windows).
- ★ The recall prompt asks on days 3–7 (not only day 3), at most 3 dismissals, never twice a day, never over a moment
  or cards.
- ★ A new "open" = cold start or a return after ≥ 5 min away.
- ★ ~1 MB log per box (256 KB × 4 files); the oldest is dropped in a heavy week (retention still works).
- ★ Metrics per install (all boxes merged). Free-text recall answers are not logged; they go through Feedback.

### #89 Camp → village moment — look sign-off is yours
Screens: `AquariumCiv/Temp/agent/m89_*.png` (local).
- ★ The fire disappears at the green beat (the doc says "shrinks to a hearth"; the village has no hearth piece).
- The green clearing is subtle at default zoom. ★ Make it stronger in #91.
- ★ During the wave people just return to their day (the doc has someone walk to each tent before its puff).
- ★ Rebuilds are an instant swap with a puff: no scaffolding, sound or haptics yet (sound comes with #96).
- ★ The village shows a view-only well until the sim builds its first. Alternative: the sim builds the well at
  AdvanceEra.
- Speech bubbles last 4 real seconds, so when hurried they spill into later beats.

### #90 Village → town moment — look sign-off is yours
Screens: `AquariumCiv/Temp/agent/m90_*.png` (local).
- The cobbled square's ground is faint at this zoom; the stalls carry the beat. ★ Stronger cobble look later.
- ★ Cobbles reach every main path (tier "path" and up), not one "main path".
- Placeholder beats (puffs + role/gather scenes): scaffolding, the hammer-knock scale, the cleared-hearth stone, the
  headman's walk, banners and the lord's chair are not built yet.
- A village without a market keeps its green in the town (#34's rule).

### #91 Era festival — look sign-off is yours
Screens: `AquariumCiv/Temp/agent/m91_*.png` (local), incl. the book still `m91_still.png`.
- In some layouts the green's houses hide the tables and dancers at default zoom. ★ Put the tables in front of the well.
- The green lawn is still subtle at default zoom (per-vertex ground colours). Bolder needs a palette call from you.
- ★ The book still is a fresh render at the cheer (pose saved), not a re-render from the year snapshot as the doc says.
- Not done: lanterns, more food on the tables, festival-night mood.

### #39 Person card and relationships (phase 1, `SimCore.People.cs`)
- ★ A pin stays on a notable who dies (their card becomes a memorial; pin time stops). Alternatives: clear it, or pass
  it to an heir.
- ★ The living card has both "last 3 stories" and "3 best stories"; phase 2 shows the last 3 with "best" on a tap.
- ★ "Shared stories" ties (2+ co-mentions) stay on the card only, not drawn in the world.
- ★ A pair that are both rivals and siblings is drawn as rivals.
- ★ With no bank line for them, the card's quote is a crowd line in their voice.
- Not done: the book's epithet ("the Sky-Bread Chief"). Founders of box 2+ may need `founderIds`.

### #94 First run (phase 1, `Sim/FirstRun`)
- ★ Step order Wipe → Patch → Name → Rain → Year → Card (decision 2026-10-29: wipe and patch first; the slice plan had
  naming first).
- ★ Never blocks: an untouched plate keeps the suggested name after 75 s; skipping the rain step drops the rain on the
  suggested spot so the first year still has its story.
- ★ The first card is the rain's standard credit line (not the design's "mushrooms by the stream" flavour).
- ★ Light name filter (whole words, leetspeak, a few stems); names 2–20 characters. The name is set directly; the
  people naming their camp later in a card (design Choice 2) is not done.
- ★ Hint texts: "Wipe the glass", "Flick the weeds", "Name it", "Give them rain", "Rub a spot".

### #92 Relic moments and the glass (phase 2) — look sign-off is yours
Screens: `AquariumCiv/Temp/agent/glass_*.png`, `relic_*.png` (local).
- Keeper Doll and Sky Crumb props are modest at full-box zoom. ★ Make them a bit bigger.
- ★ Every tap on the box outside clean/camera mode is a knock on the glass (people react by belief stage). If tapping a
  person should select them, the two need sorting out (ties to #39's pin).
- The knock uses the care chime for now; its own glass sound comes with #96.
- The Lens moment starts at dusk on the view clock, but the light looked like day: the sky may follow another clock.

### #96 Sound (phase 1: cue model + CC0 clips) — please listen
Clips in `Assets/Audio/Resources/Sfx/` and `/Ambience` (Kenney CC0 + OpenGameArt CC0; logged in assets-log.md).
- No CC0 crowd cheer found: ★ keep the code-made cheer. Alternative: Freesound CC0 (needs a login).
- The fiddle is pizzicato strings; the flute is a cartoon flute-and-drum tag nobody has listened to yet — your ear.
- ★ Phone on silent: music and ambience off, small cues still play quietly. Alternative: silent mutes everything.
- ★ Mess only thins the birds (no fly buzz, to avoid guilt-trip).
- Storm wind reuses the calm wind loop louder; ★ source a storm clip later.
- ★ Give the festival beat the fiddle cue.

### #76 Show me, echo scenes, world bubbles (phase 1)
- The first-person story lines (22 story kinds + 11 bank lines rephrased) are **new writing** — they need your voice
  check (`View/ShowMe/Core/PeopleVoice.cs`).
- ★ In a two-person echo, the second person joins only if within ~2 s' walk; otherwise the lead echoes alone.
- ★ A rivalry echo is a short argument then each sits out (no "turn backs" pose yet).
- ★ Bubbles: one every 20 s, 90 s per speaker, 6 s for a moment's line; never repeat within a session (not saved).
- Death echoes and box-wide entries (an era, a festival) are fly-to only.

### #78 Time-lapse replay (phase 2)
- Scrub cost on desktop: mean ~7–11 ms per step, but ~15 steps per 60-year sweep spike to 30–50 ms (4-chunk rebuilds at
  era changes). ★ Check on a phone (TestFlight); if it stutters, split a rebuild across frames.
- ★ Yards, the Keeper's shapings and people are absent in a replay (frame shapings are available to draw).
- Farm fields pop in once their farm is 60 % grown; home size/storeys use today's path tiers in every year.

### #85 Shaping looks + the people's answer (phase 1)
- ★ Rain, the herd's animal and the label follow the era when the tool was used; the grove look and the channel style
  follow the era now (they level up). Should a camp-era herd become goats when the village comes?
- ★ The answer moment: 40 s, up to 3 helpers and 2 kids, thanking the Keeper at the glass (the camp looks up instead of
  waving); the label stays 12 s. Answers older than 12 months while away are reported quietly; at most 3 queued.
- Sounds missing for placing a grove or a herd (#96 has no cue yet).

### #93 The box completes, box 2 arrives — look sign-off is yours
Screens: `AquariumCiv/Temp/agent/bc_*.png` (local).
- Box 2 still draws the river-valley terrain whatever its setting (oasis/mountain shows only on the shelf and label).
  ★ Per-setting terrain and palette as a follow-up issue.
- ★ The next box defaults to the desert oasis; no setting-choice UI yet (Choice 11).
- ★ Box 2 keeps living while you visit a museum (unlike the time-lapse, which holds the clock).
- Placeholders: museums on the shelf are setting-coloured slabs (not miniatures); the "last evening" gather isn't
  staged; lanterns have no floating names; the plate uses the built-in font; the last wipe is a caption.
- Not done: relic effects in box 2 (sim stub), naming the new settlement on its plate.
