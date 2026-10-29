# Spike: Growth Lab, organic placement and paths from walking (2026-10-29, branch vs/growth-lab)

**Question** (research [08 §5](../../research/08-organic-settlement-growth.md#5-first-spike-growth-lab-23-agent-days)):
do scored frontage placement and wear-grown paths look like a real village growing, at our scale, and do the
Keeper's shapings read as answered?

Seed 1 = SimCore seed 7 on the style kit's own terrain; seed 2 = seed 11 on the mirrored terrain; seed 3 = seed 1
with the pond at the back.

**Short answer:** mostly yes. The measurable criteria (2–6) pass. The organic runs grow from a camp round a fire into
a nucleated village round a green, then into a dense town with a cobbled main street, rows of houses and fields out
at the edge. Criteria 1 and 7 need people; the clips and stills for them are ready (§6). My own verdict and the weak
spots are in §5.

![Seed 1: years 10, 50, 110](img/growth-lab/seed1-years-10-50-110.jpg)
![Seed 2: years 10, 50, 110](img/growth-lab/seed2-years-10-50-110.jpg)
![Seed 3: years 10, 50, 110](img/growth-lab/seed3-years-10-50-110.jpg)

## 1. What was built (throwaway, nothing in `Assets/Game` changed)

| Part | Where | Notes |
|---|---|---|
| Logic (pure C#, no UnityEngine) | `Assets/Spikes/Growth/Logic/` (asmdef `AquariumCiv.Spikes.Growth`, refs `AquariumCiv.Sim` only) | `GrowthTerrain` (StyleBox.Height copied; preset 0 is the style kit's box, 1 is mirrored, 2 has the pond at the back), `SiteMap` (60 × 44 grid of 0.1 u: height, slope, pond/canal, water distance, fertility + the Keeper's rain, forest, wear, tier, laid, occupancy), `PathNet`, `Placer`, `Encyclopedia`, `GrowthWorld` (the driver), `GrowthChecks` (criteria) |
| Driver | `GrowthWorld` | Runs the real `SimCore` (unchanged) and places whatever it adds; Town-era extension; eras; nudges; yearly snapshots; timers |
| Time-lapse | `Assets/Spikes/Growth/GrowthLab.unity`, `View/GrowthBox.cs`, `View/GrowthArt.cs` | The style kit's palette, shader, box, soil cut, shelf, wall, glass, AO bake and peg-doll people. Renders snapshot *N* (lots, buildings per era, fields, crofts, trails/paths/roads, canals with bridges, woods, stalls). Scrub or play 0→120 in the inspector |
| Debug overlay | `GrowthBox.overlay` | Heatmap of any score term or the total score for any building kind, wear, or tiers (laid vs **emergent** paths); candidate bars (height + colour = score, grey = invalid, gold cone = chosen) |
| Controls | `RandomPlacer`, `HandPlacer` | (a) random scatter with the same constraints, same build order, same paths; (b) today's hex slots centre-out with random 60° yaws (as `PrototypeSceneBuilder`) |
| Cheats | `GrowthBox` inspector (`GrowthBoxEditor`) | Pick Rain / Grove / Channel and click the Scene view; Skip 10 years; Force next era; Clear cheats. Cheats are stored with the year and the run replays deterministically |
| Tests | `Assets/Spikes/Growth/Tests/` (EditMode, 10 tests) | Criteria 2–6, geometry, terrain = StyleBox, SimCore unchanged, lots never move |
| Editor helpers | `Assets/Editor/Agent/GrowthLabBuilder.cs` | Builds the scene, named camera views, edit-mode JPG renders (URP post included), frame export |

### How it works (one sim month)
1. Script events (the scripted Keeper, the lab's cheats), then `SimCore.Step()`: the economy decides **what** (unchanged).
2. Every building SimCore added is placed (**where**): candidates are scored, the best valid one is claimed, and a
   connection path is laid from its door to the network at once (Emilien et al.), so nothing is ever cut off.
3. Town era: the spike-side extension adds workshops, cottages and shops (SimCore has no Town types; see §7).
4. Every third month, a **wear pass**: one Dijkstra flow field per destination group (green, fields, workplaces,
   market/tavern/church); every home walks to the green, to a field or a workplace, and to the market; wear decays by
   15 % a pass. Worn ground is cheaper to walk, so routes merge (Helbing). Tiers: trail ≥ 2.5, path ≥ 8, road ≥ 20
   (Town only), with hysteresis.
5. Every year, a snapshot (sites, tier grid, flags, wear, nudges, that year's placements with every candidate).

**Candidates** for a building: frontage along the network (both sides of every edge cell, at a 0.12 u setback, the
door facing the path; the path direction comes from the routes people actually walk), **rows** (beside a lot of the
same family, fronts in line: house rows, strip fields side by side), rays out from the green (fields, mills, the
church), backland behind homes (Town), the water's edge (watermill, dyer), and a ring round a Keeper's shaping (with a
smaller lot size for the answer). **Constraints** (invalid): within 0.25 u of the glass, on water/green/a path/another
lot (8 cm gap, exact rectangle test), too steep, too wet (or too dry for a watermill), door not reachable. **Score** =
Σ wᵢ·fᵢ with each fᵢ in [−1, 1]; best of all wins, ties by a seed hash; no dice from SimCore's streams. While a
shaping waits for its answer, other buildings keep 0.9 u clear of it (weight 4).

## 2. The numbers (Editor, Mono, this PC; logged by the tests with a `[GrowthLab]` tag)

| # | Check | Pass bar | Result | |
|---|---|---|---|---|
| 2 | Nudge answer within 1.0 u of the shaped spot, 30 seeded runs per kind (spot on open ground, year 8–63) | ≥ 80 % | rain **29/30 (97 %)**, mean 0.25 u; grove **29/30 (97 %)**, mean 0.31 u; channel **26/30 (87 %)**, mean 0.68 u | pass |
| 3 | Live = catch-up | identical | 576 months one by one = `Advance(48 h)` = 2 016 odd-sized frames: identical fingerprint (positions, yaw, sizes, tiers, canals, wear bits) | pass |
| 4 | Sanity, 3 seeds × 120 years | 0 overlaps, 0 cut off, 0 in margin, ≥ 35 buildings | 0 / 0 / 0 on all three; **38, 37, 37 buildings** at year 120 (1–2 of SimCore's/the extension's builds found no lot) | pass |
| 5 | Emergence | a path no connection step laid; a main street | emergent path cells (worn to path tier, never laid) 32 / 43 / 50; longest emergent run **9 / 8 / 26 cells** (0.9–2.6 u); main street 109 / 154 / 160 cells, roads 71 / 84 / 79 cells | pass (the "viewer points to it" half is for the blind test) |
| 6 | Cost | < 1 ms per month; 576-month catch-up < 300 ms | placement 0.09 + wear 0.18 = **0.27 ms per month** (SimCore itself 0.03); catch-up of 576 months from year 60: **245 ms** | pass (thin margin on the catch-up) |
| 1 | Blind time-lapse test | ≥ 2 of 3 viewers pick B on ≥ 2 seeds | materials ready (§6), **not run** | open |
| 7 | Era stills told apart | camp / village / town recognised | materials ready (§6), **not run** | open |

Other checks: lots never move across the 121 snapshots; the economy under the spike is the same economy run alone
(population, buildings, wood, chronicle); `AquariumCiv.Sim.Tests` 54/54 and `AquariumCiv.Prototypes.Shared.Tests`
47/47 still pass.

**Performance history (worth keeping for M2):** the first version cost 1.03 ms per month and 699 ms per 576-month
catch-up in the Editor. Mono ran the Dijkstras 3–4× slower than CoreCLR. Three changes fixed it: Dial's bucket
queue instead of a binary heap, stopping each flow field once every home's door is settled, and no allocations in the
rectangle test. Wear went from 0.91 to 0.18 ms per month with identical results.

## 3. Images

### Year 120: scored placement vs the random-scatter control
![Seed 1, year 120](img/growth-lab/seed1-y120-organic-vs-random.jpg)
![Seed 2, year 120](img/growth-lab/seed2-y120-organic-vs-random.jpg)
![Seed 3, year 120](img/growth-lab/seed3-y120-organic-vs-random.jpg)

### Control (b): today's hex slots
![Hex slots, years 50 and 120](img/growth-lab/control-hex-y50-y120.jpg)

### The overlay (the tuning tool)
![Tiers: trails yellow, paths orange, roads red, emergent paths magenta](img/growth-lab/overlay-tiers.jpg)
![Fertility term for a field after the Keeper's rain (red bump), with the candidate dots of the answering field](img/growth-lab/overlay-fertility-rain.jpg)
![Wear at year 90 (seed 2): the main street is the red spine](img/growth-lab/overlay-wear.jpg)

The total-score view (`Overlay.Score`) works the same way but is sparse for big lots like fields (most cells can't
take one), so the per-term views are the better tuning tool. Inspector: pick `overlay`, `overlayKind`,
`overlayTerm`, tick `showCandidates`, scrub `year`.

## 4. The weights that worked

Lots (w along the path × d back from it, in units): hut 0.38–0.44 × 0.42–0.50; cottage 0.34–0.38²; field
0.40–0.50 × 0.70–0.90 (strips); lumber mill 0.44–0.48 × 0.38–0.42; windmill 0.36²; market 0.54–0.58 × 0.40–0.44;
tavern 0.48–0.52 × 0.40–0.44; church 0.42–0.46 × 0.70–0.76; watermill 0.44–0.48 × 0.38–0.42; smithy 0.40–0.44 ×
0.38–0.42; dyer 0.42–0.46²; shop 0.36–0.40 × 0.34–0.38. Gap between lots 0.08; setback 0.12; glass margin 0.25.

| Kind | Green band (u) | Weights |
|---|---|---|
| Hut | 0.45–1.15 | Green 1.0, Access 1.2, Social 0.9, Slope 0.8, Water 0.3, Forest 0.6 |
| Cottage (Town backland) | 0.5–1.5 | Green 0.8, Access 0.8, Social 1.2, Slope 0.8, Forest 0.6 |
| Field | 1.2–2.6 | Edge 1.0, Fertility 1.6, Slope 1.2 (max slope 0.3), SameKind 0.8, Access 0.6, Water 0.3, Forest 0.8 |
| Lumber mill | 0.9–2.4 | Forest edge 1.6, Access 0.8, Quiet 0.6, Slope 0.5, Green 0.4 |
| Windmill | 1.0–2.6 | Height 1.5, Open (no trees) 0.8, near fields 0.6, Access 0.5, not tall in front 1.0, Green 0.5 |
| Market | 0.42–0.75 | Green 2.0, Busy 1.0, Access 1.0, Slope 0.4 |
| Tavern | 0.5–1.2 | Busy 1.0, near market 0.8, Green 0.8, Access 1.0, Slope 0.4 |
| Church | 0.5–1.5 | Height 1.2, Green 1.0, Access 0.8, not tall in front 1.0, Slope 0.4 |
| Watermill | 0.8–2.6 | must touch water; Water 1.5, Access 0.8, Green 0.3, Slope 0.4 |
| Smithy | 0.7–1.7 | near mill/workshops 0.9, Quiet 0.4, Access 1.0, Green 0.6, Slope 0.6 |
| Dyer | 1.0–2.4 | Water 1.3, Edge 0.6, near mill/workshops 0.7, Access 0.8, Slope 0.6 |
| Shop (Town, on the green's rim) | 0.2–0.5 | Green 1.5, Busy 1.0, near market 0.8, Access 0.6 |

Every kind: **Nudge 4.0** (f = 1 − d/0.6, clamped to −1). Others keep clear of a waiting shaping: −4 × (1 − d/0.9).
Paths: wear 1 per trip (home → green 1.0, → field or workplace 0.8, → market 0.5), decay 0.85 per season, walking
cost × (1 + 6·slope), worn ground up to −55 %, path tier −50 %, canal fords × 5 (bridges where a path is laid).

## 5. Verdict (my own look; the user is the judge)

**It looks like a village grew there, and it reads better than the controls.** What I see in the clips and stills:
- **Camp → village → town reads in kind**, and on the same lots: tents round a fire become thatched huts round a
  green with a well; huts on the busiest paths become half-timbered houses, then two-storey townhouses; forage patches
  become fenced strip fields; the granary becomes a windmill. The Town era adds a watermill on the canal, a smithy, a
  dyer's yard, shops on the edge of a cobbled market square, and a church that went up on high ground near the green.
- **The organic runs grow outwards from one centre**, with fields pushed to the flat land at the edge (by the pond)
  and rows of houses along a street (seed 2 at year 50 is a clean row village). The random control fills the whole box
  evenly from the start, mixing fields in among the houses; the hex control is a tight, regular blob with postage-stamp
  fields. In the time-lapse the difference is the *order* of growth, which is what a viewer sees.
- **The shapings are answered where the land changed**: a field by the rain, a building by the grove, a field or a
  watermill on the canal, a bridge where the main path crosses it.

**Where it's weak:**
- **By year 100–120 the box is full.** The organic town covers most of the land and reaches the woods on every side;
  the difference to the control shrinks at that point (both are "a lot of houses"). The middle years (30–90) show the
  pattern best.
- **The control isn't absurd.** Because it gets the same paths, art and eras, its long trails between scattered
  farmsteads look lived-in, and some viewers may like it. That is a fair test, but criterion 1 is not a foregone
  conclusion.
- **Village-era paths are faint** (§7); the clear streets come with the Town era's roads.
- Late buildings that fall back to the coarse scan (when the box is full) land a bit randomly.
- At the default zoom the camp is small; people and tents are hard to see in a 960×540 still.

## 6. The human tests: how to run them

Everything is in `docs/design/spikes/img/growth-lab/`. The answers are in **`era_blind-key.b64.txt`**, base64-encoded
so a glance doesn't spoil them. Decode it only after the tests:
`python -c "import base64;print(base64.b64decode(open('docs/design/spikes/img/growth-lab/era_blind-key.b64.txt').read()).decode())"`.

**Criterion 1, blind time-lapse (the user + 2 people who haven't seen the spike).**
- Clips: `blind/set1_A.mp4`, `set1_B.mp4`, `set2_…`, `set3_…` (960×540, 15 s each: years 0 → 120 at 8 years a
  second, a small year counter, the last frame held 2 s). Each set is one map and one build order. One clip uses the
  scored placement, the other the random-scatter control. The seed order and the A/B sides are shuffled.
- Same scripted Keeper in both clips of a set: rain at year 14, a grove at year 33, a channel at year 58.
- Ask per set, after both clips: *"Which one grew like a real village?"* (A or B), and optionally *"Why?"* and *"Point
  to the main street"* (criterion 5's human half). Don't say what differs.
- Pass: ≥ 2 of 3 viewers pick the scored clip on ≥ 2 of the 3 sets, and the user agrees.

**Criterion 7, era stills (someone who hasn't seen the spike).**
- Stills: `era-test/still_01.jpg` … `still_09.jpg`: years 10, 50 and 110 of the three seeds, shuffled.
- Ask for each: *"Camp, farming village, or crafts town?"* Pass: they sort them right (say ≥ 8 of 9).

## 7. Gaps in SimCore, and what M2 should take over or change

**Gaps found (SimCore unchanged; worked round inside the spike):**
1. **The 20-slot cap is a placement limit.** The spike's own `SimConfig` sets `buildingSlots = 96` (config, not code),
   because the cap otherwise stops the economy at 20 buildings. With placement in the sim, the cap should go.
2. **The economy plateaus at about 20 buildings and 30–40 people by year 120** (the quadratic building cost and the
   slow birth rate; wood often sits at its 2 000 cap). A crafts town needs 35–45. The spike adds a **Town-era
   extension** outside SimCore: one building every 18 months, at most 16 (smithy, cottage, dyer, cottage, shop, …),
   plus a watermill landmark when the Town era starts. M2 needs a Town-era demand inside the economy.
3. **No Town-era building types** (watermill, workshops, cottages, shops) in `BuildingType`.
4. **No era in SimState.** The spike's thresholds: Village at year ≥ 20 and pop ≥ 9 (at the latest year 32); Town at
   year ≥ 75 and pop ≥ 22 (at the latest year 92). In the game an era waits for the player.
5. **Nudge answers don't fit the shaping well:** a grove is answered by a lumber mill, then a market, then a *hut*; a
   channel by a windmill, then a farm. In the Town era the spike turns a channel's answer into a watermill if there
   is none. M2: choose the answer by era and by what the shaped land is good for.
6. **`Nudge` and `TrashItem` only have `slot`.** The spike keeps the spot in its own `NudgeRec`. M2 adds `x, z`.
7. The `Camp` building stays in SimCore's list after the camp era. The view retires it ("the fire ring was cleared
   for a green").

**What M2 should take over:**
- `SiteMap`, `PathNet`, `Placer` and `Encyclopedia` into `Assets/Game/Sim/Settle/` (research 08 §3.1), with the bucket
  queue and early-stopping flow fields, the per-cell **walked direction** (it gives houses their street-facing yaw),
  **row candidates** (they make the house rows and the strip fields that read as "a real village") and the
  **keep-clear term** while a shaping waits for its answer.
- **Store results, don't re-derive them.** The same code gave slightly different layouts under CoreCLR (harness) and
  Mono (Editor): float results differ across runtimes, as research 08 §2.5 expected. The same binary is bit-identical
  (criterion 3), so positions, tiers and laid cells belong in the save.
- Save cost: tier + flags are 2 × 2 640 bytes a year; for a 120-year time-lapse, store changes per year instead of full
  grids.

**What M2 should change:**
- **The box fills up.** By year 100–120 the town covers most of the land. M2 needs the "box complete" moment or a
  growth cap, and commons that stay open (some woods, a meadow, the green).
- **Late placements fall back to a coarse scan** (4–7 per run, once frontage and rows run out). They look the most
  random. Better: densify (backland, splitting lots) or stop building.
- **Village-era paths are faint.** Homes ring the green, so trips are short and few cells reach path tier before the
  Town era. It reads as a nucleated village, but the "paths emerge" story only shows clearly later. Options: a
  stronger trail look, or trips to the woods, water and fields that start in the camp era.
- **Phone cost:** 245 ms for a 576-month catch-up in the Editor is inside the budget with a thin margin. On a phone,
  run the wear pass every 6 months during catch-up, or cap catch-up placement work.
- **Rebuild cost:** the view rebuilds the whole box plus AO each year (about 1 s in the Editor). The game needs the
  chunked rebuild from research 08 §3.8.

## 8. Not done / left open

- Criteria 1 and 7 (people). Materials ready, §6.
- No tester-agent pass: this is a lab tool and a look study for the user, not a player-facing build (as for the style
  kit spike). The scene-view click cheats were tested through the same `AddCheat` call they use, not by a real click.
- No phone run. No chunked rebuild. No chronicle lines naming the place ("beside the new channel").
- A rain or grove cheat clicked on water isn't moved to land (the grove plants nothing and the answer lands
  anywhere); the scripted Keeper and the criterion-2 runs use open ground. A forced era starts the following year.
- The hex control shrinks lots to fit a slot and doesn't check overlaps (that is today's behaviour).
- Tuning stopped once the measurable criteria passed; the weights were tuned on the three time-lapse seeds only.
