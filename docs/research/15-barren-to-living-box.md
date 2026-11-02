# 15 — From a barren box to a living one (P8 groundwork)

Research date: **2026-11-02**. Question from the user, after research 14 §3: *"right now the aquarium starts with a
bunch of things already. I would make it start with nothing and grow it. First, light rain brings grasslands with a
few bushes and some animals like rabbits to hunt; the peeps can build tents. Wood grows slowly (maybe you plant it?
how can you favour its growth?), then bigger storms make ponds where the land is low. Peeps won't build on low land.
From a barren dirt plane to a full ecosystem."* The user wants to **understand it fully before committing**: this is
research, not a plan.

**Update 2026-11-02:** the user reshaped this into [design/keepers-hands.md](../design/keepers-hands.md): the Keeper
**mists** the box (no rain, **no storms**: the water table rises with misting and surfaces in the lowlands), the people
are there from the start, care is rebuilt on the land (humidity, water level, weeds, the forest's edge), and the tree's
care is water + light + weeding + seasons + the people learning (**no nibbling**). Where they differ, that draft wins.

Markers: **[V]** read in the source, **[S]** secondary or search summary, **[K]** a game as I know it, not
re-checked, **[code]** read in the repo, **[est]** my guess to test. Recommendations are marked ★.

---

## 0. TL;DR

- **The idea is strong, and it fixes two things at once.** It gives the camp era something to *do* (bring the land to
  life), and it turns the box's growth into the player's doing. "This meadow exists because I rained here" is the
  authorship P7 is chasing, but through the land instead of questions.
- **Real ecology gives us simple rules that are also good game rules.** Grass covers moist ground first.
  Grazers keep grassland open. Grass competes with tree seedlings for water. **Thorny bushes shelter saplings from
  grazers** (nurse shrubs). Once a tree stands, **jays bury acorns in open ground** and the wood spreads by itself.
  Water collects in hollows. Each rule is visible in the box, and each one is a lever for the player.
- **How the player favours wood (decided 2026-11-02, §5):** the Keeper plants **one seed and cares for it until it
  grows**; from that first tree, birds make the forest sprout by itself. The tree's care is watering first, plus
  light (clean glass), weeding and shooing nibblers, and the people learning to care for it too. No bush
  placement (the user didn't like it).
- **Ponds ★:** storms fill hollows up to their spill level (a basin fill on the 60 × 44 grid, not a fluid sim). The
  people never build below a basin's spill line, so the first storm floods exactly the land nobody touched, and the
  rule reads as the people's wisdom.
- **Stages of the box** (barren → first green → scrub and rabbits → first wood → water → living land) become the
  camp era's arc, and **the village needs wood and water**: the land gates the era, gently.
- **Risks:** a barren box is a weak *first screenshot*; the first-run design has to change (§7). Absence must not
  stall the land, so nature keeps moving slowly on its own (§6.7). The art cost is real: vegetation growth stages
  and a few animals in the style kit.
- **The cheapest way to understand it before committing ★:** a **sim-only Ecology Lab** (lane A, no Unity art).
  Run the rules on the real terrain for 3 seeds and 4 player strategies, and output **top-down time-lapse images** of
  the land year by year, like the Growth Lab spike. About 2–3 agent days. You'd *see* barren → living, and whether the
  levers matter, before any art or design change (§9).

---

## 1. What today's box starts with [code]

- The land comes from one of five fixed terrain specs (`Settle/Terrain.cs`). The pond is **static**: every cell
  below `WaterY` is pond from the start (`SiteMap.pond`).
- The site grid already has the layers this needs, in early form: `baseFert` (fertility noise − slope), `waterDist`,
  flags for forest, meadow, rock and canal, and per-cell wear for paths.
- The box opens on a **camp** with founders, a fire, starting woods and the pond. The first run is: misted glass →
  first wipe → the camp waves → name the box → the first wish (rain) → "their first year" (`first-session-and-check-in.md` §2).
- Shaping tools: rain, grove, herd in the camp; channel, lamp, hill & hollow in the village; outcrop, spring, dye
  meadow in the town (research 10).

So the change is: the **start state** becomes bare dirt, and **forest/meadow/pond become live layers** that grow,
not flags set once at the start.

---

## 2. Reference games

| Game | How the land comes alive | What we take | What we avoid |
|---|---|---|---|
| **Terra Nil** [S] | A "reverse city builder": the wasteland is cleaned to soil, irrigators turn soil into greenery, then wetlands, forests and more biomes, in stages ("Make Green", "Burn It Down", "Wrap It Up"). The designer's aim: nature valued for itself, not for what you gain | **Barren → green as the core pleasure.** Ordered stages (soil before grass before forest). The moment green spreads from a tap | Many building types and menus; a reviewer found it "awful to play" for usability. Ours stays at one gesture per tool |
| **The Planet Crafter** [S] | A terraformation index rises; thresholds bring visible life in order: moss, then insects, fish, frogs in ponds, then mammals | **Visible milestones in a fixed order.** Each new kind of life is an event (a card) | A number to grind; a long wait between stages |
| **Equilinox** [S] | Plants make the soil more fertile so wildlife can thrive; each species needs enough of the right environment; balance matters | **Plants build soil** (a positive feedback); species requirements | Its points currency |
| **From Dust** [K] | Vegetation spreads by itself next to water; you move the earth and water | **Self-spreading cover from simple rules** | Disasters that wipe villages |
| **Timberborn** [S] | Water per tile, levelled through four "virtual pipes" to its neighbours; droughts stop the flow, floods come with rain; beavers build dams | Water that collects by height | A full fluid sim (costly) and floods that players say they can't recover from: never here (pillar 4) |
| **Reus** [K] | A god shapes biomes; humans settle the land you made | People arriving *because* of the land | Humans turning greedy and punishing you |
| **Viridi** [S] | A pot of succulents that grows in real time | A slow, kind cadence | Plants that can die from neglect |

---

## 3. The real ecology underneath (our rules come from here)

| Fact | Source | Game rule |
|---|---|---|
| Bare ground is colonised first by grasses and herbs where there's moisture; woody plants come later (primary/secondary succession) | standard ecology [K] | Rain greens moist dirt; bushes come after grass; trees after bushes |
| Grazing that is "intense enough to counteract succession, but weak enough to allow flowering" keeps grassland open | grassland management review [S] | **Rabbits keep the meadow a meadow.** With few rabbits, bushes creep in |
| Small grazers (rodents, lagomorphs) strongly control grass and can reinforce grassland ↔ shrubland transitions | dryland study [S] | The rabbit count moves the grass/bush balance |
| Grass reduced established tree seedlings by ~43 % (competition for water) | Patagonian greenhouse study [S] | **A seed planted in thick grass mostly fails; one in bare or wet ground does better** |
| Saplings survive grazing far better inside thorny nurse shrubs (e.g. oaks inside blackthorn); the shelter weakens under very heavy grazing | Smit et al. 2006/2007, wood-pasture experiments [S] | **Plant inside the bushes** and the rabbits can't reach it; with too many rabbits even that fails |
| Jays cache thousands of acorns a year, mostly in open ground and at wood–grassland edges; the unrecovered ones grow; oaks spring up in meadows and along paths | Countryfile, Audubon, acorn-caching studies [S] | **Once a tree is grown, birds plant the next ones** at the wood's edge and along paths: the wood spreads by itself |
| Water collects in closed hollows up to the spill point | hydrology [K] | Storms make ponds in hollows; the spill level is the flood line |
| Reeds, frogs and water birds follow standing water | [K] | The pond brings its own species |

What makes this good for play: every rule is **visible** (grass, bushes, rabbits, birds, water), **local** (it happens
where you acted) and **pushable** (a tool or the people move it).

---

## 4. The box's stages, with the people inside them

A sketch for the river-valley setting. Years are game years (1 real hour = 1 game year) [est].

| Stage | Trigger | What you see | The player's levers | The people |
|---|---|---|---|---|
| **0 · Barren** | New box | Dry dirt plain, a few stones, the glass misted. Hollows and a dry stream bed are visible in the land | **Rain** (drizzle) on a spot | Nobody yet |
| **1 · First green** | Rain on dirt | Green spreads from where it rained, over a few seconds live, then slowly. Insects (a cricket chirp) | More rain, where you want the meadow | — |
| **2 · Scrub and rabbits** | Grass ≥ ~15 % of the land | Low bushes appear in the grass (more next to other bushes); **rabbits** hop in from the edge | Rain; the herd tool **calls** the first rabbits sooner | **The people arrive following the rabbits** over the box's edge; they pitch **tents** (hides from the hunt). Hunter and gatherer jobs. The camp era begins |
| **3 · First wood** | The Keeper's tree reaches "grown"; birds start spreading its seeds | A young tree, then a few. Small birds in the bushes | **Plant a seed** (grove), then care for it: rain, wipe the glass above it, weed round it (§5) | Woodcutter job once there are ≥ ~5 grown trees; the fire burns wood instead of dung |
| **4 · Water** | A **storm** (rain levelled up, or held longer) over a hollow | The hollow fills; reeds at the edge, frogs at night, ducks | Storm over the hollow you choose; later **hill & hollow** makes new basins | Fisher job; the people never build below the flood line |
| **5 · Living land** | Wood spreading by birds, a pond, ≥ N species | Woods with deer, a meadow with bees, a pond with herons | All tools; the balance between them | **The village needs:** wood for huts, moist flat land for fields, water near. The land gates the era |

**What gates the village era** [est]: today it's population 10, 6 buildings, 10 years (`SimConfig.villageAt`) [code].
Add: ≥ 1 pond and ≥ N grown trees. If the player never plants or storms, natural weather does it slowly (§6.7), so
the era still comes, only later.

---

## 5. "How can you favour wood?" The Keeper's first tree (decided 2026-11-02)

User: *"care for the tree until it grows. From there the forest sprouts."* No nurse-bush placement. The shape:

1. **One seed.** The Keeper plants it (the grove tool becomes "plant a seed"), once the grass is in (stage 2).
2. **Care until it grows.** The sapling goes through visible stages over a few game years: seed → sprout → sapling →
   young tree → **grown** (it blossoms, then drops its first acorns).
3. **The forest sprouts.** Birds (jays) carry its seeds to open ground nearby. Those wild seedlings grow on their
   own (no care needed; rain only speeds them), and the wood spreads from there.

### 5.1 Ways to care for the tree (candidates)
One need shows at a time, **in the tree's look** (no bars). Neglect never kills it: it just stops growing and
wilts a little, and natural rain eventually perks it up (pillar 4).

| Mechanic | The need, as you see it | The Keeper's answer | New verb? | Notes |
|---|---|---|---|---|
| **Water** ★ core | Leaves droop and pale; the soil ring around it cracks | **Rain** on it; it perks up at once | No (rain) | The user's starting point; the most readable need |
| **Light** ★ | It leans toward the glass and grows slowly under grime; shadows over it | **Wipe the glass** above it | No (wipe) | Ties the tree to *care = clarity*: the cleaning you already do now grows the tree |
| **Weeding** ★ | Tall grass and weeds crowd round it (grass really competes with seedlings for water, §3) | **Prune** the ring around it | No (prune) | Reuses the care gesture; the grass you rained in is the tree's competition, a small irony |
| **Nibblers** (optional) | A rabbit sits by it, nibbling; bite marks on the bark | **Flick the rabbit away** (comic hop) | Yes (a flick, like the herd) | A toy, not a chore: at most once per return [est]. Also gives the hunters a reason ("the warren by the tree") and a P7 dilemma |
| **The people learn** ★ | — | After you've watered it a few times, a child starts carrying water in a skin or bucket; later someone puts a ring of sticks round it | No | *Care moves up* (game-definition §5 rule 5): the people copy the Keeper. A strong story beat and the tree's "keeper" becomes a notable thread |
| **Seasons** | Bare in winter, a growth spurt in spring | Nothing to do | No | Gives the growth a rhythm; spring is when care counts most [est] |
| Storm shelter | A storm bends it | Hold a finger over it | Yes (hold) | Fiddly, and storms happen offline; **not recommended** |
| Tap to tend (Viridi) | — | Tap it | Yes | Too thin; **not recommended** |

★ A recommended set: **water + light + weeding**, all with existing gestures, and **the people learning** as the
payoff. The nibbling rabbit as an optional toy, decided after the spike.

### 5.2 After the first tree
- **The forest spreads** through birds, biased to open ground, edges and paths; wild seedlings need no care.
- **Rabbits still matter, gently:** many rabbits nibble wild seedlings, so the wood spreads slower near a big warren.
  Hunting more means a faster wood and less meat, which is a soft trade-off the people handle (and a P7 dilemma),
  never the Keeper's chore.
- **The question flips:** from "how do I get a tree?" to "how much wood do I let them have?" (the meadow the bees will
  need, the clearing for the village fields).
- **The first tree stays special:** the people's oldest tree, a gathering spot; a book page; in the town era it can be
  the "old oak" in the square.

### 5.3 To test in the Ecology Lab
How long care takes (a few game years with care, ~2–3× longer without [est]); whether natural rain alone grows it
eventually (absence); how fast the forest spreads from one tree; the warren's brake on the spread.

---

## 6. The systems, roughly

### 6.1 Cell layers (on the existing 60 × 44 site grid)
- **soil** 0..1: dirt → rich; grass and leaf litter raise it slowly (Equilinox's positive feedback).
- **moisture** 0..1: rain adds locally, it drains downhill and dries over seasons; ponds keep their neighbours wet.
- **cover** (one byte): bare, grass, tall grass, bush, seedling, young tree, grown tree, reeds, water; plus the
  people's own (field, yard, path, lot) from Settle.
- **water** level per basin (not per cell, §6.3).
- Global counts, not individuals: **rabbits**, birds, deer and so on per area, stepped like the crowd. The animals
  you *see* are a view of those counts, like the crowd's peg dolls.

### 6.2 The step (once a game season, deterministic from the seed)
1. Moisture: rain and storm inputs, downhill drain, drying.
2. Cover: each cell may move one step along its succession by local soil, moisture, neighbours and grazing
   (probabilities from a hash of seed + cell + season, so catch-up == live).
3. Grazing: rabbits per area eat seedlings and tall grass; nurse bushes cut that by a factor [est 0.2].
4. Birds: grown trees seed nearby open cells, biased to edges and paths.
5. Animals: counts move toward what the cover supports (arrive, grow, leave gently).
6. Settle reads cover and the flood lines when it places the next building.

### 6.3 Ponds without a fluid sim ★
Compute each closed hollow once from the terrain (a priority-flood basin fill, standard for terrain): its **spill
level** and its cells. A storm adds water volume to the basin it falls in; the level rises toward the spill line;
evaporation lowers it slowly in summer. Cells under the current level are water; cells under the **spill line** are
the **flood line** the people never build below. It's cheap on 2 640 cells and deterministic. A full Timberborn-style
sim isn't needed in a sealed box.

For this, the **terrain generator must guarantee 1–3 hollows** of useful size and a dry stream bed, away from the
flat founding ground (research 14 §3.5's validator).

### 6.4 Tools, re-cast for a barren start [est]
| Era | Tool | Change from today |
|---|---|---|
| Camp | **Rain** | Drizzle (tap) → rain (rub) → **storm** (rub long, unlocked at stage 3): the storm is what fills hollows |
| Camp | **Grove** | Becomes **"plant a seed"**: one tree the Keeper cares for until it grows (§5), not an instant wood |
| Camp | **Herd** | Becomes **"call"**: the first rabbits (or the first deer when the wood is big enough) arrive sooner; you never spawn animals the land can't hold |
| Village | **Hill & hollow** | Now matters more: a hollow is a future pond |
| Village | **Channel** | Carries water from a pond; moisture follows it (fields, reeds) |

### 6.5 Art and view
- The terrain mesh tints each cell by cover and moisture (vertex colours; dirt → green), with a short spread
  animation when it changes live.
- Instanced vegetation in the style kit with growth stages: grass tufts, 2 bush sizes, seedling / young / grown
  tree, reeds.
- New animals: rabbit, small bird, jay, duck, frog (sound + a few), later deer and heron. Low poly like the peg dolls.
- That's the largest cost, but it's all reusable: every later setting uses the same pipeline with its own palette.

### 6.6 Mess and care
Overgrowth keeps its role as mess, now with a source: the nearest cover (ivy from the wood, reeds near the pond). The
glass grime is unchanged.

### 6.7 Absence ★
Nature keeps moving without the player: natural light rain a few times a year, slow spread from what's there, birds
doing their work. A box left alone for a weekend still greens, only less. The Keeper's tools **speed it up and steer
it**; they're never needed to avoid loss. Nothing dies back from neglect. Mess stays capped as today (pillar 4).

---

## 7. The first-run problem, and a better first run

A bare dirt plain is a weak first screenshot. But the **first rain turning dirt green** is a very strong first
*moment* (it's Terra Nil's whole hook). A possible first run [est]:

1. The box arrives, misted. **Wipe** (kept: the gesture that "felt amazing").
2. Behind the glass: a dry, cracked plain, a dry stream bed, hollows. Silence, a bit of wind.
3. A cloud icon pulses: **rain**. Rub over a spot; it rains; **green spreads from your finger**, insects start to chirp.
4. "Their first year" time-skip, now for the land: the green spreads, bushes appear, **rabbits hop in**.
5. **The people walk in over the edge, following the rabbits**, and pitch the first tents. The chief looks up at the
   glass: "Someone's watching over us." Name the box.
6. First card, evening talk, close (as today).

It's a stronger origin story than "a camp is already there", and it makes the people *come because of what you did*.
It also means every new box (oasis, mountain) starts with its own barren land and its own first green (the oasis:
a spring first).

**Cost:** it reworks the first run (built and tested in the slice). That's the real price of the idea, besides the
art.

---

## 8. What it changes in the design docs

- `game-definition.md` §4 (the civ starts as a camp → it starts with nobody), §6 (tools re-cast, §6.4), §8 (eras
  gated by the land too).
- `first-session-and-check-in.md` §2 (the first run, §7 here).
- Research 10 (tool level-ups), research 08 (Settle reads cover and flood lines; organic growth still has to read
  well: the Growth Lab criteria).
- Pillars hold: Keeper, not King (you change conditions; nature and the people do the rest); absence welcome (§6.7);
  the glass is the interface (the land shows its state, no bars).

---

## 9. How to understand it before committing ★: the Ecology Lab

A spike like the Growth Lab, **sim only, lane A, no art**, about 2–3 agent days:
- The cell layers and season step (§6.1–6.3) on the real terrain specs, 3 seeds.
- **Four scripted players** over 40 game years: (a) does nothing; (b) plants the seed and only rains on it;
  (c) plants and does all the care (rain, clean glass, weeding); (d) like (c), plus heavy hunting.
- Output **top-down PNG time-lapses** (cover colours per cell, water in blue, rabbits as a number) every 2 years, plus
  curves (grass %, bush %, trees, rabbits, pond area, the year the first tree grew, the year the village gate opens).
- **Answers:** does barren → living look and feel right over time? Do the levers matter (c and d clearly beat a and b for the
  first tree; d spreads the forest fastest)? Does absence still progress (a still greens)? Are the times right for a camp era of a few real days?
- You'd look at the images and decide; only then does it become a P8 prototype with art.

---

## 10. Questions for you (to settle before any commitment)

1. **Where do the people come from?** ★ They walk in over the edge, following the rabbits, at stage 2 · a few are
   there from the start, waiting at a cold fire · they arrive with the first tents already up.
2. ~~**Planting.**~~ **Decided 2026-11-02:** the Keeper plants one seed and cares for it until it grows; the forest
   sprouts from it (§5). No nurse-bush placement. Still open: which care mechanics (§5.1, ★ water + light + weeding,
   the people learning; the nibbling rabbit after the spike).
3. **Storms.** ★ Rain levels up into a storm (rub longer), unlocked at stage 3 · a separate storm tool · natural
   storms only.
4. **The low-land rule.** ★ The people never build below a hollow's flood line, from the start · they build there
   until the first flood and then move (a story, but buildings would move, which breaks "buildings never jump") ·
   no rule.
5. **The land gates the village.** ★ Yes, gently (≥ 1 pond, ≥ N trees), with natural weather as the fallback · no,
   eras stay as today.
6. **First run.** ★ Rework it around the first green (§7) · keep today's first run and start barren only from the
   second box.
7. ~~**Next step.**~~ **Decided 2026-11-02:** the Ecology Lab spike (§9), issue #228.

## Sources
- [Science Friday: Terra Nil prioritises restoring an ecosystem](https://www.sciencefriday.com/articles/terra-nil-video-game-climate-change/) [S]
- [GDC 2023: Developing Terra Nil, a strategy game about nature, not expansion](https://gdcvault.com/play/1029027/Developing-Terra-Nil-A-Strategy) [S]
- [Terra Nil wiki: Toxin scrubber](https://wiki.terranil.org/w/Toxin_scrubber), [Irrigator](https://wiki.terranil.org/w/Irrigator) [S]
- [Adrian Hon on Terra Nil](https://adrianhon.substack.com/p/terra-nil) [S]
- [Planet Crafter wiki](https://planet-crafter.fandom.com/wiki/), [Corrosion Hour insects guide](https://www.corrosionhour.com/planet-crafter-insects-guide/) [S]
- [Equilinox on IndieDB](https://www.indiedb.com/games/equilinox) [S]
- [Game Developer: deep dive into Timberborn's water](https://www.gamedeveloper.com/design/deep-dive-timberborn-s-water-mechanics) [S]
- [Smit et al.: nurse plants, tree saplings and grazing pressure](https://folia.unifr.ch/unifr/documents/300362) [S]
- [Does facilitation by nurse shrubs depend on grazing resistance?](https://research-portal.uu.nl/en/publications/does-the-strength-of-facilitation-by-nurse-shrubs-depend-on-grazi/) [S]
- [Smit et al. 2007, Oecologia](https://infoscience.epfl.ch/record/141572/files/Smit%20et%20al%202007%20(Oecologia).pdf) [S]
- [Dryland grasses and shrubs establishment](https://par.nsf.gov/biblio/10298286-germination-early-establishment-dryland-grasses-shrubs-intact-wind-eroded-soils-under-greenhouse-conditions) [S]
- [Countryfile: jay guide](https://www.countryfile.com/wildlife/birds/jay-guide-how-to-identify-behaviour-where-to-see-in-britain) [S]
- [Audubon: could corvids replant a forest after fire?](https://www.audubon.org/magazine/winter-2019/could-corvid-conservation-corps-replant-forest) [S]
- [Acorn caching by jays and mice (Plant Ecology)](https://link.springer.com/article/10.1007/s11258-010-9839-1) [S]
- [Natural Resources Wales: seed dispersal](https://www.naturalresourceswales.gov.uk/media/686728/information-note-seed-dispersal.pdf) [S]
