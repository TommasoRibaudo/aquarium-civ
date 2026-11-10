# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-11-09._

## Playtest-feedback batch 3 (2026-11-09)
Shots in `design/results/p9/feedback3/` (`5xx_*` per issue, `f3_*` the touch play-through, `tester_*`/`tfix_*` the tester run and its fixes).
Run log: `overnight/FEEDBACK-3-STATUS.md`; pass results: `overnight/feedback3-pass-results.md`.

### Sign-offs on the phone (built, merged, issue open)
- **#516 placing life**: ghosts keep the species' colours as pale silhouettes (no area ring on the thicket); the hint names the group ("Tap again to set the bushes down here."); the glowing ground was made stronger after the tester found it faint; with a tool in hand the Kit button reads "✕ Put away" (clean mode keeps "Done"). The camera lean now keeps the ghost clear of the caption.
- **#518 seed**: first tap shows a ghost, second plants. Two-finger pans never mist, plant or wipe (the tools wait 0.1 s for a second finger; the cloth replays the path so nothing is lost). The seed ghost is small at the default zoom (~22 pt): ★ accept, or make it bigger. **Question:** should the seed also go into the life jar?
- **#520 barren wind**: a listen. The 40 s loop was the repeat; now two voices at different pitches, wandering level and pitch, softer fade.
- **#524 wet at the glass**: misting at the glass darkens the edge and a strip down the soil face (`524_*`, `f3_g_*`). Subtle at the default zoom: ★ accept, or stronger.
- **#525 tree at night**: a faint warm pool under the grown tree, far weaker than the fire's (`f3_i_*`). ★ accept, or stronger.
- **#526 poles by the tents**: the lanterns (Village on) got a shorter post and a bigger, brighter capped head. The tall dark poles in your shot are most likely the festival bunting posts, which I didn't touch: say if those are the ones you meant.

### Decisions needed
- **Greening pace**: in the pass's touch play-through, holding the mister on 5 spots every season, the box was still 62 % bare at year 15.8 with the camp gate shut; the seed wasn't unlocked by year 17 and no pond had formed by year 19. The play-through used state cheats for those beats. ★ Look at a real early game on the phone first; if it feels this slow, open a tuning issue.
- **Lanterns at tent doors** (#526): lanterns only exist from the Village era. Camp tents have none. ★ Leave as is.

### Calls I made without you
- #519 history lines when a pond dries: ducks: "The ducks had no water left to swim on. One morning they were gone, and only a few feathers lay in the dry hollow." Fish: "The water was gone from the hollow, and the fish with it. The children looked for them in the mud." Say if you want other words.
- #519 root cause: the old random duck story fired on living-land boxes; it now only runs on old (non-ecology) boxes.
- #521: at most 2 diggers at 3 patches on the first camp days; the cook stays at the fire; lunch in turns.
- #523: weeds and vines never grow on the fire's ring or settled ground; vines that reach it are pulled.
- While a life or seed tool is in hand, people's caption lines wait (they play after).

### Polish noted, not fixed
- The cloth's first stroke starts ~0.1 s after the finger lands (the second-finger wait); the path is replayed, so nothing is lost.
- After one full edge-to-edge wipe the glass looks clean, but the worst pane can still read just above the "wiped" mark, so the first-run step may need a second pass.
- Already failing on dev (not this batch): MistDuskPlayTests at 20 h and 21 h (spraying lightens the land at dusk by ~1.2 vs the 1.08 limit).

## Playtest-feedback batch 2 (2026-11-04/05)
Shots in `design/results/p8/feedback2/`. Run log: `overnight/FEEDBACK-2-STATUS.md`.

### Sign-offs on the phone (built, merged, issue open)
- **#331 sand gusts**: a listen. The constant hiss is gone; 2–6 s gusts every 8–25 s, each a bit different (cut, pitch, pan, level), lower than before, silent below 40 % bare. The four cuts come from the same recording as the old loop. The barren wind loop (40 s, random start) is unchanged: say if you still hear a repeat.
- **#332 camp**: the logs round the fire are gone; the rack is a compact A-frame with up to two laced pelts, the rest in a folded pile (`332_*.jpg`). At the default camera it's small, like everything in the camp. ★ Accept, or make the rack bigger / the pelts larger.
- **#329 Skip step / Skip all**: hairline pills, faint fill, 44 pt (`329_*.jpg`).
- **#281 fresh green**: toned down to a faint pale hint (noon light vs the land 1.034 → 1.012), fades in 100 s (was 180) (`281_newgrass_after_noon.jpg`). ★ Accept, or fainter still.

### Decisions needed
- **#327 people's words**: three options with mock-ups (`327_today.jpg`, `327_option_A/B/C.jpg`, design note `design/people-talk.md`). ★ **A**: at the default zoom lines go to the top caption with the speaker's name and a ring under them; zoomed in (doll ≥ 45 px, about zoom 0.62) over the head. Also: should a line have its own short voice; crowd lines as "someone, by the fire" or always a named person; drop the silent far-zoom group marks?
- **#330 tweezers in a forest**: options in `design/keepers-hands.md` §5.7. ★ **B now** (pluck saplings only on paths and yards, ~2–3 h), **then C1** (1–3 bonsai shoots on the Keeper's tree, ~10–14 h); decide C2 (forest trees) after playing C1. And: are a leaf print on the pane, a lopsided crown and a little shade enough for "if you don't prune"?

### Calls I made without you
- The tapped rabbit freezes 0.3 s, then runs (2.2 units/s); the tap spot and its old spot are avoided for 180 s of field time; rabbits within 0.75 follow. The knock or ripple still plays with the startle. A tap right on a rabbit wins over a piece's card (a rabbit by the fire opened "The fire").
- The hunted rabbit now ignores everyone until the strike (it used to ignore only the hunter and the kid): with rabbits spread over the meadow, a passer-by sent it off mid-stalk.
- The recall question waits for 2 s of calm with nothing up; a missed day just tries again next session.
- The rack sits 0.5 from the fire (was 0.34); pelts lean 12° (40° went edge-on at the camera's pitch).

### Polish noted, not fixed
- Rabbits cluster when the meadow is one small patch (year 8 in the tester's run, meadow ~2 %): they only live on green.
- A small log stack beside a sitter at the fire (likely the firewood stash) still reads a little like a seat.
- "Not now" on the recall sheet is bare text about 35 px tall: under 44 pt.
- The camp, rack included, is a few pixels at the default camera.

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
- **#249 the kit button**: built after build 15 as a sheet with three tools, not the fanned rail of keepers-hands §3.3; the stick isn't in it; the tweezers are (#311).

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
