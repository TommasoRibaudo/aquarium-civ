# Spike: Growth Lab, organic placement and paths from walking (2026-10-29, branch vs/growth-lab)

**Question** (research [08 §5](../../research/08-organic-settlement-growth.md#5-first-spike-growth-lab-23-agent-days)):
do scored frontage placement and wear-grown paths look like a real village growing, at our scale, and do the
Keeper's shapings read as answered?

> **Round 2 (issue #44):** the user picked random scatter over these rows in 3 of 3 blind sets, so a third variant,
> a **cluster village** (family clusters round shared yards, curved worn lanes, commons), was added and the blind
> test redone with three clips per set. See [Round 2](#round-2-2026-10-29-issue-44-branch-vs44-growth-cluster-the-cluster-village) at the end.

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

### Results so far (2026-10-29)
| Viewer | Clips: picked as "grew like a real village" | Era stills |
|---|---|---|
| The user (not fully blind: had seen two labelled organic-vs-random stills) | set 1 A, set 2 B, set 3 B = **the random control in all 3 sets** (0/3 for scored placement) | **9/9 correct** |

Criterion 7 (eras read) passes for this viewer. **Criterion 1 fails for this viewer:** the random scatter read as more
like a real village in every set. Next: ask what made the random one feel real, then look at the weights in the
overlay before changing the method (§5 rule); 2 more viewers still to come.

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

## Round 2 (2026-10-29, issue #44, branch vs/44-growth-cluster): the cluster village

**Why.** In the user's blind test the random-scatter control beat round 1's scored placement in 3 of 3 sets: *"the
non random do not have a lot of straight lines (since the map is very small, you end up with a couple of rows only and
it looks a bit wonky. i guess it looks overall unreal."* With about 30 buildings in a tabletop box, frontage rows give
two stiff, half-planned rows. Real hamlets of this size are cluster villages (Haufendorf). Round 2 adds that as a
third variant. **Organic (rows) and Random are unchanged**: their 120-year fingerprints are bit-identical to round 1's
code on `origin/vs/slice`.

![Seed 1, year 110: Organic, Cluster, Random](img/growth-lab-2/seed1-y110-organic-cluster-random.jpg)
![Seed 2, year 110: Organic, Cluster, Random](img/growth-lab-2/seed2-y110-organic-cluster-random.jpg)
![Seed 3, year 110: Organic, Cluster, Random](img/growth-lab-2/seed3-y110-organic-cluster-random.jpg)

### R2.1 What changed

| Part | Where | What |
|---|---|---|
| `Variant.Cluster` | `Logic/ClusterPlacer.cs` (new) | Huts and cottages live in **family clusters round a shared yard**. A new home either joins a family whose yard has room (candidates all round the yard's edge), or founds a new yard on rings round the green. A family builds 2 (40 %), 3 (45 %) or 4 (15 %) homes; in the Town era a cottage may add one more. |
| No lining up | same | **Yaw** comes from one of four rules, each with **±20° hashed jitter**: face the yard (preferred), face the low sun (the lab light, yaw −35°), face the green, or face downhill (only where the slope is over 0.12, so the long side follows the **contour**). **Sizes** vary (× 0.8–1.08 of the lot range, depth a little less), **setbacks** from the yard vary (0.02–0.09 u). Round 1's house rows are off for this variant (`Placer.homeRows = false`). |
| Keep gaps | same | A home keeps **0.26 u** from another family's buildings and 0.14 u from other lots; the yard itself is never built on; the land round a yard (yard radius + 0.3 u) waits for the family until it is full or 8 years pass without a new home. **Three commons** (radius 0.24–0.32 u, one near the village, two further out) are never built on; a Keeper's shaping opens commons within 0.7 u of it so the answer can land there. |
| Everything else | inner `Placer` | Fields stay at the edge, mills, workshops, market, tavern, church and shops keep round 1's siting rules (strip-field rows stay). When nothing fits, a **fine infill** (every free cell × 4 yaws, pruned by a clear-radius field) replaces round 1's coarse scan as the last resort. |
| Paths | unchanged wear model | Paths are **not laid between clusters**: each new home gets its short connection, and the paths between clusters wear in from walking as in round 1. |
| Curves | `Logic/PathCurves.cs` (new) | The tier grid is thinned to a one-cell skeleton (Zhang–Suen), traced into chains between junctions and dead ends, simplified (Douglas–Peucker, 0.06 u), corners cut twice (Chaikin), drawn through a centripetal **Catmull–Rom** spline, resampled every 0.03 u and relaxed six times. |
| Look (Cluster only) | `View/GrowthBox.cs`, `View/GrowthArt.cs` | Paths as ribbons draped on the ground (trail / path / cobbled road widths 0.08 / 0.12 / 0.17 u, soft verge, round caps; bridges as before). Each yard is trodden earth with a **barn, byre or open shed** across from the houses and a woodpile or crates at its edge. **Between clusters**: hedges, fruit trees, kitchen gardens and woodpiles, only on open ground. The commons are a flowering meadow with an old orchard. |
| Speed (all variants) | `Logic/PathNet.cs` | `Relax` takes a no-bounds-check path for interior cells, in the same order: results bit-identical, round 1's catch-up went from 234 to 199 ms. |
| Debug | `GrowthWorld.unplacedLog`, `.failed` | Which builds found no lot, with every candidate and its reject reason. The Cluster variant is in the `GrowthBox` inspector's `variant` drop-down; all cheats (rain, grove, channel, skip, era) work on it. |

### R2.2 The numbers (Editor, Mono; `[GrowthLab]` log lines of `AquariumCiv.Spikes.Growth.Tests`, 18/18 green)

| # | Check | Pass bar | Cluster | Organic (round 1, for reference) |
|---|---|---|---|---|
| 2 | Nudge answer within 1.0 u, 30 runs per kind | ≥ 80 % | rain **28/30 (93 %)**, mean 0.31 u; grove **25/30 (83 %)**, mean 0.65 u; channel **26/30 (87 %)**, mean 0.66 u | 97 / 97 / 87 % |
| 3 | Live = catch-up (576 months: one by one, `Advance(48 h)`, odd frames) | identical | identical fingerprints and families | identical |
| 4 | Sanity, 3 seeds × 120 years | 0 overlaps / cut off / in margin, ≥ 35 buildings | 0 / 0 / 0; **35, 36, 35 buildings**; commons and yards never built on; lots and yards never move | 38, 37, 37 |
| 5 | Emergent path; main street | a path ≥ 5 cells no step laid; roads > 0 | largest emergent run **16 / 13 / 6 cells**; roads 75 / 92 / 98 cells | 9 / 8 / 26; 71 / 84 / 79 |
| 6 | Cost | < 1 ms/month; catch-up < 300 ms | 0.121 + 0.189 = **0.31 ms/month**; catch-up **264 ms** | 0.20 ms; 199 ms |
| — | No rows: neighbouring homes parallel (yaw within 5°) / fronts in line | Cluster < 0.6 × Organic; in line ≤ 5 % | **14 / 30 / 23 %** parallel, **4 / 3 / 0 %** in line | 70 / 55 / 44 %; 18 / 18 / 22 % (Random 0 / 22 / 29 %; 0 %) |
| — | Curves | cover the path cells; no kinks | cover 141/141 path cells within 0.15 u; sharpest bend 21° between samples | — |

`AquariumCiv.Sim.Tests` 85/85 and `AquariumCiv.Prototypes.Shared.Tests` 47/47 still pass.

**Margins are thin, say so plainly:** building count 35 on two seeds (bar 35), grove 25/30 (bar 24), catch-up 264 ms
(bar 300; it moved between 259 and 289 ms across runs). The cluster village uses more land per home (yards, gaps,
commons), so 1–5 of the Town era's late builds find no lot (Organic: 1–2). Mono and the CoreCLR harness give
slightly different layouts (37/36/37 buildings there), as round 1 found. Wear costs more than in round 1 (0.19 vs
0.13 ms/month) because families sit further from the green and the flow fields run longer before every door settles.

Families: 10/16, 5/17 and 8/15 of the huts and cottages end in a family of 2 or more. SimCore builds only 7–9 huts
in 120 years, and most of the Town-era cottages find no room by a yard, so many families stay at 2; the barns and
sheds make each one read as a farmstead of 3–4 buildings.

### R2.3 The weights that changed (Cluster homes only; `ClusterPlacer` statics)

| Term | Weight | Shape |
|---|---|---|
| Join a family with room | **+1.4** (none while answering a shaping) | — |
| Yard distance from the green | 1.0 | 1 inside 0.9–1.9 u, −1 at the green's rim and 1.2 u beyond the band |
| New yard near the other yards | 0.6 | 1 up to 1.6 u, −1 at 2.4 u |
| Access (door to network) | **0.4** (round 1 huts: 1.2) | round 1's |
| Slope / water / woods | 0.8 / 0.3 / 0.6 | round 1's shapes, one pass over the lot |
| Contour (long side along the slope) | 0.9 | (2·\|front·downhill\| − 1) × min(1, slope/0.4), only on slopes over 0.12 |
| Yaw rule | 0.35 | yard 1, downhill 0.6, sun 0.2, green 0 |
| Taste (hashed per candidate) | 0.3 | ±1, so equal spots don't always resolve the same way |
| Nudge | 4.0 | round 1's; answer candidates sit on rings 0.1–0.55 u round the spot with their yard in front (a lone house if no yard fits) |
| Frontage, setback 0.12, rows, social pull | **off** | — |

Lot gap 0.08 and glass margin 0.25 as round 1. Yards 0.15–0.24 u radius, 1.0 u apart.

### R2.4 My look verdict (honest; the user is the judge)

![Seed 1: years 30, 60, 110 for the three variants](img/growth-lab-2/seed1-years-30-60-110.jpg)

*Does the cluster version look like a real tiny village from the side, more than random does?* **A little, not
decisively.**
- **Against round 1's rows it is clearly better.** The two stiff rows are gone: houses sit at all angles round small
  yards with a barn, fruit trees, hedges and gardens between the families, and the lanes curve. Nothing reads as
  planned. From the side the Cluster box at years 30–60 looks like a hamlet of farmsteads round a green.
- **Against Random the difference is small at the overview distance.** Both spread irregular houses over the box with
  greenery between. Cluster is more *nucleated* (a centre and its farmsteads, fields at the edge) and its paths are
  smooth lanes instead of 0.1 u stair-steps. Random scatters fields among the houses and its yards don't exist. In
  the middle years I would pick Cluster as the real village; at year 110 both are "a lot of houses" and I could not
  say which is real without looking closely.
- **Weak spots:** the families are often pairs (few huts, see R2.2), so the cluster idea shows best in the close view;
  the Town era still packs workshops and cottages round the market and the box is nearly full by year 110; Town-era
  cottages that find no family land wherever infill finds room.
- **A confound for the blind test:** only the Cluster variant gets curved path ribbons, yards, barns, hedges and the
  orchard commons (Organic and Random are drawn exactly as in round 1). A viewer who prefers Cluster may be preferring
  the dressing, not the placement. See R2.6.

### R2.5 The human test: how to run it

Everything is in `docs/design/spikes/img/growth-lab-2/`. The answers are in **`blind-key.b64.txt`**, base64-encoded so
a glance doesn't spoil them. Decode only after the test:
`python -c "import base64;print(base64.b64decode(open('docs/design/spikes/img/growth-lab-2/blind-key.b64.txt').read()).decode())"`.

- **Clips:** `blind/set{1,2,3}_{A,B,C}.mp4`, 960×540, years 0 → 120 at 8 years a second (15 s) with the last frame
  held 2 s, a small year counter bottom right (as round 1). Each set is one map and one build order; the three clips
  are the three variants (rows, cluster, random) in a shuffled order per set; the seed order is shuffled too.
- Same scripted Keeper in every clip of a set: rain at year 14, a grove at year 33, a channel at year 58.
- Ask per set, after all three clips: *"Which one grew most like a real village?"* (A, B or C), and optionally *"Which
  looks least real?"* and *"Why?"*. Don't say what differs.
- Pass for round 2: the user (and ideally 2 more viewers) picks the cluster clip on ≥ 2 of the 3 sets.
- Labelled stills for after the test: `stills/seed{1,2,3}-{organic,cluster,random}-y{030,060,110}.jpg`, the 3×3 sheets
  `seed{1,2,3}-years-30-60-110.jpg` and the side-by-sides `seed{1,2,3}-y110-organic-cluster-random.jpg`.

### R2.6 Open questions

1. **The dressing confound:** should the next round give all three variants the same path curves, yards-free dressing
   and commons, so the blind test isolates placement? (I kept Organic and Random exactly as they were, as asked.)
2. **More homes per family need more homes:** SimCore builds 7–9 huts in 120 years. Should a family's barn or byre
   become a real lot (a spike-side "outbuilding" kind), or should M2's economy build more, smaller homes?
3. **The full box:** with commons and gaps, 1–5 late builds find no lot. Is that the "box complete" moment research
   08 asked for, or should the Town era densify the clusters instead?
4. If Cluster wins, M2 would take `ClusterPlacer` + `PathCurves` (and keep round 1's field, mill and workshop rules).

### Round 2 blind results (2026-10-29)
| Viewer | Set 1 | Set 2 | Set 3 | Tally |
|---|---|---|---|---|
| The user | B = **Cluster** | A = Rows | A = **Cluster** | Cluster 2 · Rows 1 · Random 0 |

Cluster passes criterion 1 for this viewer (picked in 2 of 3 sets), and random scatter, which won all 3 sets in round 1,
was picked in none. Caveat: only Cluster had the curved paths, yards and commons dressing, so this measures the overall
look, not placement alone. Next: M2 (#32) takes `ClusterPlacer` + `PathCurves` for homes and keeps round 1's rules for
fields, mills and workshops; open points from the round-2 report (bigger families, full-box densify) go to #32.
