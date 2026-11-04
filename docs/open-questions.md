# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-11-04._

## P8 polish run (2026-11-04)
Details: [design/results/P8.md](design/results/P8.md) "The polish run"; shots in `design/results/p8/polish/`.

### Sign-offs on the phone (built, merged, issue open)
- **#266 misting** (+#263, #265): spray, recorded "fsss" (debug "Mist sound A/B" flips to the code-made one), wet area, a little green at once. ★ Judge the puddles: they still read as grey-blue stones with a dark halo.
- **#269 / #270 / #297 the pond and the soil cut**: the pond meets the glass; damp earth with no line. A faint stair-step where the green cover ends (the 10 cm grid).
- **#279 the pond's first water** and **#286 frogs and ducks**: frogs are small at default zoom; ducks need ≥ 20 water cells (a natural first pond had 11–25).
- **#281 fresh green on return**: subtle by day with few new cells (`_FreshLift` 0.7 is the knob); grass tufts keep their colour.
- **#282 rabbits** (size 2.2×, tracks subtle), **#283 the hunt** (a few pixels at default zoom), **#284 hides and the first tent** (dark if the gate opens at night), **#288 berries** (cheat bushes sit by the fire; natural ones come in the meadow).
- **#280 sound**: a listen. The recorded spray is brighter (7.9 kHz centroid); birds are faint against the wind on a half-green day; no recorded dirt-crackle exists in CC0, so the barren bed has none.
- **#273 camera**: close zoom reaches each wall; far zoom keeps the box on screen; the front/back reach at close zoom is small on a tall phone.
- **#274 bridges**, **#276 ritual symbols**, **#208 dawn lane**, **#210/#211 crumbs**: look sign-offs. #210: ★ I read it as "add falling crumbs" and kept the hut-sized Sky Crumb lump; say if the lump itself should become crumbs.
- **#249 the kit button**: built after build 15 as a sheet with three tools, not the fanned rail of keepers-hands §3.3; snips and the stick aren't in it.

### Decisions needed
- **#292 kit wishes**: a living-land box has no wishes; what is the first wish, if any?
- **Pacing of the pond.** With misting every season or year, the pond's first water came at about year 17–25 (and not by year 23 in one run). The lab's figure is about year 20 with care. ★ Keep (it's the long goal), or speed up the water table so a player sees the pond in the first weeks of real time.
- **Camp timing with care** is about year 3.3–3.8 after the retune (lab 4.3). ★ Keep; `campMeadow` 0.20 → 0.22 brings it nearer 4.3.
- **#246 leftovers**: the glass voice's first line on the first mist ("the sky wept") isn't built; birds on the first bushes are a small grey bird. ★ Close #246 and track the line in a small issue.

### Calls I made without you
- Dropped the fire move to the first green, as you asked (keepers-hands §2/§2.1 struck).
- The mist greens ≥ 3 cells at once every time; `grassRate` 0.5 → 0.1, `campMeadow` 0.15 → 0.20 to hold the gates.
- Tents cost 2 hides on a living land (first tent soon after the gate).
- The hunt: 62 % catch chance, the herd never hunted below 2 rabbits.
- Fresh green fades over 180 s.
- The recorded mister spray is the default, the code-made one is kept as a fallback.
- The camera limit is on the view, not the pivot (far zoom: almost no pan).
- Play tests turn the living land off around every test; the Editor's own switch is now off.

### Polish noted, not fixed
- The name step's touch ring covers the plate's words; at 1080×1920 "Skip all" sits on the box rim.
- Old caption lines linger across an away (the return can open on a year-old line); a founder's caption covers the box during naming.
- People and rabbits are a few pixels tall at the default zoom.
- History's timeline says "Year 1 · made camp" while the camp gate opens later; Year 0 on the HUD vs "chief since year 1".
- The mister's instant green still uses the old tint mode (could glow if misted at night, for a few seconds).
- On day 0 a dawn water trip can drop its full-bucket walk home.
- A tie label can sit on another person in a crowded village card.

## P7 Keeper's dilemmas (2026-11-02)
Details and screenshots: [design/results/P7.md](design/results/P7.md) ("Dilemmas", "Agent playtest", "Polish").
- **#222 text review.** The 8 dilemmas after your first review (1, 5, 6, 8 rewritten; 4 patched; the people never say
  tank). ★ Read them in P7.md "Dilemmas" and mark what still reads wrong. Check: `village_harvest`'s offering hint says
  "left on the hill for the Keeper" (a hill inside the box).
- **#224 look sign-off** (and #231's camps). ★ Look at the choice-scene and camp shots. The points to judge: do the halos
  and tags read as a moment or a menu; the question caption sits over the box the whole wait; the which-way framing; the
  chief half hidden by the fire props; the tradition offerings small among the baskets ("the stone" reads as firewood);
  the woods camp lost in the trees and the shore hut reading as part of the village at default zoom.
- **Survey Q4/Q6.** No in-game survey form exists. ★ Paper/online form for the lab; build one in-game later if P7 goes on.
- **Small polish** (★ one ticket after the lab): the bar truncates "The Keeper chose the…"; the same placeholder icon on
  both options; page dots overflow at 17 cards on a small phone; no speaker on trait-worded questions on the card; the
  taller callback sheet covers the bottom of the box on a small phone; a toast slides under the choice-scene bar.
- **Go / reshape / drop** after the lab and the 3-day diary, against P7.md "Pass".

## Still open (2026-10-30)

### Camera lifts after the glass fix
- Show me, relic nudges and answer flights still rise to pitch 50 for a haze that turned out to be an Editor bug.
  ★ Keep them, they frame well. Alternative: go back to the player's pitch.

### Dirty glass look
- Olive smears and drips now. ★ Keep (you said it's fine for now; variety is #121). Alternative: heavier or mistier.

## From tester run 4, first play through clear glass (2026-10-31)
None of these block the build. ★ = file it and do it after the PR to dev.
- **Trash and weeds:** trash reads as boulders and buries the tents; weeds read as a pretty bush. ★ Rework both.
- **Citizens at default zoom:** about 30x41 px (2.5 mm), coloured pegs. ★ Scale up a little and test.
- **Relics:** Sky Crumb looks like a tent; Keeper Doll and Lens don't stand out; the Sandal had no prop. ★ Shared cue, fix the Sandal.
- **Camera after a fly-in:** never returns, so normal play opens cropped on the camp. ★ Glide back after a few seconds.
- **Wish Give then Cancel:** the card and dot vanish until the next return. ★ Bring the card back.
- **Skip all and History:** the dot stays and History reopens the stack, so a skipper never reaches the time-lapse. ★ Skip all clears the dot.
- **Two years on the time-lapse screen:** HUD 70, panel 71. ★ Hide the HUD year there.
- **Small UI tidy:** targets under 44 pt, pin numbers under the playhead, tray toast behind the sheet, faint grey text, tray margin, tablet hint overlap, stacked labels at the end of the first run, lone word in a bubble, snapshot toast over the town, "Next 1/7". ★ One ticket.
- **Tall sheets:** the tray is 30 % since #167; time-lapse caption about 45%. ★ Leave for the UI rework (#108).
