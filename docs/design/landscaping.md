# Landscaping: more bushes, more trees, one tool that plucks them (#724, research, 2026-11-14)

Asked by the user 2026-11-14. Research only: **nothing here is built or decided** until the user picks (§7).
Related: #663 (free bushes, same bush question), #330 (what the tweezers tend in a forest, keepers-hands §5.7),
#675 (seed wording), #709 (the bird in the bushes). Hours are [est] for one worker including tests, not the Editor pass.

## 1. The ask
1. More berry bushes and thickets than the one thicket today.
2. More trees. The first stays the Keeper's tree (hand-built, tended). Every further tree is a normal tree seedling.
3. One new tool that plucks trees and bushes. The tweezers stop plucking seedlings and saplings; the new tool does all of
   it, so the Keeper landscapes the box as they like.

## 2. What is true today (from the code)
| Thing | Rule today | Where |
|---|---|---|
| Thicket | Exactly one, placed once, never reopens. A disc (`thicketRadius` 0.45 u, ragged edge) fills from 3 founders to `thicketBushes` 22, a bush a season (+ a few when misted, #605). None grow outside it. It chokes tree seedlings (`thicketChoke` 0.6). The rabbits keep out of it, and the settlement planner treats it like water. | `EcoModel.Jar.cs` `Place`, `EcoModel.Thickets.cs`, `LifeJar.Restocks` |
| Keeper's tree | One seed, one tree (`st.treeCell`, `Plant` refuses a second). | `EcoModel.cs` `Plant` |
| Other trees | Wild only: birds drop seeds, Seedling → Young → Tree on per-cell hash dice. The mister helps growth (`watered`). | `EcoModel.cs` (`treeRate`, `seedlingRate`, `seedsPerBird`, `wildSeedChance`) |
| Pluck | Tweezers take a wild Seedling/Young cell (`CanPluckSapling`), never the Keeper's tree, a grown tree, or a forest heart (3+ grown trees beside). Nothing plucks a bush or a grown tree. | `EcoModel.Overgrowth.cs` |
| Representation | Forest trees and bushes are one cell of `EcoCover` each, baked into the ecology mesh. They are not objects, so anything per tree needs a pick rule. | `BoxView.Eco.cs` |
| The village gate | Camp started + 20 pond cells + **10 grown trees** + ducks placed + fish placed. | `EcoModel.cs` `Gates` |

The decisions this touches: one thicket ("exactly one, ever", placed-life §5.1, 2026-11-08) and one seed (keepers-hands
§3, 2026-11-02, "the forest sprouts from your tree").

## 3. Bushes: options
| | A1. A bigger single thicket | A2. A few thickets (2-3) | A3. Free bushes (#663) |
|---|---|---|---|
| What | Raise the disc and `thicketBushes` (22 now), or a second founding group into the same thicket | The jar reopens (per count or per season) and the Keeper places another disc | A bush is a kit item like the seed: tap to plant one, no limit |
| Decision | Keeps "one thicket" | Reverses it | Reverses it fully |
| Sim | **S**, 1-2 h: tuning + `EcoBushCountTests` | **M**, 8-12 h: `thicketA/B` existed before #418 (so the shape is known); a list of discs in `EcoState` (save), per-disc cap, choke, rabbit reach, planner reserve, rabbit jar | **L**, 16-24 h: thicket rules become per bush; planner reserve per bush or per cluster; `InThicketReach` per bush; berries per bush |
| View | none (bush mesh already per cell) | **S-M**: ghosts for a second disc; the placing hint | **M**: a plant ghost per bush, the pluck pop |
| Food balance | Cap rises: berries rise with it | Berries scale with thickets: needs a cap on total | **Needs crowding** (a bush grows and fruits less with bushes near it) or a dense patch is an exploit |
| Risk | Low | A second thicket next to the camp can crowd the people's lots | Largest: the people, the rabbits and the bird (#709) all read "the thicket" |

## 4. Trees: options, with a measurement
Extra seedlings the Keeper could place (option B2 at its most generous: all placed on day one, the month the seed is
planted, on open ground round the Keeper's tree; normal wild seedlings after that, same mist). Measured on the real sim with
the #569 lab (Active Keeper, game year, seeds 7 / 21 / 42), `Tools/SimTests/LandscapeOptionsLab.cs`
(`dotnet test Tools/SimTests --filter LandscapeOptionsLab`, explicit).

| Extra seedlings | 10 grown trees reached (year) | Village (year) | Trees at the end of the run |
|---|---|---|---|
| 0 (today) | 23.3 / 23.3 / 23.3 | 23.3 / 23.3 / 23.3 | 185 / 176 / 178 |
| 5 | 8.5 / 9.3 / 7.5 | 23.0 / 23.0 / 14.8 | 267 / 279 / 296 |
| 10 | 6.8 / 5.5 / 4.3 | 23.0 / 23.0 / 14.8 | 304 / 309 / 318 |
| 20 | 3.5 / 4.5 / 2.8 | 23.0 / 23.0 / 14.8 | 335 / 343 / 353 |

What it says:
- **Trees are the slow gate today** (23.3 for all three boxes). Five placed seedlings bring the tree gate to year 8-9.
- **The village barely moves**, because the **fish** then wait: fish need the ducks placed, `jarFishAfterDucks` months after,
  and reeds. That holds two boxes at 23.0; one box (seed 42) is freed to 14.8. So free tree placing does not wreck the
  year-25 pacing target by itself: the fish and the pond gate it. It does **double the wood** by the end (about 180 → 350
  trees), which changes the woodcutters, the felling story (#366) and the box's look.
- Not measured: placed seedlings that get extra care, or placed trees in meadow-less sand. The numbers are an upper bound for
  "plant and walk away".

| | B1. A few more seeds | B2. An unlimited seedling item | B3. Seeds earned |
|---|---|---|---|
| What | After the Keeper's tree is grown, the seed jar gives N seeds; each plants a normal seedling | Place seedlings anywhere valid, any count | A grown tree gives a seed now and then (a pouch counter) |
| Decision | Reverses "one seed" mildly | Reverses it | Keeps "the forest sprouts from your tree": the Keeper's tree gives the seeds |
| Sim | **S-M**, 3-5 h: `Plant` drops the one-tree rule for non-Keeper cells, a seed count in `EcoState` + save | **M**, 5-8 h: as B1 without the count, plus valid ground | **M**, 5-8 h: a seed clock, save |
| Pacing | Safe: five seeds is the sweet spot of the table | The village stays ~23 years (the fish gate), wood doubles | In between, depends on the clock |
| View | **S**: the seed's ghost exists (#518), allow it again | same | the pouch counter in the kit |

Open: do placed seedlings get extra care? (A simple answer: no, they are wild from the first day.) Do they count for the
deer jar (`jarDeerTrees`) and the village (`villageTrees`)? As written they do, since they are normal trees.

## 5. The landscaping tool
- **What it plucks:** seedlings, young trees (today's pluck, moved off the tweezers), **bushes**, and grown forest trees.
  Never the Keeper's tree (the user: a tended bonsai). Grown trees are the real question: keepers-hands §5.7 worried that
  plucking trees reads as "the Keeper fells trees"; the user now wants to landscape freely, so this is likely wanted. If
  a grown tree goes, the woodcutters, the camp's wood and the kids' tree (#404) must not break (see people-and-things.md).
- **What the tweezers keep:** weeds round the Keeper's tree, the creeping vine, later the field weeds. `CanPluckSapling`
  and `PluckSapling` move to the new tool; `EcoTweezersTests` and `TweezersPlayTests` are updated.
- **Name and look:** the user names it (they named the tweezers, 2026-11-04). Candidates: trowel, shears, spade.
- **Picking on a phone:** forest trees and bushes are cells in a mesh. Use nearest-cell pick within a reach (as
  `NearestSapling` does) plus a hover ghost on the target before the lift, and a drag-along sweep for a row of seedlings.
- **What follows a pluck:** the ground goes to meadow if damp, else bare (today's rule). A plucked thicket bush: it
  regrows as the thicket refills to its cap, so the tool's meaning is "make room": a freed cell stays clear for a season
  or two (a short `cleared` memory per cell in `EcoState`), or the thicket's cap falls by one per pluck (it can be
  re-filled by placing). A plucked tree with a pair of rabbits, deer or the berry kid near it: the animals walk on.
  The history line and the book get one line per day ("cleared a corner").
- **No undo.** Placed life has none today. If the pluck can be wrong, a free replant (A3/B2) is the undo.
- **Debug panel (CLAUDE.md rule):** give seedlings, place bushes, "pluck all in the area", "cleared corner" state.

| Part | Sim | View | UI |
|---|---|---|---|
| Move the seedling pluck to a new tool | **S** 2-3 h | **S** (icon, the pop exists) | **S-M** 3-4 h (a new tool in the rail, the tweezers' hint) |
| Pluck bushes | **S-M** 3-5 h (`CanPluckBush`, cap rule) | none | none |
| Pluck grown trees | **M** 4-6 h (a heart rule, wood, woodcutters, #366) | **S-M** 3-5 h (a fall/pop, mesh rebuild) | none |
| Tests | **M** 4-6 h + a tester pass | | |

## 6. Recommendation (the user's call)
1. **Trees: B1 now, grow into B3.** Give a few seeds (5) once the Keeper's tree is grown, and add the earned seed if it
   is played. It gives the freedom asked for with the safest pacing (the table: 5 seeds bring the tree gate to year 8-9 and
   leave the village where the fish say), and it keeps "the forest sprouts from your tree" alive. Unlimited (B2) works
   for pacing but doubles the wood by the end: do it if the user wants a free canvas, and put the cap in the wood.
2. **Bushes: A1 first (a bigger thicket, 1-2 h), then A2 (more thickets) if wanted.** Both keep the berry food and the
   people's lots under control. A3 (free bushes with crowding) is the free canvas the user may want, but it is the largest
   change (16-24 h) and rewrites the thicket's role in the rabbit jar, the planner and the bird.
3. **Tool: one tool, name by the user, plucks seedlings, young trees, bushes and grown forest trees (not the Keeper's
   tree).** Do it with the trees: the free canvas needs both hands. About 14-22 h in total.
4. **Order:** tool (move the seedling pluck + bushes) → B1 → A1 → grown-tree pluck (the people's rules) → A2 / B3 / A3 only after play.

## 7. Questions only the user answers
1. **Bushes:** A1, A2 or A3? (A3 keeps #663 open; A1/A2 close it with an answer.)
2. **Trees:** B1, B2 or B3, and how many seeds?
3. **Tool:** may it pluck **grown** trees? What is its name? Does a plucked thicket bush stay gone for a while?
4. **Phase:** this is new feature work in the Polish phase ("no new features"): now, or after Polish?
5. When picked: update `decisions.md` (one thicket, one seed) and `placed-life.md` §5.1, `keepers-hands.md` §3.3 and §5.7.

## 8. Picked (the user, 2026-11-14): B3 trees, A3 bushes, one tool
> "B3 for trees, you earn as the civ grows. A3, but you gain them as trees, but the first bush tries to grow a full thicket,
> if you do more the thicket are gradually smaller (even shrinking when player is placing a lot)."

Recorded in `decisions.md` (2026-11-14). The shape below is a proposal built from that sentence; numbers are [est] and are
tuned with `EcoBushCountTests` and the pacing lab. Still open: the tool's name, whether it plucks grown trees, the phase (§7).

### 8.1 Earning (trees and bushes)
- Two counters on the Keeper: **seeds** (each plants one normal tree seedling) and **bushes** (each plants one bush). Both are
  earned from the same civ steps, so they feel like one thing: the civ grows, the Keeper is given more to plant.
- **Proposal for the steps:** one of each at every new era, and one more of each for every +5 people at their peak (so a
  growing village pays out steadily and a shrinking one pays nothing). A stock of 3 at most waits in the pouch.
- The first seed and the first bush stay as now: the seed is in the life jar from the start; the first bush is the first
  thicket (§8.2), placed from the bushes jar once the seed is in the ground.
- A placed seedling is a normal wild seedling from the first day (no extra care), so the pacing in §4 is an upper bound.
- UI: the life row shows a count on the seed and the bush; the quick-placing ghost and the "tap again" of #518 are reused.
- Debug panel: give seeds, give bushes, "next civ step".

### 8.2 Thickets that shrink as you place more
- The **first bush placed** founds a thicket that tries to grow to the **full size** (today: 22 bushes, `thicketRadius` 0.45 u),
  as #418 does now.
- **Every further bush the Keeper places** founds its own small thicket round that spot (min 3 bushes), or, if it lands
  inside an existing thicket's disc, is refused with a reason ("already a thicket here"), as #664's refused tap does.
- **Crowding rule** (the user's sentence): with `n` thickets, each thicket's target is `full / sqrt(n)` bushes (n=1: 22,
  n=2: 15, n=4: 11, n=9: 7), and its disc radius follows (`radius x n^-0.25`). So the total berries rise slowly (22 -> 44 -> 66),
  never with the count, and a heavy planter gets many small thickets, not a farm. A thicket never goes below its founders (3).
- **Shrinking:** a new placing lowers every thicket's target at once. A thicket above its target loses **one bush a season**
  (the weakest, the one farthest from its centre, never one the people are picking that visit), until it fits. It never
  drops by a lump, and the berries it held are not lost: the gatherers carry them as now. Placing a lot shrinks the whole
  box gently over a few seasons; stopping lets the thickets settle.
- A **maximum of 12 thickets** and a minimum gap (the discs may not overlap) keep the settlement planner's reserved area sane:
  each disc stays "water" to the planner, so the people's lots keep out of every one.
- The rabbits' ground, the bird (#709), the berry kid and the drying mat read "the thickets" (a list) instead of "the thicket".
- Save: `st.thicketA/B/Radius` become a list of discs (a v1 save loads as one full thicket).

### 8.3 The tool (still to be named)
One landscaping tool plucks seedlings, young trees and bushes (the tap lifts it out with the pop), never the Keeper's tree.
A bush pluck lowers that thicket's count and, because the crowding rule is by thicket count, **plucking a whole thicket's
bushes away frees room for the others to grow back** to their larger target. Whether it also lifts **grown** trees stays the
user's call (§7). The tweezers keep weeds and the vine.

### 8.4 Issues to build (small, one agent each)
1. Seeds earned as the civ grows: the counters, the steps, the pouch UI (lane sim, then view).
2. More trees: `Plant` takes any seed from the pouch as a normal seedling; tests and the pacing lab (lane sim).
3. Thickets as a list: the data, the save migration, the planner reserve, the rabbit ground (lane sim).
4. Crowding and shrinking: targets, one bush a season, tests with `EcoBushCountTests` (lane sim; after 3).
5. The landscaping tool: bush pluck, move the seedling pluck off the tweezers, rail icon (needs the tool's name).
6. Bushes earned and placed one at a time: ghost, refusal reasons, counter (view + ui, after 3).
7. Debug panel and the tester pass over all of it.
