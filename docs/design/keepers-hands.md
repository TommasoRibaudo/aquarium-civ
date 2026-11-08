# The Keeper's hands: tending a box that starts barren (draft, 2026-11-02)

Status: **draft for the user's confirmation.** It turns the user's direction of 2026-11-02 into a design. It builds on
[research 15](../research/15-barren-to-living-box.md) (barren → living, the real-ecology rules, the Ecology Lab spike
#228) and replaces, **if approved**, parts of [game-definition.md](game-definition.md) §5 (care), §6 (control) and §8
(eras), [research 10](../research/10-shaping-the-land.md) (the toolset), the HUD's Clean and Shape buttons
([ui/spec.md](ui/spec.md), [one-handed-controls.md](one-handed-controls.md)) and the first run
([first-session-and-check-in.md](first-session-and-check-in.md) §2). Nothing changes in code until it's approved and
the spike is back. **[est]** = a guess to tune.

User's direction, in their words:
- *"Rethink these powers as the keeper's interactions (they spray water, it does not rain), without showing too much."*
- The arc: *"people on barren land → rain → meadows and bunny → camp phase officially starts → bushes are born and
  people can gather → you plant the one seed and care for it → the forest grows and your people can cut wood and
  gather → you keep spraying the tank (you can even clean the droplets on the tank glass) → water level grows until
  it shows in the lowlands → village → you dig a canal and water fills up for the mill and fertility boost →
  continues to town."*
- *"The entire care system should start from these interactions (weeds in fertile farmland, outgrowth of the forest,
  keeping track of the humidity and water level)."*
- Tree care: water, light, weeding and the people learning, **plus seasons**. **No nibbling, no storms.**

---

## 1. The idea in one line

**The Keeper is a terrarium keeper, not a weather god.** Everything the player does is something a real person does
to a real terrarium: mist it, wipe the glass, plant a seed, snip what overgrows, dig with a stick. Inside the box, the
people see the effects as weather and wonders. The player sees a kit of hand tools. That makes **care and shaping
one activity**: tending.

**"Without showing too much"**, read as: no hand, no bottle, no giant tool models in view. The player sees only
the *effect*: a fine mist drifting down, droplets beading on the glass, a seed dropping, the soil parting under a
line. The people never see a Keeper either: the mist is "the sky weeping", the seed "fell from above" (this fits the
glass-voice stages: a little scared in the camp, curious in the village, studying in the town).

---

## 2. The arc, beat by beat

| # | Beat | What the player does | What grows | The people | Era |
|---|---|---|---|---|---|
| 0 | **Barren land** | Wipe the misted glass (first run) | Dry, cracked dirt; hollows; a dry stream bed | **The tribe is already there**: one tent and a fire, nothing more. Slow life: they dig roots in the dry stream bed, sit and sleep by the fire, watch the sky (§2.1) | **Tribe** (before the camp) |
| 1 | **The first mist** | **Mist** a spot | Green spreads from where it fell, over seconds; crickets start with the first green | They react **at once**: they stand in it, faces up, astonished; a child plays in the mist; ~~they move their fire next to the green~~ (dropped, user 2026-11-03); the glass voice's first line. Every early mist is a small moment (§2.1) | Tribe |
| 2 | **Meadows and bunnies** ~~(arrive)~~ → **placed**, see [placed-life.md](placed-life.md) | Keep misting, here and there | Meadow spreads; rabbit tracks first, then **rabbits** arrive with enough grass | They hunt; hides become **new tents** (none are built before this) | **Camp starts here, officially** (an era moment, small) |
| 3 | **Bushes** ~~(appear)~~ → **one placed thicket**, see [placed-life.md](placed-life.md) | (nothing new) | Bushes appear in the meadow over time | **Gatherers** (berries) | Camp |
| 4 | **The one seed** | **Plant** the seed (once); care for it (§4) | Seed → sprout → sapling → young → grown, through seasons | Watch it; later they carry water to it | Camp |
| 5 | **The forest** | (nothing new; keep tending) | Birds spread its seeds; wild trees grow without care | **Woodcutters**, more gathering (mushrooms, nuts) | Camp |
| 6 | **The water rises** (ducks and fish now **placed**, see [placed-life.md](placed-life.md)) | **Keep misting** the box; wipe the droplets that bead on the glass | The **water table** rises, visible in the soil cross-section; it **breaks the surface in the lowlands** as a pond, then reeds, frogs, ducks | **Fishers**; nobody ever built in the lowlands (§5.3) | Camp → ready |
| 7 | **Village** | Watch the era moment | Fields on the moist flats | Farmers, huts of wood | **Village** |
| 8 | **The canal** | **Dig** a line from the pond with the stick | Water fills it; the land along it greens and grows richer | A **mill** on the canal; fields along it do better | Village |
| 9 | **On to the town** | Tend: weeds in fields, the forest's edge, the humidity, the water | The land matures | Crafts | Town (its tools: §3.3) |

What gates each era [est, the spike measures it]: the **camp** starts with meadow ≥ ~15 % and the first rabbits;
the **village** needs the forest (≥ N grown trees) **and** water at the surface, plus today's population and
building rules (`SimConfig.villageAt`). Natural moisture keeps all of this moving slowly without the player (§5.4), so
an era always comes eventually, only later.

> **Superseded in part (2026-11-08):** beats 2, 3 and 6 and the camp gate below are replaced by
> [placed-life.md](placed-life.md): the thicket, rabbits, deer, ducks, fish and wolves are placed by the Keeper.

### 2.1 Before the camp: the tribe (decided 2026-11-02)
The user's answers after the Ecology Lab (#228), which showed that with care the camp gate opens about 3–4 game
years in (3–4 real hours), and 18–30 without the player:
- **The tribe phase is the start.** The people are there from the first frame with **one tent and a fire**, and
  they build **no new tent until the camp starts**. So the camp gate (meadow + rabbits) is the moment they can
  build, not the moment they arrive. **No time skip** in the first run: the tribe's hours are played, not skipped.
- **Slow life before the first green.** They scrape by: dig roots in the dry stream bed, carry them back, sit and
  sleep by the fire, look up at the glass. Quiet and slow, but never still.
- **The first mist is an event for them, at once.** They stand in it, faces up; a child plays in the drifting mist;
  ~~they move their fire next to the first green~~ (dropped by the user, 2026-11-03: the fire stays put); the glass voice says its first line ("the sky wept"). Every early
  mist is a small moment of its own, so the player is answered before any gate.
- **The land shows progress between the gates**, so a return always finds something new: crickets with the first
  green, rabbit tracks before the rabbits, birds landing on the first bushes, later frogs at the pond.
- **The barren box has its own sound, and the green takes it over.** Dry wind, sand hissing along the ground,
  the tick of cracking dirt, a loose stone. Its volume follows the share of bare land, so each mist, each meadow and
  each tree makes it a little quieter, while the living sounds (crickets, then birds, then water and frogs) rise in
  its place. Nobody needs a bar: the box sounds more alive as it is. Pillar 4 holds: nothing ever brings the barren
  sound back, because the land never dies back.
- Debug panel: cheats to skip to each gate and to trigger each of these signs.

### 2.2 The pace of green: a little at once, the wet area over time (decided 2026-11-03; #266 sim half, #271)
The mist no longer greens its whole core at once, and green no longer waits for the next visit. What the sim does now
(`Sim/Eco`; numbers in `EcoTuning`, all [est]):
- **Every mist shows a little green at once**, not only the first: the middle of the mist turns to grass now, and if
  that is bare, at least 3 cells do (`mistMinGreen`). A few shoots come up round them (bare cells with a `sprout`
  value; the view draws them as shoots).
- **The mist is bigger and grows with the hold:** radius 0.45 to 0.7 for a short mist, up to 1.0 at the full hold
  (4 s, `EcoMistRadius(seconds)`). The wetness it leaves is per cell (`EcoWetAt`, 0..1): a **wet area** that dries
  by itself (about a fifth to a third a game month, slower in humid air), so it is nearly gone within a game year.
- **The wet area greens over the following months:** shoots in the inner part of the wet area (the inner ~40 % of the
  radius) grow into grass in about 6 game months (30 real minutes at 1 h = 1 game year). The rim only wets the
  ground; its moisture still greens it slowly.
- **Puddles:** where a mist is long enough to soak flat ground (amount x falloff over 0.85, so a held mist's middle),
  a puddle stands (`EcoPuddleAt`, `EcoPuddleCells`). It dries and sinks in within about a game year. A puddle of 0.25
  or more can be drunk from: **the water source while there is no pond** (`EcoNearestPuddle`, `EcoDrawPuddle`; #267
  wires the planner).
- **Spreading on its own (#271):** a misted patch creeps outward between visits (a green edge nudges a bare neighbour
  on, if the ground has a little damp), and bare ground gets the odd sprout after dew or a shower. It is slow: three
  unvisited years add a few dozen cells. **The mister stays the fast way**, many times faster.
- **Pace kept:** the camp gate wants 20 % meadow (was 15 %) plus rabbits, and the plain bare-ground grass chance fell
  (`grassRate` 0.5 to 0.1) while the creep took its place. Seed 7: camp with care Y3.3-3.8 (lab Y4.3; the issue's
  range is 3-5.4), no player Y20.3 (lab Y25.3; range 18-30), village with care Y24.5 (lab Y25.3).
- Debug: "mist big here" (`SimCore.DebugMistBig`) and "puddles now" (`SimCore.DebugPuddlesNow`).

---

## 3. The Keeper's kit (replaces Clean and Shape)

### 3.1 The tools
| Tool | Gesture (one finger; two fingers = camera) | What it does | From |
|---|---|---|---|
| **Cloth** | Drag on the glass | Wipes grime **and droplets** (condensation) off the glass | Start (today's wipe) |
| **Mister** | Hold over a spot; the mist drifts down where you hold. Longer = wetter | Raises the spot's moisture (green follows) and **the box's humidity**; over many mistings, **the water table** | Beat 1 |
| **Seed** | Tap a spot | Plants the one seed. The slot empties after (one per box; later seeds: §3.3) | Beat 4 |
| **Tweezers** (the snips; the name is the user's, 2026-11-04) | Tap, or drag across, what overgrows | Plucks the **weeds round the young tree**, the **forest's edge** (wild seedlings and saplings creeping over paths, yards and the meadow) and the **creeping vine** (§5.5); later **weeds in the fields**. The pluck lifts it out with a small pop. Never the Keeper's tree, a grown tree or the heart of a wood (§8). The cloth keeps the glass only (#311) | Beat 4 (the tree's weeds) |
| **Stick** | Drag a line from water | Digs a **canal**; water fills it and follows the slope. Push/pull to make a hollow or a bank (today's hill & hollow) | Village |

### 3.2 What happens to today's tools
| Today | Becomes | Why |
|---|---|---|
| Rain (cloud) | **Mister** | The user: spray, not rain |
| Grove | **Seed** (one, cared for) | Decided 2026-11-02 |
| Herd (flick in animals) | ~~**Gone**: animals come by themselves when the land can hold them~~ **Replaced 2026-11-07/08:** the **life slot** places one founding group per species once the land is ready ([placed-life.md](placed-life.md)) | The user wants the arrival of life to be the Keeper's doing |
| Channel | **Stick** (dig a canal) | A physical act |
| Hill & hollow | **Stick** (push or pull) | Same tool, second gesture |
| Keeper's Lamp | **Gone as a tool** (§8.4): the lamp-lit square is the people's own | Not tending |
| Outcrop | **Gone** (§8.4): giving resources isn't tending | |
| Spring | **Gone**: the water table makes springs where it meets a hillside | Ecology |
| Dye meadow | **Seed pouch** on meadow → wildflowers (§8.5) | One tool, the land picks |
| Decorate | **Ornaments**, as today | Already physical |
| Wishes | Stay: a notable wishes *"for the sky to weep on the east field"*, or for the forest edge to be kept back; Give picks the tool and the spot | |

### 3.3 The button ★
Today's HUD has **History, Clean, Shape** (ui/spec.md §2, signed off in #107). Proposal:

- **★ One "kit" button** where Clean and Shape were. Tap it and a short rail of the unlocked tools fans out in the
  thumb zone (cloth, mister, tweezers, and later seed, stick). Tap a tool and it's in hand until you put it down (as
  clean mode today). The cloth comes first, so a glance is still one tap away from a wipe. Two buttons become one.
- Alternative: **two buttons**, *Clean* (cloth, tweezers: care) and *Tend* (mister, seed, stick: growth). It keeps
  today's muscle memory, but the split no longer matches the design, since everything is care now.
- Rules kept: one gesture per tool (#145), one finger = the tool and two = the camera, ≥ 48 dp targets, no timers or
  counts on the tools.
- **Limits:** no charges. The mister and tweezers are free; the land's own pace is the limit (moisture saturates, then
  more misting only fogs the glass, §5.1). The seed is one per box until the forest stands. The stick's canals have
  a natural cap (the pond's water). That removes the charges economy (`SimConfig.shaping*`), which care fed.

This reworks the signed-off HUD, the tray (`ShapingTray`), the shaping view, one-handed controls and the first-run
reveal of the buttons. That's real work, and it needs your sign-off.

---

## 4. Caring for the one tree

The user's picks: **water, light, weeding, the people learning, seasons.** One need shows at a time, in the tree's
look. The tree never dies; neglected, it wilts a little and pauses.

| Need | You see | You do |
|---|---|---|
| **Water** | Leaves droop and pale; the soil ring cracks | **Mist** it |
| **Light** | It leans toward the glass; growth slows under grime or droplets | **Wipe** the glass above it |
| **Weeding** | Grass and weeds crowd its foot | **Snip** a ring round it |
| **Seasons** | Bare in winter (no needs show); buds and a growth spurt in spring; full in summer; colour in autumn | Spring is when care counts most [est] |
| **The people learn** | After you've misted it a few times, a child carries water to it in a skin; later someone rings it with sticks | Nothing: they share the care, and it's a story |

Growth takes about a few game years with care and 2–3 times longer without [est, spike]. When it's grown: it
blossoms, drops its first seeds, birds take them, and the forest begins. The tree stays the people's oldest tree,
their gathering place, a book page, and in the town it's the old tree in the square.

---

## 5. Care, rebuilt on the land

The care loop no longer has *mess types*. It has **the box's state**, which the player tends with the kit. All of it
is capped, and none of it kills or takes away stock (pillar 4).

### 5.1 Humidity: shown on the glass ★
The box has one **humidity** value (the air), fed by misting, the water surface and plants, and lost slowly to the room.
**It's shown on the glass, not in a bar:**
- **Too dry:** the soil cracks, plants go pale and grey-green, dust in the light, growth pauses. → Mist.
- **About right:** a light bloom of condensation at the glass's bottom edge in the morning that clears by day.
- **Too humid:** droplets bead and run down the glass, fog in the corners, moss on the stones. The land is
  fine, but you can't see in well and the light drops a little. → Wipe the droplets (that clears it a bit; the humidity
  settles back over hours).

That's exactly a real terrarium's language (condensation tells you the humidity), and it keeps pillar 5 (the glass is
the interface). The glass grime from time stays as today; droplets are the second thing the cloth clears.

### 5.2 The water table: shown in the soil cross-section ★
Misting over time raises a slow **water table**. The soil cross-section behind the glass (already in the style kit)
shows it as a darker wet band rising through the layers. When it reaches the surface in the lowest hollows, they
become ponds, and the pond grows with it. Through the town era, it feeds the canals and the mill. It falls slowly in
summer and as the fields and the people drink [est]. **It never drops below what the box already has**, so an
established pond never vanishes from neglect (pillar 4).

### 5.3 The lowlands rule
The people never build in a hollow below its spill line (research 15 §6.3). So when the water rises, it fills land
nobody built on, and the rule reads as their wisdom ("the old ones said the low ground is for the water").

### 5.4 Absence
While you're away: natural moisture (a little dew, a rare passing shower the people remember) keeps growth crawling,
humidity drifts back toward a middling value, the water table holds. **Nothing dies back.** The land just grows
slower without you, and grime and droplets cap as mess does today (48 h).

### 5.5 What overgrows (the tweezers)
| Thing | Where it comes from | You see | If left | Era |
|---|---|---|---|---|
| **Weeds round the young tree** | The meadow | Tall grass at its foot | Its growth slows | Camp |
| **The forest's edge** | Birds keep spreading seeds | Saplings creeping into paths, yards and the meadow | It takes the meadow and pinches paths; the people work round it (never a building lost) | Camp → |
| **Weeds in the fields** | Fertile, watered farmland | Weeds among the crops | The harvest pace drops a little (today's −25 % max, capped) | Village → |
| **Vines on roofs** | The forest nearby | As today's overgrowth | As today | Village → |
| **The creeping vine** (#314) | Damp shade at the forest's edge: it starts only beside young trees on wet ground, from the camp on | A dark, bluish strand of small leaves along the ground, in a line; slower than the weeds | It grows to a cap and counts as overgrowth (the mess and the sunlight), never blocks a building | Camp → |

**Care moves up** (game-definition §5 rule 5): in later eras the people weed their own fields and keep their own
forest edge (gardeners, woodcutters), and you see them doing it. The glass and the water stay yours.

### 5.6 Trash: removed (user, 2026-11-02)
The trash heaps go. The land gives enough to tend, and the people's own mark on the land comes back in the town as
felling, smoke and fouled water (§8), which belong to the land model instead of being litter.

### 5.7 Open: what the tweezers tend in a forest (#330, discussion 2026-11-04, no build until you pick)
**The question.** #311 made the tweezers pluck wild seedlings and saplings at the forest's edge, never a grown tree and
never a "heart" (a cell with 3+ grown trees beside it, `EcoTuning.heartTrees`). You asked why the Keeper would remove
trees at all, and pictured the trees as **bonsai**: the Keeper takes back the tall shoots that search for the top of
the box, and the tree stays.

**How it works today (from the code).**
- `EcoModel.CanPluckSapling(c)`: the cell is a seedling or a young tree, is not the Keeper's tree, and is not in a heart.
  `NearestSapling` finds one within `SaplingReach` (0.16 box units) of the finger; the pluck leaves grass (damp) or bare ground.
- Forest trees are not objects. Each grown tree is one cell of cover, drawn into the ecology mesh in `BoxView.Eco.cs`
  (`Pieces.Tree`, seeded by the cell). The Keeper's tree is the one hand-built model (`KeeperTreeKit`, stages 0-4).
- There is no growth state per tree beyond its stage, and nothing a finger can pick on a crown.
- A sapling pluck is a **removal**. It only takes a weed-sized tree, but it can read as the Keeper felling, which sits
  badly with pillar 3 and with §5.4's "nothing dies back".

**What not pruning must look like, in every option that adds shoots.** It never punishes and no tree dies (§5.4,
`game-design-practices.md` §9). A shoot left alone does soft things, never a number or a warning: (1) its tip presses
on the glass and leaves a **leaf print** that the cloth wipes off; (2) the crown grows **lopsided**, leaning toward the
glass (the tree already leans for light, §4); (3) a long limb **shades a yard** a little, and the people work round
it. Each caps at a mild value after a few game years, and shoots stop growing while you are away (like grime at 48 h).

**The three options.**

| | A. Keep the pluck as is | B. Pluck only in the people's space | C. Bonsai pruning (shoots) |
|---|---|---|---|
| **What the tweezers tend** | Weeds, the vine and edge saplings, anywhere outside a heart | Weeds, the vine, and saplings **on paths and in yards** only; a sapling in the meadow or the wood is left to grow | Weeds and the vine as now, plus **reach shoots** on trees. C1: the Keeper's tree only, 1-3 shoots. C2: later, forest trees too. The sapling pluck is dropped (C alone) or limited as in B (C with B) |
| **Reads as** | Weeding the meadow's edge | Keeping the paths open: the people's space is yours to clear, the wood's is not | Shaping a bonsai: the tree stays and keeps its shape; nothing leaves |
| **Pros** | Built and tested, no new art. Already answers "the forest eats the meadow" | Small edit. Answers your doubt for the wood and keeps the pluck where the people need it. Removes the "Keeper fells trees" reading in the forest | Matches your picture. Nothing is removed. The first real use of the tweezers on the tree you care for; a visible, charming act, and a reason to look up |
| **Cons** | Still a removal. No answer to "trees reaching for the glass" | The rule needs a hint (pluck on a path, not beside it). Nothing holds the forest back from the meadow, so the meadow slowly loses ground (a land story, not a loss card, but a change) | New sim state, a new mesh part and a pick target. C2 is the expensive half. Dropping the pluck (C alone) has the same meadow cost as B |
| **Sim** | none | **S**, about 1-2 h: `CanPluckSapling` gains "cell is path or yard" (those layers exist); `NearestSapling` follows; update `EcoTweezersTests` | C1 **M**, about 4-6 h: a `reach` count (0-3) and length on the Keeper's tree, grown on a clock in its own seeded stream (not the main stream), capped, paused in absence; `PluckShoot`; the soft effects (print age on the pane, a lean value, a shade flag for the cell the limb covers). C2 **L**, about 10-14 h more: a sparse per-tree shoot map (a byte per grown tree), save format, the shade rule on yards |
| **View** | none | none (the pop exists) | C1 **M**, about 4-6 h: shoot meshes in `KeeperTreeKit.Grown` (a thin limb with a leaf cluster, length by growth, `mb.sway`), rebuilt when the count changes; the shoot tip as a pick piece like the weed patches that `PickUnder` already finds; a leaf-print on the near pane (reuse the grime path), wiped by the cloth; a snip pop. C2 **L**, about 12-16 h: wood trees are baked cell by cell into one mesh, so shoots need per-tree pieces (instanced or a second mesh layer), and picking among many small shoots on a phone needs a wider reach or a drag-along snip |
| **Tests** | none | S: the path/yard rule; a meadow sapling stays | M: growth, cap, absence pause, snip, seed determinism, save round trip, print and cloth, pick by touch (PlayMode), a tester pass. C2 adds the dense-wood case |
| **Size** | **S** (nothing) | **S** (about 2-3 h) | **C1: M** (about 10-14 h with tests and a tester pass); **C2: L** on top (about 25-30 h); C with B is C1 plus 2-3 h |
| **Debug panel** | as is | as is | "grow shoots on the tree", "shoots to full", "leaf print now" (CLAUDE.md rule) |
| **Risk** | The complaint stands | Low | The Keeper's tree is one tree in one place, so its shoots are easy to find. In the wood they are small and many: a chore in what must stay a toy (practices §9). Keep C2 off until C1 has been played |

Hours are [est] for one worker including tests, not the Editor verification pass.

**Recommendation (yours to accept or not): B now, then C1.**
1. **B now** (about 2-3 h). The cheapest honest answer: the Keeper clears the people's paths and yards and leaves
   the wood alone. A forest left to itself is the land's business, and later the people (woodcutters, §8.3) manage it.
2. **Then C1**: bonsai shoots on the Keeper's tree only, 1-3 of them, with the soft consequences above (leaf print,
   lopsided crown, a little shade). The snip makes the tree neater and removes nothing. It gives the tweezers a
   reason to touch the tree you love, which is the part of your idea that adds something new.
3. **Decide C2 after C1 has been played.** If one tree's shoots are fun, extend them to a few old forest trees at a
   time (a reaching limb now and then), not to every tree. If they are a chore, leave them on the Keeper's tree.

Why not A: it keeps the one gesture that reads as felling. Why not C alone: with no pluck at all the forest's spread
has no hand against it, which is a quiet, permanent change to the meadow that you did not choose.

**Question for you.** Do you want **B now, then C1** (shoots on the Keeper's tree)? Or something else: A, B alone, C1
alone (accepting that the forest takes meadow), or C2 as well? And is a leaf print on the pane, a lopsided crown and a
little shade in a yard enough for "if you don't prune", or should one of them go?

---

## 6. What it changes, and the order

| Area | Change | Size [est] |
|---|---|---|
| Sim | Ecology layers (soil, moisture, cover), humidity, water table, basins, the one tree, the forest's spread, the gates (all from the spike #228) | Large |
| Sim | Care re-sourced: weeds, forest edge, droplets; the charges economy removed | Medium |
| Sim | Era gates read the land; the pre-camp wanderers | Medium |
| View | Terrain tint by cover, vegetation growth stages, rabbits and birds, the water table in the cross-section, the rising pond, droplets on the glass, mist | Large (art) |
| UI | The kit button and rail replace Clean and Shape (§3.3) | Medium |
| First run | Wipe → barren land → the first mist → green → the wanderers astonished → name the box | Medium |
| Docs | game-definition §4–§8, research 10, ui/spec, one-handed-controls, first-session | Small |

**Order ★:** (1) the Ecology Lab spike #228, now testing *this* model (misting, humidity, the water table, the tree's
care with seasons), shows whether the arc reads and the times are right; (2) you approve this draft; (3) a P8
prototype on the real scene (the camp beats 0–6 first, gray-box art); (4) the village beats (canal, mill, field
weeds); (5) the town's tools.

## 7. Choices for the user
1. ~~**The button.**~~ **Decided 2026-11-02: one kit button with a tool rail** (§3.3).
2. ~~**"Without showing too much".**~~ **Decided 2026-11-02: only effects**, no hand or tool models; the people read
   it as weather and wonders.
3. ~~**Humidity on the glass.**~~ **Decided 2026-11-02: yes**, condensation is the humidity gauge (§5.1).
4. ~~**Trash heaps.**~~ **Decided 2026-11-02: removed** (§5.6).
5. ~~**The town era.**~~ **Decided 2026-11-02: yes** to the town plan (§8): the town uses up the land and the Keeper
   keeps it alive; the seed becomes a **seed pouch** (the land picks tree, reeds or flowers); the people learn
   **coppicing** and the **reed bed**.
6. ~~**The lid.**~~ **Decided 2026-11-02: no lid.** The box stays open-topped; smoke drifts out of the top and its soot
   on the upper glass goes to the cloth. Revisit only if the Ecology Lab shows the water model needs a closed box.
7. ~~**Tired fields and a cleanup crew.**~~ **Decided 2026-11-02: left out for now.**

---

## 8. The town era: what the Keeper does (analysis, 2026-11-02)

The user's question: *"maybe there's something to be added? Maybe they cut down many trees to grow and fuel their
economy, so more seeds to grow trees? Not sure... analyze."*

### 8.1 What a crafts town does to its land [K]
A renaissance crafts town (mills, workshops, a market, guilds: game-definition §8) runs on its land:
- **Timber** for houses, the guildhall and the mill wheels, and above all **fuel**: charcoal and firewood for the
  smith, the potter's kiln, the baker's oven, the brewer.
- **Bark** for the tanners, **dyes and wash water** for the dyers: both foul the stream.
- **Smoke** from every chimney and kiln.
- **Fields worked year after year** grow tired.

The real answers were just as local: **coppicing** (cut hazel, ash or oak to the stump and it regrows into a crop of
poles on a rotation of years), woodland laws, **reed beds** where dirty water slowly clears, crop rotation and
fallow, manure.

### 8.2 The shape this gives the eras ★
| Era | The Keeper's role | The land's story |
|---|---|---|
| Camp | **Make it live**: mist, the one tree | Barren → meadow → forest |
| Village | **Route the water**: the canal, field weeds | Water reaches the fields and the mill |
| Town | **Keep the balance**: the town eats the land, the Keeper keeps it alive | A town *and* a living land |

So each era has its own verb, and the town gets a real role instead of a few gift tools. The box completing (at the
town's peak) can then say how it ended: the museum label *"a town that kept its forest"* (which joins P7's
Keeper's-character idea, research 14 §2.6).

### 8.3 The user's idea: they fell trees, the Keeper replants
**For:**
- **It continues the heart of the camp.** The tree you cared for becomes a forest; in the town that forest is cut,
  and replanting is how you keep caring for it. The player already knows the gesture and the meaning.
- **It's visible:** stumps, clear-cuts, log stacks by the workshops, smoke over the kilns. No bar needed.
- **It's real**, and the real fix gives the best story (below).
- **It feeds P7:** *"Fell the east wood for the new guildhall, or bring timber the long way?"*

**Risks, and the answers:**
| Risk | Answer |
|---|---|
| A death spiral: fewer trees → fewer seeds → fewer trees | **The first tree is never cut** (the old tree in the square) and always drops seeds, and the forest has a protected heart around it. The pouch never runs dry |
| Guilt: watching your forest fall | The people take what they need, never all of it (a floor, as with all care); felling only slows when the forest is thin, and the economy just leans to other trades. Not the player's fault, never a loss card |
| A chore: replanting every stump | Town seedlings need **no care** (the one tree was the care lesson); a tap per clear-cut, and the land does the rest. Then **the people take over** (next row) |
| It ends up a treadmill | **Care moves up:** after the Keeper has replanted a few clear-cuts, a notable notices the seedlings always rise where the old trees fell, and the woodcutters start **coppicing**: they leave the stumps, which regrow. From then on the town keeps its own forest, and you see it (rings of young poles round old stumps). That's the town-era story beat, and it ends the chore by design |

**Verdict ★: yes.** It's the strongest town idea. It needs the three answers above (the old tree is sacred, no care
for town seedlings, coppicing as the people's own fix).

### 8.4 Other candidates
| The town's pressure | You see | The Keeper's interaction | What the people learn | Verdict |
|---|---|---|---|---|
| **Felling** for timber and fuel | Stumps, clear-cuts, log stacks | **Seed pouch**: tap a clear-cut | Coppicing; a forester | ★ Core (§8.3) |
| **Fouled water** from dyers and tanners | The canal below the workshops runs coloured; fish move upstream | **Seed pouch** at the water's edge: a **reed bed**, which slowly clears it (a real constructed wetland) | They dig settling ponds by the dye works | ★ Same tool, the land picks the seed |
| **Smoke** from forges, kilns, ovens | Smoke drifting out of the open top, a soot film on the upper glass | **Cloth** for the soot | Tall chimneys | ★ Cloth only. **No lid** (decided 2026-11-02): the box stays open-topped |
| **Tired fields** | Fields pale after years of use | **Cleanup crew**: drop worms and isopods into the soil; you see them in the cross-section; the soil recovers (a real bioactive terrarium) | Rotation and fallow | Optional (Choice 7). Charming, but a seventh tool |
| Quarrying | The hill bites | The pebble (drop a stone in) | — | **Drop**: giving resources isn't tending |
| Night life | The square at night | The lamp | — | **Drop as a tool**: keep the lamp-lit square as the people's own |
| Bees and dyers' colours | — | Flower seeds | — | **Folded into the seed pouch**: sown on meadow, it's flowers |

### 8.5 The seed pouch: one tool, the land picks
The camp's **seed** (the one tree) becomes the **seed pouch** in the town, with the same one-tap gesture, like the
outcrop rule of research 10 ("the land picks"):
- **in a clear-cut or bare ground → a tree** (no care needed),
- **at the water's edge → reeds** (clears fouled water, brings frogs and birds),
- **on meadow → wildflowers** (bees for the beekeeper, colours for the dyers).

So the rail stays short: **cloth, mister, seed (pouch), tweezers, stick**. That's five tools by the town, versus eight
gift tools plus decorate today.

### 8.6 What stays true
- **Pillar 3 (Keeper, not King):** you never mark trees to keep or forbid a felling; you replant and clean.
  The people decide what to cut, and in the end they learn to cut well.
- **Pillar 4:** nothing collapses. Felling has a floor, fouled water only colours it and moves the fish, smoke only
  hazes the glass. All of it is capped like care today.
- **Absence:** stumps resprout slowly on their own, reeds spread on their own, and the smoke drifts out of the open top on its own.
  The Keeper only speeds things up.