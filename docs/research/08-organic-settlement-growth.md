# 08 — Organic settlement growth: the people decide where things go

Research date: **2026-10-29**. Question from the user: the village should grow *organically*, like a real settlement.
Placement is decided by the people (the sim), not by the player, not by random scatter, not by a fixed grid.
Context: a 6 × 4.4 unit box seen from above at an angle, 10–40 buildings, eras Camp → farming Village → crafts Town,
everything drawn in code by the style kit ([style-kit.md](../design/spikes/style-kit.md)).

Markers: **[V]** read in a primary source (paper, official doc, developer's own words). **[S]** secondary source only
(press, wiki, guide, search summary). **[est]** my estimate, to measure. **[unverified]** I couldn't open the source.
This doc gives options and a recommendation. It doesn't decide anything.

---

## 0. TL;DR

- **Real villages follow a few rules you can see from above:** houses cluster round a shared point (well, green,
  church), front onto paths, and run their plots back from the path. Fields go on flat land at the edge. Dirty water
  crafts (tanners, dyers, mills) go downstream at the edge. Paths are worn in first and paved later. Towns densify by
  infilling backlands and by market stalls turning into buildings. Plot lines persist for centuries (§1).
- **The best-matched technique is Emilien et al. 2012**, "Procedural generation of villages on arbitrary terrains".
  Each building type scores candidate spots with a weighted sum of simple terms (near others but not too near, near a
  road, preferred slope, near water, high ground for churches). Every new building is connected to the road network at
  once, and reusing an existing road is cheaper than a new one. Plots then grow back from the road. Ubisoft adapted it
  for Ghost Recon Wildlands (§2.1).
- **Paths should come from walking.** Trail models (Helbing 1997) and GDMC entries (AgentCraft, after TownSim) wear
  the ground where people walk and turn busy cells into roads. Ostriv and Foundation do this in shipped games (§2.2).
- **Recommendation (§4): a "scored frontage" placement layer in pure C#.** A 0.1-unit site grid (60 × 44 cells)
  holds static terrain layers and live layers: wear, water, fertility, forest, occupancy. Each sim month the economy
  still decides *what* to build. The placement layer decides *where*: it samples spots along worn paths and in gaps,
  scores them per building type, picks the best (ties broken by a seed hash), and lays a path to it. Every season,
  trips from homes to the well, fields and work wear the ground, and paths emerge. Rain, groves and canals edit the
  layers, so the people's answer appears where the land changed.
- **Determinism is easy here:** results (positions, path polylines) are *stored* in the save, and live play and
  catch-up run the same code on the same device (§3.5).
- **First spike (2–3 agent days):** a headless "Growth Lab" plus an editor time-lapse that renders years 0–120 of 3
  seeds with the style kit. It passes if viewers pick it over a random-scatter control as the one that "grew like a
  real village", nudges get a visible answer near the shaped spot, and live play = catch-up (§5).

---

## 1. How real settlements grow

### 1.1 Patterns and why they happen
| Pattern | Why | Game rule it suggests | Source |
|---|---|---|---|
| **Nucleated** village: houses round a church and/or green | Debated: soils, open-field farming, lords, population [S] | One shared centre (well/green) from founding; houses score higher near it | [Nucleated village](https://en.wikipedia.org/wiki/Nucleated_village) [S] |
| Church on/by the green in >30 % of a Lincolnshire sample | The green is the village's oldest open space | Keep the green free; landmarks sit on its edge | [Everson & Stocker 2012](https://archaeologydataservice.ac.uk/catalogue/adsdata/arch-1164-1/dissemination/pdf/Volume_27_2012/MSR27_12-19_Everson_Stocker.pdf) [S] |
| **Row** village: tofts (house plots) and crofts (gardens) running back from a main road, a back lane behind, church and manor at the end | Planned or regularised villages, 9th–10th c. England | Lots are strips: narrow frontage on the path, depth behind; a back lane appears later | [Northants HER, Steane](https://her.northamptonshire.gov.uk/Designation/DNN19608) [V] |
| **Linear** settlement along a road, valley or river | Transport and water | A path or a canal pulls houses along its length | [Internet Geography](https://www.internetgeography.net/topics/patterns-of-settlement/) [S] |
| **Wet point** (near water) vs **dry point** (above flood) | Water is heavy to haul; floods kill | Score: near water, but not on the wet edge | [Internet Geography](https://www.internetgeography.net/topics/patterns-of-settlement/) [S] |
| **Beady ring** hamlets (Vaucluse): a ring of open spaces that widen and narrow, formed by repeating one local rule | Each house keeps its door on the shared open space | Every house must face open space joined to the rest (no walled-in houses) | [Hillier et al. 1976](https://ideas.repec.org/a/sae/envirb/v3y1976i2p147-185.html) [S], [Gassin case, arXiv 2104.04131](https://arxiv.org/pdf/2104.04131) [S] |
| Fields at the edge: village ringed by 2–3 open fields | Short walk out, homes kept together | Fields score on flatness and fertility, and *against* the centre | [Nucleated village](https://en.wikipedia.org/wiki/Nucleated_village) [S] |
| **Desire lines**: walked trails settle into a compromise between direct routes | Walking wears the ground; worn ground draws walkers | Wear map → trails → paths → roads | [Helbing et al. 1997](https://arxiv.org/abs/cond-mat/9806097) [V] |

### 1.2 Growth over time: continuity, infill, densification
- **Plots outlive buildings.** Conzen found Alnwick's late-medieval plan little changed by the 1770s; plot lines seldom
  move for centuries ([Conzenian tradition review](https://journal.urbanform.org/index.php/jum/article/view/3896) [S]).
  → *Never move a lot. Replace what stands on it.*
- **Burgage plots**: narrow strips running back from the high street to a rear lane
  ([Hungerford](https://www.hungerfordvirtualmuseum.co.uk/index.php/10-themes/954-burgage-plots) [S]); widths cluster on
  one unit width ([Tait, four Scottish burghs](https://journals.socantscot.org/index.php/psas/article/view/9724) [S]).
- **Infill**: backlands were built over and alleys (vennels) cut between plots
  ([Scottish burgage review](https://journals.socantscot.org/index.php/psas/article/view/9608) [S]); plots were split as
  prosperity grew (Thame had 76 burgages by 1305) ([Thame Museum](https://interactive.thamemuseum.org/?p=1265) [S]).
  The "burgage cycle" is backland filling, then clearance, then redevelopment ([review](https://journal.urbanform.org/index.php/jum/article/view/3896) [S]).
- **Market squares fill up**: stalls became permanent and then buildings. Market Harborough's market place was infilled
  ([Harborough](https://www.harborough.gov.uk/directory_record/1276/market_harborough_conservation_area) [S]); Thrapston
  had stalls turned into houses by 1364 ([Northants HER](https://her.northamptonshire.gov.uk/Monument/MNN102670) [V]);
  Ashbourne's encroachments split the square in three ([Derbyshire HER](https://her.derbyshire.gov.uk/Monument/MDR12718) [S]).
  Brackley's market place was "a widening of the main street" ([Northants HER](https://her.northamptonshire.gov.uk/Monument/MNN8173) [S]).
  → *The square is the widest, busiest junction. In the Town era stalls appear there, and a few later harden into buildings.*

### 1.3 The crafts town (our third era)
| Fact | Source | Rule |
|---|---|---|
| Tanners and dyers sat by rivers, often at the edge, near mills: they needed water and dumped foul waste | [Ulm tanners' quarter](https://english.ulm.de/0/fishermens-and-tanners-quarter) [V], [Bury St Edmunds](https://heritage.suffolk.gov.uk/Monument/MSF24074) [V], [Bayeux marker](https://www.hmdb.org/m.asp?m=308085) [S] | "Wet" workshops: near water, downstream, far from the green |
| A dye house stood beside the town water mill; dyers and fullers sought riverside sites | [Hungerford](https://hungerfordvirtualmuseum.co.uk/index.php/component/phocagallery/category/282-97-98-high-street?Itemid=0) [S] | Workshops cluster round a mill: a craft street |
| One trade per street: York's Shambles was the butchers' street, earlier named after hay sellers and needle makers | [The Shambles](https://en.wikipedia.org/wiki/The_Shambles) [S] | Same-trade attraction term: a smith wants to be near smiths |

---

## 2. Game and procgen techniques

### 2.1 Interest maps + incremental roads (Emilien et al. 2012) — the closest match
From the paper ([PDF](https://perso.liris.cnrs.fr/egalin/Articles/2012-villages.pdf), [HAL](https://hal.archives-ouvertes.fr/hal-00694525)) [V]:
- **Seeding:** pick a random spot, check it can be built on, compute its interest `I(B)`, then keep it with
  probability based on `I`. Otherwise retry.
- **Interest** = `Σ wᵢ·fᵢ(B)`, each `fᵢ ∈ [-1, 1]`, and `0` if any term is −1 (forbidden). The weights depend on
  (village type, building type) and live in a "building encyclopedia".
- **Terms:** *sociability* (attraction-repulsion by distance to neighbours: close is good, too close is bad);
  *worship* (the same, towards religious buildings); *accessibility* (a bell around a preferred distance to the nearest
  road); *slope* (a bell around a preferred slope: flat for corn, hills for vines); *water* (decreasing with distance);
  *fortification*; *geographical domination* (higher than the neighbours: churches).
- **Roads:** right after each building, a shortest path to the network. Cost = slope + curvature + water + a huge cost
  for crossing buildings. Existing road costs are scaled by `w_ex < 1`, so roads get reused, not duplicated. An extra
  step adds *cycles*: look for a road node in a cone ahead and link to it.
- **Parcels:** each building claims road frontage on both sides of its projection onto the road. Corner plots go to
  whoever got there first. The plot then grows back from the road on a grid, with anisotropic cost (Chebyshev distance
  for houses gives near-rectangular lots, Euclidean for fields), water/road/wall as walls, and slope making it costly
  to spread uphill.
- It's one of the few methods that model *evolution over time* ([Beneš et al. 2014](https://cgg.mff.cuni.cz/~jaroslav/papers/2014-cities/2014-cities-paper.pdf) [S]).
  Ubisoft adapted it for Ghost Recon Wildlands ([LIRIS page](https://perso.liris.cnrs.fr/egalin/2014-2010.html) [S],
  [80.lv](https://80.lv/articles/procedural-technology-in-ghost-recon-wildlands/) [S]).

### 2.2 Paths from movement
| Technique | How | Fit for us | Source |
|---|---|---|---|
| Active walker trails | The ground has a "comfort" value; walking raises it, it decays; walkers steer toward comfortable ground and their goal. Ends in a minimal-detour network, not a tree | ★★★ the core of "paths emerge" | [Helbing et al. 1997](https://arxiv.org/abs/cond-mat/9806097) [V] |
| Prosperity nodes (TownSim → AgentCraft) | Agent actions raise a node's "prosperity", which decays; above a threshold a road is built from it. Agents prefer roads when pathfinding | ★★★ the same loop, discrete | [AgentCraft](https://ceur-ws.org/Vol-3217/paper3.pdf) [V], [TownSim](https://www.pcgworkshop.com/archive/song2019agentbased.pdf) [S] |
| Ant colony paths | Pheromone paths for GDMC villages | ★★ (same idea, more tuning) | [Deinböck, BNAIC 2024](https://bnaic2024.sites.uu.nl/wp-content/uploads/sites/986/2024/11/Procedurally-Generating-Natural-Looking-Villages-in-Minecraft-with-Ant-Colony-Optimization-Algorithms.pdf) [S] |
| L-systems with global goals / local constraints | Grow a road network from rules plus density maps | ★ city-scale, hard to control; no people in it | [Parish & Müller 2001](https://history.siggraph.org/?p=116923) [S] |
| Shipped: Ostriv | "Dirt roads are formed in a natural way at places where people move the most"; paved roads optional; no grid, any angle | ★★★ proof it reads well in a game | [dev forum post](https://gamedev.net/forums/topic/682846-city-building-strategy-game-in-development-ostriv/5318156/) [V], [ModDB](https://www.moddb.com/games/ostriv) [S] |
| Shipped: Foundation | Player paints zones; villagers place their own houses in them; streets come from where citizens walk | ★★★ the closest *player role* to ours | [Foundation wiki: Zones](https://wiki.polymorph.games/foundation/Zones) [S], [PC Gamer](https://www.pcgamer.com/au/foundation-is-a-freeform-medieval-city-builder-with-no-grids-to-follow) [S] |

AgentCraft notes that agent-built settlements cost much more than direct generation, but "people consistently find
the liveness of agent behavior compelling" ([paper](https://ceur-ws.org/Vol-3217/paper3.pdf) [V]). That supports our
plan: show the growth.

### 2.3 Other generators and games
| Name | What it does | Lesson for us | Source |
|---|---|---|---|
| Watabou Village Generator | Finds the densest spot (often on the highway) and makes the square from a distorted circle, with a tree or well in the middle | The square is a placed *void* with a centrepiece; houses come after | [devlog 1.4.0](https://watabou.itch.io/village-generator/devlog/507254/140-village-square-and-orchards) [V] |
| Watabou Medieval Fantasy City Generator | Voronoi wards; lots from a straight-skeleton-style split. The author says the method is "rather arbitrary", made to look good, not to model a city | Good look, no growth over time: wrong tool for a time-lapse | [itch page](https://watabou.itch.io/medieval-fantasy-city-generator) [V], [source](https://github.com/watabou/TownGeneratorOS) |
| Lechner et al. (Northwestern) | Agent-based land use (residential, commercial, industrial, roads) on terrain | Agents per land use: developers who look for sites by score | [TR 2007](https://ccl.northwestern.edu/papers/2006/TR-2007-33.pdf) [S], [arXiv 2510.15877](https://arxiv.org/pdf/2510.15877) [S] |
| GDMC competition | Generators judged on adaptability to terrain, function, narrative and looks | Our pass criteria can borrow the four axes | [Salge et al. 2018](https://arxiv.org/abs/1803.09853) [V] |
| AgentCraft (GDMC 2021, 2nd) | Agents gather, rest, build from schematics; roads from traffic; a written chronicle | Closest to our "sim + chronicle" game; schematics = our parametric buildings | [paper](https://ceur-ws.org/Vol-3217/paper3.pdf) [V] |
| Townscaper | Player says *where* to grow; the system picks fitting pieces (WFC on an irregular grid). Gardens only appear in enclosed flat spaces | Same split of control as ours, inverted: the land is the player's, the architecture is the sim's | [Game Developer](https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making) [S] |
| Manor Lords | Player draws burgage plots; homes face the first edge drawn (the road) | Frontage-first lots look right. But in our game the people draw them | [Dexerto guide](https://www.dexerto.com/gaming/best-burgage-plot-upgrades-manor-lords-2674835) [S] |
| The Universim | Roads follow a hidden, precomputed city blueprint | Cheap and tidy, but it can't respond to the player's shaping | [ModDB news](https://www.moddb.com/news/roads-are-coming-to-the-universim) [unverified: 403, search summary only] |
| Songs of Syx, Dwarf Fortress, Kenshi | No public write-up of town placement found | — | [unverified] |

### 2.4 Building orientation
- Emilien's lots have "one side neighboring a road and two sides perpendicular to it" ([PDF](https://perso.liris.cnrs.fr/egalin/Articles/2012-villages.pdf) [V]).
  Manor Lords homes face the road edge ([guide](https://www.dexerto.com/gaming/best-burgage-plot-upgrades-manor-lords-2674835) [S]).
- Rule: **door faces the path, long axis along the slope contour**; snap yaw to the path tangent ± a few degrees of
  jitter. StyleBox already does "face the well ± 12°" (`FaceYaw`), so this is a small change.

### 2.5 Deterministic from a seed
- Unity doesn't promise float determinism *across* platforms for IL2CPP; basic IEEE ops look portable in practice, but
  trig/transcendentals are the weak point ([Unity discussions](https://discussions.unity.com/t/state-of-determinism-in-unity/867770) [S]).
  Burst has an opt-in deterministic float mode ([Unity manual](https://docs.unity3d.com/Manual/burst/float-precision-determinism.html) [V]).
  Arm suggests double precision for cross-platform consistency ([Arm](https://learn.arm.com/learning-paths/cross-platform/floating-point-behavior/how-to-4) [V]).
- **We mostly sidestep this:** placement *results* go in the save. Live play and catch-up run the same binary on the
  same device, which is deterministic for the same inputs. Cross-device only matters if we ever re-derive a world from a
  seed alone (cloud save, shared seeds), and we don't plan to [est].

---

## 3. Connecting it to our sim

### 3.1 What changes in `SimCore`
Today `TryBuild` picks a **type** from the economy and `AddBuilding` takes the first free of 20 abstract slots;
`AddBuildingNear` picks the free slot closest *by index* to the nudge's slot; trash and nudges also store `slot`.
- **Keep the economy as the decider of *what*** (it's tuned and tested). Replace only *where*.
- A new pure-C# folder `Assets/Game/Sim/Settle/` (in the same `AquariumCiv.Sim` assembly, no `UnityEngine`):
  `SiteMap`, `PathNet`, `Placer`, `Encyclopedia`.
- `Building` gains `x, z, yaw, w, d, lotId` (keep `slot` for old-save migration). `Nudge` and `TrashItem` gain `x, z`.
- `SimState` gains `settle` (wear grid as `ushort[]`, path polylines with `foundedMonth` and `tier`, lots, the
  nature/water edits from nudges). It's serialised like the rest. About 2640 cells × 2 bytes ≈ 5 KB [est].
- The view (`StyleBox` → slice art layer) reads positions and path polylines; it no longer owns any layout.

### 3.2 The site grid (0.1 units, 60 × 44 cells)
| Layer | Static/live | Set by |
|---|---|---|
| height, slope, aspect | static per box (from the terrain seed), edited by "terrain shifts" | terrain generator (moved from `StyleBox.Height` into pure C#) |
| waterDist | live | pond + canals (channel nudges) |
| fertility | live | base noise + moisture (rain nudge: +, decays over years) + near water |
| forest | live | starting trees + groves (grove nudge); cut back by lumber |
| wear | live | seasonal trips (§3.4) |
| occupancy | live | lot id / field id / path / green / glass margin / nudge prop |

The grid lets every score be a lookup, so placement is cheap and testable with plain arrays.

### 3.3 Scoring per building type (Emilien-style, `I = 0 if any term forbids, else max(0, Σ wᵢfᵢ)`)
| Type | Main terms (+ attract, − repel) | Notes |
|---|---|---|
| Hut / House | + near the green (attraction-repulsion, preferred 0.5–1.2 u); + path frontage (bell at 0.15 u setback); + near own family's lot; − slope; − within 0.2 u of water; − tall near front glass | The core of the nucleated look |
| Farm (field + small barn) | + flat; + fertility; + next to existing fields (consolidation); − near the green (edge-seeking); field grows as a lot back from a path (Euclidean conquest) | Rain raises fertility: new fields go where it rained |
| Lumbermill | + forest edge; + path access; − near houses (noise) | Grove → the mill appears at its edge |
| Well | the founding green's centre; a 2nd well at the far end of a long row of houses | Wells are the first "commons" |
| Windmill | + high ground (domination); + open (no trees within 0.4 u); + near fields | Reads as a landmark on the hill |
| Watermill (Town) | must touch waterDist ≈ 0 on a canal or the pond outflow | A channel nudge is the way to get one |
| Workshops (Town: smith, dyer, tanner, weaver) | + near the mill/water; + same-trade neighbours; wet trades − near the green, + downstream | A craft street along the canal |
| Market | on the busiest junction (wear) at the green's edge; stalls on the green | Stalls later harden into buildings (§1.2) |
| Tavern | + busy junction; + near market | — |
| Church | + domination (highest local point); + seen from the green | — |

**Readability terms (game-only):** no lot within 0.25 u of the glass; buildings over 0.5 u tall get a penalty in the
front third of the box (they hide the people); minimum 0.08 u gap between lots so each roof reads at full-box zoom [est].

### 3.4 Paths emerge from people
1. Once per season, run a **Dijkstra flow field** from each destination (the well/green, each field, each workplace,
   the market). Cost per cell = `base × slope × (1 − 0.7·wear) × (∞ on buildings, water unless bridged)`. That is 5–10
   fields over 2640 cells, well under a millisecond [est].
2. For each home (weighted by its residents), trace down to each destination it uses and **add wear** along the way.
   Wear decays a little every season, so unused shortcuts fade.
3. Cells above thresholds become a **trail** (faint), then a **path** (dirt), then in the Town era a **road**
   (cobble). Chains of path cells are simplified to polylines (Douglas-Peucker) for the renderer. StyleBox already draws
   polylines with `DistToPaths`.
4. Every new building also gets an immediate connection path from its door (A* with existing paths at `w_ex = 0.3`), as
   in Emilien, so nothing is ever cut off.
Because routes prefer worn cells, the network settles into a few shared routes (Helbing's compromise), and a main
street appears without anyone placing it.

### 3.5 Steps per sim month (all in `Step()`, after the economy)
1. `TryBuild` picks a type (unchanged) → `Placer.Place(type)`:
   candidates = up to 24 deterministic samples: frontage points along paths (both sides, at setback), gaps between
   lots (infill), and points on the edge of the built area. Score each, take the best. Ties go to the lower
   `Hash(seed, buildingId, i)`. No draws from `rng`/`econRng`, so the economy's dice never shift.
2. Claim the lot (frontage then depth, Chebyshev for houses, Euclidean for fields), set yaw to the path tangent, write
   occupancy, connect to the path net.
3. Every 3rd month: wear pass (§3.4) and path tier updates.
4. Nudge answers (`NudgeStep`) call the same `Placer` with an extra term `+w·closeness(nudge.x, nudge.z)`. The answer
   lands where the land changed, for the same reasons the people would put it there anyway.
Catch-up = the same loop. A 48 h absence is 576 months, which should be well under 1 s on a phone [est: measure].

### 3.6 The player's shaping changes the scores
| Shaping | Layer edit | What the people visibly do |
|---|---|---|
| Rain (on a spot) | fertility +0.4 in a 0.8 u radius, fading over 3–5 years | A new field (or a field grows) there; a trail to it wears in |
| Grove | forest cells planted | A lumbermill on its edge, a path to it; later houses avoid it (− near forest for houses keeps it standing) |
| Channel / canal | waterDist recomputed along the dug polyline; cells become water | Fields line it; in the Town era a watermill and the dyers/tanners settle along it; a footbridge where the main path crosses |
| Terrain shift (later) | height/slope edit | Fields avoid the new slope; the church/windmill like the new hill |

### 3.7 Era change reshapes without moving lots
- **Camp → Village:** tents are *replaced in place* by huts on the same lots (plots persist, §1.2). The camp's fire
  ring becomes the green; the first well goes in its centre (Watabou-style void with a centrepiece).
- **Village:** fields **consolidate** (adjacent field lots merge into larger striped fields); paths on the main route
  get the "path" tier; huts along the main path become houses first.
- **Town:** the busiest junction becomes the **market square**: stalls appear on the green, and over time a few
  harden into buildings (encroachment). Backland infill fills the gaps behind frontage houses. The main path becomes a
  cobbled road. Workshops line the canal/pond outflow.
- Any demolition happens only at a scripted era moment, with an on-screen beat and a chronicle line (e.g. "The old
  camp ring was cleared for a market."). It's never silent.

### 3.8 Phone cost and small-scale readability
- Logic: grid lookups, ≤ 24 candidates per build, a few Dijkstras per season. Cheap [est].
- Rendering: the style kit already builds one static mesh per box (~20k tris). A new building or path tier change
  should **rebuild only the affected chunk** (e.g. 4 chunks per box) and not re-bake AO for the whole box. AO bake was
  0.3–0.45 s per box in the Editor ([style-kit.md](../design/spikes/style-kit.md)), too slow to repeat every change on
  a phone [est].
- Paths as ground vertex colour (trail = faint tint, path = dirt, road = cobble stripes) so wear shows up gradually.
- At 10–40 buildings in 26 m² of ground, scale is ~0.3–0.45 u per house: what StyleBox uses now.

---

## 4. Options and recommendation

| Option | What | Organic look | Answers the player | Cost / risk |
|---|---|---|---|---|
| **A. Scored placement only** | §3.3 scoring + connection paths, no wear | ★★ (clustered, but the paths look drawn) | ★★★ | Low |
| **B. A + wear-grown paths + frontage lots** *(recommended)* | §3.2–3.7 | ★★★ | ★★★ | Medium: tuning weights |
| C. Full agent sim (AgentCraft-style villagers walk and build) | Each person pathfinds and acts | ★★★ | ★★ | High: per-person catch-up, hard to keep readable and cheap |
| D. Hidden blueprint, revealed over time (Universim-style, or WFC) | Precompute a whole town from the seed and unveil it | ★★ | ★ (ignores shaping) | Low, but it betrays the premise |
| E. Fixed slots, hand-designed per box (today) | 20 authored positions | ★ | ★ | Lowest; doesn't scale to 3 eras or shaping |

**Recommendation: B.** Emilien's scoring (a published method that shipped in a AAA game) plus TownSim/Helbing-style
wear paths (shipped in Ostriv and Foundation). It's pure C#, the economy stays as it is, and the player's shaping
feeds straight into the scores. Start with Emilien's *best-of-K* instead of his random accept test: it's more readable
in a tiny box and needs no extra dice. Keep C as flavour only: villagers in the view *walk the flow fields* the sim
already computes, so the paths the player sees match where people go.

---

## 5. First spike: "Growth Lab" (2–3 agent days)

**Result (2026-10-29):** see [design/spikes/growth-lab.md](../design/spikes/growth-lab.md). Criteria 2–6 pass; 1 and 7
are ready for people.

**Question:** do scored frontage placement and wear-grown paths look like a real village growing, at our scale, and do
the nudges read as answered?

**Build**
1. `Assets/Spikes/Growth/` (throwaway, pure C# + EditMode tests): `SiteMap`, `PathNet`, `Placer`, `Encyclopedia`
   for Hut/House, Farm, Lumbermill, Well, Windmill, Market, Watermill + 2 workshop types. The terrain function is copied
   from `StyleBox` into pure C#.
2. A driver that runs `SimCore` (for the build order) with `Placer` on top, for 120 game years, writing a snapshot
   per year.
3. An editor time-lapse scene: StyleBox renders snapshot N (buildings from positions, paths from polylines with
   tiers, fields from lots, canals as water strips). Scrub or play years 0→120; export a GIF/MP4 per seed.
4. Debug overlay: heatmap of any score term or wear, and candidate dots with their score (the Emilien Fig. 5 view).
   This is the tuning tool.
5. Controls for the comparison: the same build order with (a) random scatter (no overlap) and (b) today's hand
   positions.
6. Debug cheats (per CLAUDE.md): rain/grove/canal at a clicked point, skip 10 years, force next era.

**Pass criteria**
| # | Check | Pass |
|---|---|---|
| 1 | Blind time-lapse test: the user + 2 others watch 3 seeds × (B, random control), order shuffled | ≥ 2 of 3 viewers pick B as "grew like a real village" on ≥ 2 seeds; the user agrees |
| 2 | Nudge answer is visible: 30 seeded runs per nudge kind | Answer building/field within 1.0 u of the shaped spot in ≥ 80 % |
| 3 | Live = catch-up | Stepping month by month vs `Advance(48 h)` gives identical positions and paths (EditMode test) |
| 4 | Sanity over 3 × 120 years | 0 overlapping lots, 0 buildings without a path to the green, 0 lots in the glass margin; ≥ 35 buildings fit |
| 5 | Emergence | At least one path forms that no connection step laid, and a viewer can point to "the main street" |
| 6 | Cost (Editor) | Placement + wear < 1 ms per month on average; 576-month catch-up < 300 ms [est: then check on a phone] |
| 7 | Era reads | Stills at year 10 / 50 / 110 are told apart as camp / farm village / crafts town by someone who hasn't seen the spike |

If 1 fails, look at the weights in the overlay before changing the method. If 2 fails, raise the nudge term or make
the nudge also reserve a lot.

---

## 6. Risks
| Risk | Mitigation |
|---|---|
| Looks like scatter at small scale (too few buildings for patterns to show) | Strong frontage + green terms; yaw snapped to paths; one unit lot width (§1.2); compare against the control early |
| Weight tuning never ends | The overlay tool; tune 3 seeds only; cap the spike at 3 days |
| Paths spaghetti (too many trails) | Higher wear thresholds, decay, `w_ex` reuse; ≤ 3 destinations per home |
| Nudge answers feel random | Extra closeness term; a chronicle line naming the place ("beside the new channel") |
| Canal through built land | The player's canal routes around lots (cost ∞), or stops at them: decide in the spike |
| Save migration from `slot` | Old saves: map slot → old view position once, then claim lots; slice saves are versioned ([save-versioning.md](../design/spikes/save-versioning.md)) |
| Rebuild cost on phone (AO bake) | Chunked rebuild, AO only for the new chunk; measure on device later |
| Time-lapse needs history of paths | Store `foundedMonth` per path segment and tier changes; the yearly snapshot can then be rebuilt |
| Eras need demolition | Only at scripted era moments, announced; lots never move |

## 7. How it fits the style kit
Everything stays code-generated from the palette: lots and fields are polygons that the kit already draws (fenced field
rect → any lot polygon), paths are the existing polylines with a tier colour, canals are a carved height edit plus a
water strip, and houses use the existing parametric `House(...)` with `w/d` from the lot. The layout moves out of
`StyleBox` (hand-placed `houses[]`, fixed `paths`, `FieldRect`) into the sim. The view becomes a pure function of state,
which is what the style-kit doc's next step 3 asks for.

---

## Sources
1. Emilien, Bernhardt, Peytavie, Cani, Galin, *Procedural generation of villages on arbitrary terrains*, The Visual Computer 2012 — [PDF](https://perso.liris.cnrs.fr/egalin/Articles/2012-villages.pdf), [HAL](https://hal.archives-ouvertes.fr/hal-00694525)
2. LIRIS publications page (Wildlands adoption) — https://perso.liris.cnrs.fr/egalin/2014-2010.html
3. 80.lv, Procedural technology in Ghost Recon Wildlands — https://80.lv/articles/procedural-technology-in-ghost-recon-wildlands/
4. Beneš et al. 2014, procedural city growth — https://cgg.mff.cuni.cz/~jaroslav/papers/2014-cities/2014-cities-paper.pdf
5. Helbing, Schweitzer, Keltsch, Molnár 1997, *Active walker model for the formation of human and animal trail systems* — https://arxiv.org/abs/cond-mat/9806097
6. Iramanesh & Kreminski 2021, *AgentCraft* — https://ceur-ws.org/Vol-3217/paper3.pdf
7. Song & Whitehead 2019, *TownSim* — https://www.pcgworkshop.com/archive/song2019agentbased.pdf
8. Salge et al. 2018, *GDMC Settlement Generation Competition* — https://arxiv.org/abs/1803.09853
9. Deinböck 2024, ACO villages in Minecraft — https://bnaic2024.sites.uu.nl/wp-content/uploads/sites/986/2024/11/Procedurally-Generating-Natural-Looking-Villages-in-Minecraft-with-Ant-Colony-Optimization-Algorithms.pdf
10. Parish & Müller 2001, *Procedural modeling of cities* — https://history.siggraph.org/?p=116923
11. Lechner et al., *Procedural modeling of urban land use* — https://ccl.northwestern.edu/papers/2006/TR-2007-33.pdf, https://arxiv.org/pdf/2510.15877
12. Watabou, Village Generator devlog 1.4.0 — https://watabou.itch.io/village-generator/devlog/507254/140-village-square-and-orchards
13. Watabou, Medieval Fantasy City Generator — https://watabou.itch.io/medieval-fantasy-city-generator, https://github.com/watabou/TownGeneratorOS
14. Game Developer, How Townscaper works — https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making
15. Foundation wiki, Zones — https://wiki.polymorph.games/foundation/Zones; PC Gamer — https://www.pcgamer.com/au/foundation-is-a-freeform-medieval-city-builder-with-no-grids-to-follow
16. Ostriv dev post — https://gamedev.net/forums/topic/682846-city-building-strategy-game-in-development-ostriv/5318156/; ModDB — https://www.moddb.com/games/ostriv
17. Manor Lords burgage guide — https://www.dexerto.com/gaming/best-burgage-plot-upgrades-manor-lords-2674835
18. The Universim roads (unverified) — https://www.moddb.com/news/roads-are-coming-to-the-universim
19. Hillier et al. 1976, *Space syntax* — https://ideas.repec.org/a/sae/envirb/v3y1976i2p147-185.html; beady ring/Gassin — https://arxiv.org/pdf/2104.04131
20. Nucleated village — https://en.wikipedia.org/wiki/Nucleated_village
21. Everson & Stocker 2012, church-on-the-green — https://archaeologydataservice.ac.uk/catalogue/adsdata/arch-1164-1/dissemination/pdf/Volume_27_2012/MSR27_12-19_Everson_Stocker.pdf
22. Northamptonshire HER, Steane (tofts, crofts, back lanes) — https://her.northamptonshire.gov.uk/Designation/DNN19608
23. Internet Geography, Patterns of settlement — https://www.internetgeography.net/topics/patterns-of-settlement/
24. British urban morphology: the Conzenian tradition — https://journal.urbanform.org/index.php/jum/article/view/3896
25. Hungerford, burgage plots — https://www.hungerfordvirtualmuseum.co.uk/index.php/10-themes/954-burgage-plots
26. Tait, burgage plots in four Scottish burghs — https://journals.socantscot.org/index.php/psas/article/view/9724
27. Archaeology of burgage plots in Scottish towns — https://journals.socantscot.org/index.php/psas/article/view/9608
28. Thame Museum, burgage plots — https://interactive.thamemuseum.org/?p=1265
29. Market Harborough conservation area — https://www.harborough.gov.uk/directory_record/1276/market_harborough_conservation_area
30. Northants HER, Thrapston market place — https://her.northamptonshire.gov.uk/Monument/MNN102670; Brackley — https://her.northamptonshire.gov.uk/Monument/MNN8173
31. Derbyshire HER, Ashbourne — https://her.derbyshire.gov.uk/Monument/MDR12718
32. Ulm, fishermen's and tanners' quarter — https://english.ulm.de/0/fishermens-and-tanners-quarter
33. Suffolk HER, Bury St Edmunds tannery — https://heritage.suffolk.gov.uk/Monument/MSF24074
34. Bayeux tanners marker (HMdb) — https://www.hmdb.org/m.asp?m=308085
35. Hungerford dye house — https://hungerfordvirtualmuseum.co.uk/index.php/component/phocagallery/category/282-97-98-high-street?Itemid=0
36. The Shambles, York — https://en.wikipedia.org/wiki/The_Shambles
37. Unity discussions, State of determinism in Unity — https://discussions.unity.com/t/state-of-determinism-in-unity/867770
38. Unity manual, Burst float precision and determinism — https://docs.unity3d.com/Manual/burst/float-precision-determinism.html
39. Arm, consistent floating-point across x86 and Arm — https://learn.arm.com/learning-paths/cross-platform/floating-point-behavior/how-to-4
