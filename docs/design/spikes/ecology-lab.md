# Spike: Ecology Lab, a barren box that grows (#228, branch p8/ecology-lab)

**Question** ([keepers-hands.md](../keepers-hands.md) §2, [research 15](../../research/15-barren-to-living-box.md) §9):
does the Keeper's tending (mist, wipe, plant the one seed, snip) turn a barren box into a living one, in the arc's
order and at a pace that fits a camp era of a few real days? Do the levers matter, and does absence still progress?

## Plan (written before the code)
- **Where:** `Tools/EcologyLab/`, a .NET 8 console runner plus its own NUnit tests. It compiles the game's
  `Sim/Settle/Terrain.cs` read-only (with a one-line `BoxSetting` shim), so the land is the real river-valley land
  (presets 0, 1, 2: stylebox, mirror, backpond) on the real 60 × 44 site grid. Nothing in `Assets/` changes.
- **Step:** one game season. Everything random is a hash of seed + cell + season, so a run is a pure function of
  (terrain, strategy).
- **The model:** cell layers (soil, moisture, cover), one box humidity, a glass with grime and droplets, one box-wide
  water table, basins from a priority flood (their spill level = the people's flood line), the one tree (5 stages,
  water + light + weeds + seasons, the people learning), the forest spread by birds, rabbits and birds as counts,
  natural dew and rare showers, the camp gate and the village gate.
- **Time:** 1 real hour = 1 game year, so one season is 15 real minutes. The scripted players visit on a real-day
  clock: awake 16 h, asleep 8 h. (c) and (d) check in every hour they're awake, (b) every 4 hours, (a) never.
  The nights are the absences.
- **Players:** (a) does nothing; (b) mists now and then, plants, never weeds or wipes; (c) mists steadily, plants,
  full tree care (mist, wipe, snip); (d) like (c) but over-mists (3× the misting).
- **Output:** a frame every 2 game years (cover per cell, water, the tree, the camp, a cross-section strip with the
  water table), a contact sheet per run, CSV curves, and the key years. Runs go on to year 100 for the key years only,
  so a late gate still gets a year.
- **Tune** by eye until the arc reads in order, then tests, then this write-up.

---

**Short answer: yes, it reads.** Every run goes from bare dirt to green, then meadow and rabbits, then the camp,
bushes, the one tree, a wood spreading from it, the water rising into the hollow, and the village, in that order.
The levers matter. Full care takes the land from camp to village in about a real day; a casual keeper takes about
2½ days; an absent one gets there anyway, in 3–4 days. Over-misting brings the water first and fogs the glass.
The weak spots are in §6.

![Seed 1: the four keepers](img/ecology-lab/compare-seed1.png)

## 1. What was built (throwaway, nothing in `Assets/` changed)

| Part | Where | Notes |
|---|---|---|
| Land | `Tools/EcologyLab/Lab/Land.cs` | The game's own `Terrain.Height` (linked read-only) on the 60 × 44 site grid; presets 0 stylebox, 1 mirror, 2 backpond. **Basins by priority flood** from the box's edge: each closed hollow, its cells and its spill level (= the flood line). The main hollow holds 253 / 283 / 277 cells; its floor is 0.77–0.80, its spill 1.01–1.03 (the old static pond, `h < 0.92`, was 125–175 cells) |
| Model | `Lab/Eco.cs`, numbers in `Lab/Tuning.cs` | Cell layers soil, moisture, cover; box humidity; glass grime + droplets; one water table; the one tree; the forest; rabbits and birds as counts; gates. One step = one game season, every chance a hash of (seed, cell, season) |
| Players | `Lab/Player.cs` | On a real-day clock (§2) |
| Output | `Lab/Render.cs`, `Lab/Png.cs`, `Lab/Run.cs` | Frames, contact sheets, the four-keeper comparison per seed, CSV curves, `summary.csv`. A 3 × 5 pixel font and a PNG writer on `ZLibStream` |
| Tests | `Tools/EcologyLab/Tests/` (20 tests) | §7 |

Run it: `dotnet run -c Release` in `Tools/EcologyLab` (all 12 runs, frames included, in about 4 s), and
`dotnet test Tools/EcologyLab/Tests` (about 10 s).

## 2. The model in plain words

**Time.** 1 real hour = 1 game year, so a season is 15 real minutes. The run starts at 08:00 on day 1. The scripted
player is awake 08:00–24:00 and asleep for 8 hours (8 game years), so every night is an absence.
- (a) **does nothing**: never visits.
- (b) **mists now and then**: visits every 4 waking hours. It mists 2 spots (the tree first, once planted) and plants
  the seed. It never wipes or snips.
- (c) **full care**: visits every waking hour. It mists the tree and 3 spots, plants, wipes the glass and snips the
  weeds round the tree.
- (d) **over-mists**: like (c), with 3× the misting (6 spots at 1.5×).
- The first session is two seasons (half an hour). Players mist "here and there": the driest of a few spots round
  the camp, reaching further out as they go.

**Each season:**
1. **The Keeper's hands.** The mister wets a 0.45 u spot (its core greens at once), raises the box's humidity and
   soaks some water toward the table. Wiping clears grime and droplets. Snipping clears the tree's weeds. The seed
   is planted on open, flat ground 0.8–1 u from the camp, above the flood line.
2. **Weather the people remember.** Dew every season (more when the air is humid). A passing shower about one
   season in seven, on a random 0.9 u patch.
3. **Moisture.** Water runs downhill (the lowlands collect it). Above field capacity, part soaks down to the water
   table: at most 12 units a season (the soil takes only so much; past that, misting only fogs the air). Then it
   dries by season (summer most) and by humidity; plants hold water.
4. **The water table** is one level for the whole box. It starts 0.13 below the lowest ground and rises with what
   soaks down. A bigger pond needs more water per unit of rise. It falls a little in summer, never below its peak
   − 0.015 (pillar 4). Where it meets the ground inside a hollow, that ground is water. **The pond never shrinks**,
   and it can't rise past the hollow's spill line. Near the table, the soil stays damp from below.
5. **Air and glass.** Humidity drifts toward a box value: 0.2 bare, more with plants and the pond, lost to the room
   through the open top. Above 0.55, droplets bead on the glass; they come back about an hour after a wipe if the
   air stays humid. Grime builds slowly. **Light** = 1 − ½ grime − ½ droplets.
6. **Cover.** Bare → grass needs moisture, and comes faster next to green. Grass → tall grass needs moisture and a
   little soil; grazing holds it back. Grass or tall grass → bush needs soil ≥ 0.28 and comes faster next to
   bushes; rabbits hold it back. Plants build soil (grass slowly, trees fastest). Reeds grow at the water's edge.
   The camp's yard stays short grass.
7. **The one tree.** Its stages are seed, sprout, sapling, young and grown (progress 0.5 / 1.5 / 3 / 4.5).
   Progress per season = rate × season (spring 1.5, summer 1, autumn 0.55, winter 0) × water at its cell × light ×
   (1 − 0.6 × weeds). Weeds creep in from the meadow round its foot. After 3 of the Keeper's mistings, **the people
   carry water** to it every season; after 6, they ring it with sticks, which halves the weeds. It wilts and pauses,
   and never dies.
8. **Birds and the forest.** Once a tree is grown, birds (their count follows bushes and trees) carry seeds in
   spring and autumn. Most land near a tree and a few far (r = 8 cells × u^1.8). Seeds take best in bushes (nurse
   shrubs), then in grass, and never in the flood line or the yard. Wild seedlings need no care: seedling → young →
   grown takes about 14 years.
9. **Absence's own tree.** Once the camp has started, a passing bird may drop one seed a year (8 % chance).
10. **Rabbits** arrive at 10 % meadow and grow toward meadow ÷ 35.
11. **The gates.**
    - **Camp:** meadow ≥ 15 % and rabbits ≥ 2.
    - **Seed:** the first bushes (0.3 % of the land).
    - **Village:** the camp, a pond of ≥ 20 cells, and ≥ 8 grown trees.

**Nothing dies back:** plant cover only moves forward (bare → grass → tall → bush → seedling → young → tree, or
into reeds and water in a hollow). The pond never shrinks, and the tree never loses a stage. The tests check this
every season.

## 3. The contact sheets

The four keepers per seed (years 0, 4, 8, 12, 20, 30, 40):

![Seed 2: the four keepers](img/ecology-lab/compare-seed2.png)
![Seed 3: the four keepers](img/ecology-lab/compare-seed3.png)

Every frame of every run (a frame each 2 years), seed 1:

![Seed 1 (a) does nothing](img/ecology-lab/sheet-seed1-a.png)
![Seed 1 (b) mists now and then](img/ecology-lab/sheet-seed1-b.png)
![Seed 1 (c) full care](img/ecology-lab/sheet-seed1-c.png)
![Seed 1 (d) over-mists](img/ecology-lab/sheet-seed1-d.png)

The other seeds' sheets are `img/ecology-lab/sheet-seed{2,3}-{a,b,c,d}.png`. The single frames are in
`img/ecology-lab/frames/`.

**How to read a frame:**
- The map is the land seen from above, back at the top.
- Dirt is tan, darker when damp, with cracks when dry. Grass is light green, tall grass mid green, bushes olive,
  tree seedlings a dark tick, young trees and grown trees dark green squares.
- Water is blue, reeds are striped. The flood line is blue dots.
- The camp is a fire (grey and cold before the camp, orange after) with tents. The Keeper's tree is a yellow ring
  (orange while it wilts). Rabbits are white dots.
- Over the map: grime as a grey haze, droplets as beads.
- The strip under the map is a cross-section through the hollow's lowest row. The water table is the blue band
  rising through the soil; the dashed line is the spill (flood) level. Hills outside the hollow are clipped.
- The bar at the bottom shows humidity (tan = too dry, green = right, blue = too humid) and the counts.

## 4. The curves, summarised

Key game years (= real hours from 08:00 on day 1; 24 = 08:00 on day 2). Runs go on to year 100 for these; frames
stop at 40. Full curves are in `img/ecology-lab/curves/`, and every column is in `summary.csv`.

| Seed | Keeper | Camp | Bushes | Planted | People water it | Tree grown | Water surfaces | **Village** | Trees / pond at Y40 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | (a) nothing | 25.3 | 37.3 | — | — | (wild 69.3) | 80.8 | **81.8** | 0 / 0 |
| 1 | (b) casual | 11.3 | 19.3 | 24.0 | 32.0 | 36.3 | 59.8 | **60.8** | 2 / 0 |
| 1 | (c) care | 4.3 | 8.5 | 9.0 | 11.0 | 13.5 | 20.0 | **25.3** | 50 / 120 |
| 1 | (d) over-mist | 1.3 | 5.3 | 6.0 | 7.0 | 11.3 | 11.3 | **20.5** | 105 / 134 |
| 2 | (a) | 30.0 | 37.3 | — | — | (wild 84.8) | 73.5 | **91.3** | 0 / 0 |
| 2 | (b) | 16.3 | 18.3 | 24.0 | 32.0 | 36.3 | 56.0 | **56.8** | 1 / 0 |
| 2 | (c) | 3.3 | 8.3 | 9.0 | 11.0 | 13.5 | 22.3 | **30.3** | 37 / 115 |
| 2 | (d) | 1.3 | 6.3 | 7.0 | 9.0 | 12.3 | 11.3 | **26.5** | 65 / 130 |
| 3 | (a) | 18.3 | 28.5 | — | — | (wild 47.3) | 66.3 | **68.3** | 0 / 0 |
| 3 | (b) | 9.5 | 14.3 | 24.0 | 32.0 | 36.3 | 49.3 | **51.3** | 1 / 0 |
| 3 | (c) | 3.8 | 7.8 | 8.0 | 10.0 | 12.5 | 18.8 | **24.5** | 62 / 110 |
| 3 | (d) | 1.3 | 5.3 | 6.0 | 8.0 | 11.3 | 11.3 | **22.5** | 96 / 125 |

- **Seed planted → grown:** (c) about 4.5 years; (b) 12.3 years (2.7×); (a) never plants, and a bird's seed grows
  wild 30–70 years in.
- **Too-humid seasons by Y40:** (d) 22–25 of 160; (a), (b) and (c) none.
- **Meadow at Y40:** (c) 66–70 %, with bushes 19–20 %. (d) has more scrub: 29–31 % bushes.

## 5. Answers

1. **Does barren → living read right over time?** Yes. The frames tell the beats without labels: dust, a green
   patch where the mist fell, a spreading meadow with rabbits, the tents, scrub, a ring round one tree, a dark wood
   thickening round it, blue rising in the strip and then filling the hollow. The lowlands green first, since water
   runs there, which reads as natural. The order is camp, bushes, seed, tree, water, village on all three seeds with
   care.
2. **Do the levers matter?** Yes, clearly.
   - Care moves the camp from Y18–30 (a) to Y3–4 (c), and the village from Y68–91 to Y24–30.
   - The tree grows 2.7× faster with care.
   - Over-misting brings the water about 9 years sooner, but it fogs the glass for 22–25 seasons. It only gains
     the village 2–5 years, because the trees still need their time.
   - The one lever that's nearly flat is **light**. Grime is slow and droplets come only from over-misting, so
     wiping barely changes the tree for (c). Watering and weeding do the work (§6).
3. **Does absence still progress?** Yes, slowly. (a) greens from the lowlands and from showers, and its camp starts
   at Y18–30. A passing bird drops its first tree, the water reaches the surface from dew and showers alone at
   Y66–81, and the village comes at Y68–91. Nothing ever dies back.
4. **Are the times right for a camp era of a few real days?** I think so.
   - **(c) full care:** camp in the first 3–4 hours, the tree grown in the first afternoon, the water on the first
     evening, and the village on the morning of day 2.
   - **(b) casual (4 check-ins a day):** camp in the first half-day; the village on day 3.
   - **(a) absent:** the village on day 3–4.
   - That's "a few real days" for most players. An hourly player gets there in one. If that's too fast, raise the
     village's tree count (8 → 12) rather than slowing the camp.

## 6. Tuned constants and what surprised me

**Tuning rounds** (also in `docs/overnight/ECOLOGY-STATUS.md`):
1. **v1.** The arc read at once, but the water surfaced too early: (c) at Y10, before the tree was planted, and
   (d) at Y3. The forest was a speckle over the whole box, and (a) never got a tree.
2. **Less straight-down soak from the mister** (0.6 → 0.15), lower bush soil, fewer seeds per bird. The water
   hardly moved, so the soak from the mister wasn't the cause.
3. **Found a runaway loop.** As the table nears the surface it wets the lowlands above field capacity, and that
   water soaks straight back down. Fix: soil wet from below stays just under field capacity. Then: a cap on the
   soak per season (the design's "moisture saturates, then misting only fogs"), and a bigger pond needs more water
   to rise.
4. **Natural help made real.** Showers 0.09 → 0.14 a season, and a passing bird can drop a seed once the camp
   exists. The village needs a real pond (≥ 20 cells) and a small wood (≥ 8 trees). A bug made the bird's seed
   never land (it was rolled at 1.0); fixed.
5. **Wild trees slowed** (seedling 0.10 → 0.06, young 0.07 → 0.045), so the Keeper's neglected tree isn't beaten by
   a wild one.
6. **Seeds land near trees** (r ∝ u^1.8), so the wood spreads from the one tree instead of speckling. Fewer bushes,
   and grazing holds them back harder (40 % scrub → 20 %). The pond keeps growing with care (less summer loss).
7. **The seed unlocks at the first bushes, and the table starts deeper** (0.10 → 0.13 below the floor), so with
   care the water comes after the tree.

**The constants that matter most** (all in `Tuning.cs`):

| Constant | Value |
|---|---|
| `soakCapPerSeason` | 12 |
| `tablePerSoak` | 0.0003 |
| `tableStartBelow` | 0.13 |
| `tablePondDamp` | 50 |
| `tableSummerLoss` | 0.002 |
| `treeRate` | 0.35 |
| tree stage thresholds | 0.5 / 1.5 / 3 / 4.5 |
| `learnWater` | 3 |
| `peopleWater` | 0.18 |
| `seedlingRate` / `youngRate` | 0.06 / 0.045 |
| `seedFalloff` | 1.8 |
| `showerChance` | 0.14 |
| `bushSoil` | 0.28 |
| `grazeBush` | 0.7 |
| village gate | pond ≥ 20 cells, trees ≥ 8 |

**What surprised me:**
- **The people's watering decides the tree's pace, more than the Keeper's care.**
  - An hourly player mists the tree only once a game year, so after the first 3 mistings, the child with the water
    skin keeps it alive.
  - (b)'s tree is grown at Y36.3 on all three seeds: its pace is set by when (b) visits (and so when the people
    learn), not by the land.
  - That's good for the story (*care moves up*), but it means the water need mostly disappears after the first
    hours of play.
- **Light is a weak lever.** Grime rises slowly, and droplets only come from over-misting. So "wipe the glass above
  the tree" hardly changes anything for a careful player. Either make the tree's light need stronger (it leans and
  stalls under a little grime), or let the wipe matter through humidity instead.
- **The water table is a feedback system.** Damp lowlands soaking back into the table ran away until I broke the
  loop. The real model needs the same care: water wet from below must never count as recharge.
- **Over-misting isn't punished, only less efficient.** (d) gets water first and the most trees, at the price of
  fogged glass. Since the soak cap gives diminishing returns, "more mist" is never wrong. That's fine for pillar 4,
  but it isn't yet a choice.
- **In absence, the water can come before the first tree** (seed 2: water at Y73.5, the bird's tree at Y84.8). That
  doesn't matter: absence only has to arrive, not tell the beats in order.
- **The hollow is much bigger than the old pond.** The flood line holds ~250–280 cells, about 10 % of the box, and
  the care runs fill ~120 of them by Y40. The people's "never build below the spill line" rule therefore costs a
  real slice of the flat land near the founding spot on presets 0 and 2. A P8 terrain wants a slightly deeper,
  smaller hollow.
- **The wood takes the meadow.** By Y100 in (c), trees cover about a quarter of the box and rabbits are falling
  (60 → 30). That's the "forest's edge" care (the snips) arriving on cue, but nobody snips in this lab, so it's
  unchecked.

**What I'd change in `keepers-hands.md`:**
- §4: say outright that the people take over the watering after a few mistings, and give light more weight, or let
  it go.
- §5.2: the water table is capped at the hollow's spill line, and "never below what the box already has" means the
  pond only grows.
- §2: put the gates' numbers in (camp at 15 % meadow plus rabbits; the seed at the first bushes; village = pond of
  20 cells plus 8 trees).
- §5.5: the forest's edge needs a brake (the people's woodcutters, or the snips) by the late camp, or it eats the
  meadow and the rabbits.

## 7. Tests (`dotnet test Tools/EcologyLab/Tests`: 20 passed)

| Test | What it checks |
|---|---|
| Determinism | Same seed: identical state fingerprint and identical frame pixels. A different seed: a different box |
| (a) still greens | By Y40 it has meadow > 10 % and the camp; the village comes by Y100 |
| The tree | (c) grows the tree by Y20; (b) takes ≥ 2× as long from planting and finishes ≥ 10 years later; (a) never plants, and its wild tree comes ≥ 25 years after (c)'s |
| Over-misting | (d) surfaces water sooner than (c) and shows the too-humid state; (c) never does |
| The arc in order, with care | camp < 6 years, then bushes, then planted, then grown, then water, then village |
| Ponds and die-back | Over 60 years, all 4 keepers, every season: water and reeds only in basins, no pond cell dries, no cell moves back along the succession, the tree never loses a stage |
| The flood line at the start | The table starts below the lowest ground, no water, all bare; still dry after the first over-misted year |
| PNG writer | Writes a valid file |

The tests run on all three terrains. They aren't in CI: CI's sim job runs `Tools/SimTests` only. Adding the lab
would touch `ci.yml`, and this spike touches only `Tools/EcologyLab` and docs.

## 8. What a P8 prototype with art would need

- **Sim:** port `Eco` into `Sim/` as a land layer on `SiteMap`. The cover per cell replaces the static forest,
  meadow and pond flags; the main hollow's spill replaces `WaterY`; the gates feed `SimConfig.villageAt`. It costs
  about 2.6 k cells × a few float ops a season, so the 48 h catch-up (192 seasons) is trivially cheap. Saves need
  cover (bytes), soil and moisture (floats or quantised), plus the table, humidity, glass and tree state.
- **Terrain:** a guaranteed hollow, smaller and deeper than today's dip, away from the founding flat (research 14
  §3.5's validator).
- **View:**
  - Vertex-colour terrain tint by cover and moisture, with a spread animation on a live mist.
  - Instanced vegetation in 3 stages (tufts, bushes, tree seedling / young / grown).
  - The water table band in the existing soil cross-section, and a rising pond mesh with reeds.
  - Droplets and fog on the glass, and the mist effect.
  - Rabbit and bird dolls driven by the counts.
- **UI:** the kit button and its rail (cloth, mister, seed, snips), replacing Clean and Shape.
- **First run:** wipe → dust → the first mist → green → the wanderers.
- **Debug panel:** give mist, skip seasons, set humidity / table / tree stage, spawn a shower, force the gates.
- **Open design points for the user:** the light need (§6), the water need being taken over by the people, and the
  forest's brake.
