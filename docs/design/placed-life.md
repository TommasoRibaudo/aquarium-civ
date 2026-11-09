# Placed life: the land opens the jar (spec, 2026-11-08)

Status: **draft for the user's sign-off** (#416). Every number is **[est]** until #434's pacing pass on the real terrain.
Research: [18 placed life and ecosystems](../research/18-placed-life-and-ecosystems.md),
[19 wolves and the first dogs](../research/19-wolves-and-the-first-dogs.md). Decisions: decisions.md 2026-11-07 (three
rows) and 2026-11-08 (this spec's answers). Beat sheets: [animation-specs.md](animation-specs.md) P0 (the shared quadruped) and B12–B26.
Milestone: **P9 Placed life** (#416–#447).

This replaces, where they differ: [keepers-hands.md](keepers-hands.md) §2 beats 2, 3 and 6 (rabbits, bushes and ducks
"arrive"), §3.2's "Herd: gone" row, and research 15's "no bush placement".

---

## 1. The rule in one line

**When the land can hold a species, it shows in the Keeper's kit; the Keeper places one founding group where they
choose; it grows to what the land holds; nothing big arrives by itself.**

The Keeper places six species: the **berry thicket, rabbits, deer, ducks, fish and wolves**. Small life (crickets,
songbirds, jays, frogs, dragonflies, a heron, an owl, ravens, later butterflies) still comes by itself. It is a sign,
not a resource, and nothing the people need depends on it.

---

## 2. The jar

### 2.1 States (one per species, in the save)

| State | Means | The player sees |
|---|---|---|
| `Locked` | The land or the ladder isn't ready | Nothing. The species is not in the kit. Too soon is never possible, so nothing fails |
| `Ready` | Readiness met (§3) | The life slot **wiggles once** (on the next open if it happened while away), then shows a **small dot until something is placed**. The people's hint starts (§2.4) |
| `Held` | The species is in hand; a ghost is shown | Valid ground glows; a faint ghost of the group sits where the player tapped |
| `Placed` | The founding group is in the box | The arrival plays at once (#424, B12), the people react, a history line, the dot goes |
| `Restock` | At the floor for 2 game years (§4.3) | Back to `Ready` for one more founding group, with the same wiggle and dot |

A species goes `Locked → Ready → (Held ↔ Ready) → Placed`, and `Placed → Restock → Placed` at most once per floor
spell. The thicket and the wolves never restock (§4.3).

### 2.2 The absence rule (signed off, Q1)
**A ready jar waits for the Keeper. Nothing arrives by itself, ever, and there is no fallback.** Readiness can be
reached while away; its wiggle waits for the next open, like a held moment (visit-loop §2). The box keeps living
meanwhile: small life still comes, the people still work. A box nobody opens stays at its last placed rung. This is
the visit loop's "hold, stage, ask" applied to life: the Keeper's gift never happens unseen.

No notification for a ready jar (Q1 option C not taken).

### 2.3 The ladder (signed off, Q3)
Each jar opens only after the rung below it is placed, on top of its land condition.

```
thicket ──► rabbits (camp) ──┬──► deer ──► wolves
                             └──► ducks ──► fish  ──► (village gate)
the seed unlocks after the thicket is placed and the camp has started
```

The two branches run side by side; when both have a ready jar, the player picks the order (the chooser, §6).

### 2.4 The waiting hint (the people notice too)
While a jar is `Ready`, the people show what's missing, a little more the longer it waits. Never a bar, never a line
of text on its own (seen, not read).

| Waiting | Thicket | Rabbits | Deer | Ducks | Fish | Wolves |
|---|---|---|---|---|---|---|
| From ready | A gatherer stops at the meadow and looks at the empty grass | A hunter crouches at the meadow and studies the ground | A hunter stands at the wood's edge and looks in | A child crouches at the empty pond | A fisher stares into the water, spear in hand | At dusk the people look toward the far wood a beat longer than usual |
| ~1 game year | They talk with their hands about it (B7 talk, pointing) | Two hunters point across the meadow | The same, plus a pointing kid | The child throws a stone in and watches the rings | The fisher wades in, finds nothing, shrugs | — (wolves have no hint past this: they're not missed) |
| ~3 game years | A child sits at the spot each evening | Same, most days | Same, most days | The child sits there each evening | Same, most days | — |

The hint plays as a background-life beat (B11), at most once per visit for each ready jar. The beat sheets are in
B12 (#424).

---

## 3. Readiness per species [est]

`meadow` = grass + tall-grass cells as a share of the land (`EcoModel.MeadowFrac`). One game year ≈ one real hour.

| Species | Rung below | Land condition | Notes |
|---|---|---|---|
| **Berry thicket** | — | meadow ≥ **10 %** | The first jar. Today's first-rabbit point (`rabbitsAtMeadow` 0.10) |
| **Rabbits** | thicket placed | meadow ≥ **15 %** | Rabbit tracks show only after rabbits are placed (D3) |
| **Deer** | rabbits placed (the camp) | ≥ **5 grown trees** (the Keeper's tree counts) | Earlier than the village's 8 (Q2) |
| **Ducks** | rabbits placed (the camp) | pond ≥ **12 cells** | Earlier than the village's 20 (Q2) |
| **Fish** | ducks placed ≥ **6 months** ago | reeds ≥ **6 cells** (`cReeds`) | The reeds the ducks bring are the fish's shelter |
| **Wolves** | deer placed | deer ≥ **5** for a full game year, wood ≥ **20 grown-tree cells**, and a den spot ≥ **1.5 u** from the fire (§5.6) | Predators come after their prey |

### 3.1 The gates that change

| Gate | Today (`EcoTuning`) | Now |
|---|---|---|
| **Camp** | meadow ≥ 20 %, rabbits ≥ 2 (arrived) | meadow ≥ 20 %, **rabbits placed** (≥ 2) |
| **Village** | camp, pond ≥ 20 cells, ≥ 8 grown trees, plus the population and building rules | the same, **plus ducks and fish placed** (Q2). Deer and wolves are not required |

Natural moisture still moves the land without the Keeper (keepers-hands §5.4), but placed life never comes by itself.
So the camp and the village now wait on the Keeper's jars. That is the intent: the Keeper's gift starts each era.

---

## 4. Populations

### 4.1 Founding groups and numbers (D1, D2) [est]

| Species | Founding group | Floor | Cap | Breeds |
|---|---|---|---|---|
| Berry thicket | 3 bushes | — | **20–25 bushes**, in its own area (§5.1) | Spreads a bush a season inside the area until full |
| Rabbits | a pair | 2 (`SimCore.HuntFloor`, as today) | 1 per 35 meadow cells (`meadowPerRabbit`, as today) | As today (`rabbitGrow`) |
| Deer | a stag and two does | 2 | 1 per 3 grown trees, at least 3, at most 8 | One fawn per doe in spring, if under cap |
| Ducks | a pair | 2 | 8 (wild; pen ducks counted apart, §7) | A clutch in spring: 3–5 ducklings, ~half grow up |
| Fish | one school of 6 | 3 | about 1 per 3 pond cells, at most 8 shadows shown | Up to cap over a year |
| Wolves | a pair at the den | 2 (the pair never leaves) | 6, one pack per box | 2–4 pups in spring; young wolves leave at the box edge in autumn |
| Dogs | — (adopted, §5.7) | — | 3 | One litter when the line allows (§5.7) |

The sim keeps counts for each species (as `EcoState.rabbits` does) and a few named individuals where the view or a
choice needs them: the stag, the white stag, the grey wolf, the white wolf, every dog.

### 4.2 Who takes, and the floors
Every taker respects the floor: the hunters (rabbits, deer), egg gathering (ducks: eggs only, never birds), fishers
(fish), and the wolves (rabbits, deer, wild ducks; never pen ducks once guarded, §5.6). A take that would cross the
floor doesn't happen. **No species ever dies out.** Wolf kills count toward the floors (#422).

### 4.3 Shy, then restraint, then restock (D4, Q9)
1. **Shy:** a species at its floor keeps to the far side of its ground and shows mostly at dawn. Fewer seen, none gone.
2. **Restraint (once per species):** the first time a species sits at its floor for a season, a notable rules on it,
   staged live as a small moment: "leave the does in spring" (deer), "leave the young ones in spring" (rabbits), "leave
   the nests till summer" (ducks), "let the small ones go" (fish). From then on, that take pauses in spring. A history
   line and a book entry.
3. **Restock:** a species at its floor for **2 game years** in a row reopens its jar for one more founding group (the
   same as the first). Wolves never restock (they never drop below their pair). The thicket never needs to: a picked
   bush recovers (`EcoBushes`).

---

## 5. Per species

### 5.1 The berry thicket (Q4, Q5, the user's note)
- **Exactly one thicket per box, ever.** It is the only bush life in the box: **no bushes grow anywhere until it is
  placed**, and none ever grow outside its area. Today's wild thickets and strays (#385: `thicketBoost`, `bushStray`,
  `thicketBAfter`) are switched off; the bush cover stage of succession exists only inside the thicket's area.
- **Its area** is a disc around the placed spot sized to hold **20–25 bushes** (about three tents wide). It fills a bush
  a season from the 3 founders, then stops.
- **The people never build on it.** The area is reserved: no tent, hut, field, pen, path or yard is ever planned on
  it, in any era. The settlement planner treats it like water. The people walk into it to pick.
- **Valid ground:** meadow, at least the camp margin (`bushCampMargin` 0.9 u) from the fire, the whole disc on dry
  land (no lowland, no pond), clear of existing tents and paths.
- **The seed** unlocks once the thicket is placed **and** the camp has started (user, 2026-11-08; it used to follow the first wild bush, `seedUnlockBush`).
- **Near the Keeper's tree** (D6): the tree's weeds grow a little slower under it [est 0.8×]. A small reward, never needed.
- **Uses:** picking (B5, exists), the berry kid, the songbird, the drying mat (B17, #430).

### 5.2 Rabbits
- As today (`RabbitHerd`, `EcoRabbit*`) once placed; they no longer arrive by themselves. The founding pair's cell is
  where the herd starts (`rabbitArrivalCell` today).
- **Valid ground:** any meadow cell outside the camp margin.
- **Uses:** the hunt (B6, exists), hides → tents (exists), snares (#430), kids chase bunnies (exists).

### 5.3 Deer
- A small herd in and at the edge of the wood (#420). **Valid ground:** within ~0.3 u of a grown tree, outside the camp
  margin. Placed at the edge, they are seen often; deep in, they're shy and rare (D6).
- With wolves about, they graze nearer the camp (the human shield, research 19 §1).
- **Uses:** the deer hunt, the pole carry, the feast and the antlers (#426); dawn at the pond, nobody hunts at the water
  (#427); the shed antler a child finds in spring.
- **The white stag (D5):** once per box, a choice staged live: "Hunt it, or let it be?" Hunt: a feast and great antlers
  over the chief's tent. Let it be: it shows at dawn on later visits, and the people tell stories of it.

### 5.4 Ducks
- **Valid ground:** pond cells at the edge. The reeds grow first where the ducks were placed.
- **Uses:** eggs and the mother duck's charge, feathers → fletched arrows (a visible upgrade on the hunters' backs),
  ducklings in a line with a kid copying, feeding (#428).
- **Village:** the duck pen (§7).

### 5.5 Fish (Q7)
- **No fishing of any kind until fish are placed.** Today's line fishing from a log (#364, `LifePlanner.Fish`, `FishBeats`)
  waits for the fish; before that, the pond is for drawing water, splashing and the ducks. When fish arrive, fishing
  appears at once: the line, spearing, the smoking rack (#429).
- **Valid ground:** pond cells away from the edge (the deeper part).
- Fish are seen as shadows under the water, scattering when kids splash.
- **Village:** the weir and basket traps where the canal leaves the pond (#432); the fish pond (§7).

### 5.6 Wolves (research 19, decisions 2026-11-07)
**The hard rules:** a wolf never touches a person (tested in the sim and in the planner: no plan has contact). The
people never kill a wolf; they drive them off with shouts, torches and fences. Wolves are never among the tents.
What they steal is small and capped: a strip of meat or a hide, at most once a season, never while the people would go
hungry, never as a "you lost" card while away. Testable form: the **tent ring** (every tent plus 0.25 u) is closed
to wolves in the planner, and a wolf always keeps ≥ 0.3 u from any person. The drying rack stands at the camp's edge,
outside the ring, so the thief can reach it without crossing it. Fear is acted (kids run to the fire, everyone goes quiet at a howl),
never scored; moods may dip a little at night, as with weather.

- **Valid ground:** a den spot (a hollow under roots or a rock) in grown-tree cells ≥ 1.5 u from the fire, on the far
  side of the wood. The glow shows only those.
- **The pack:** 2–6, one per box. Most chases fail; a kill takes an old doe, a fawn, a rabbit or a wild duck, within
  the floors. Ravens circle every kill.
- **Boldness** (0–1, starts 0.2): **+** leaving the scraps, leaving them their kill; **−** chasing them off, taking
  their kill, the dogs on guard. It sets how often eyes show at the firelight, whether a wolf trails the hunters, and
  how near the grey wolf comes to the bone spot. Its cap is "the edge of the firelight": never closer.
- **Scenes:** the pack hunt (#440), the howl at dusk and eyes at the edge (#441), the meat thief and the grey wolf
  (#442), trailing the hunters and the ravens' kill (#443), the knock sends them into the trees (exists).
- **The choices** (each waits for the Keeper, plays live, and has "Let them decide"):
  1. **The meat thief** (after the first theft): chase them off, or leave the scraps (#442). Scraps opens the grey wolf
     at the bone spot and the early lost pup.
  2. **The lost pup** (spring, on the scraps path; or two springs after the first choice on the chase path, so dogs
     are never closed off): bring it home, or leave it with its mother (#444). Asked again next spring if left.
  3. **The wolves' kill** (ravens over the wood): take their meat, or leave it to them (#443).
  4. **The duck pen** (village, once): a thorn fence, or let the dogs keep watch (#446; the second only with a dog).
  5. **The white wolf** (rare, once): a pure moment, not a choice. Everyone comes out to see it; a book page (D5).

### 5.7 Dogs
- **Adoption** (choice 2): the child who found the pup becomes its **keeper** (a bond, shown on both cards).
- **Stages:** pup at the fire (chews a hide, follows its child, steals a fish) → grown, with a hide collar: **joins
  hunts** (flushes rabbits; after a missed deer, sometimes finds it) and **guards** (barks at night when the pack comes
  near; the wild wolves turn back; the meat thief stops) (#445).
- **Generations** every 2–3 game years: a litter when there is room under the cap of **3**. Each generation's look
  drifts toward "dog": ear droop, coat patches, tail curl, shorter face (the shared quadruped's parameters, #425). By
  the village they look like dogs.
- **Old age:** a dog's muzzle greys, it lies by the fire more, and one visit it's simply gone, with a history line,
  as old people's deaths are told today (`SimCore.Apart` "died of old age"). Never on screen.
- **The book:** "The first dog", with its keeper's name.

---

## 6. The tool (Q6, Q8)

- **One life slot in the kit rail, next to the seed** (#423). It shows only when at least one jar is `Ready`.
- **Ready signal:** a single wiggle when a jar opens (on the next open if it opened while away), then a small dot on
  the slot until that jar is placed.
- **Choosing:** with one ready jar, tapping the slot takes that species in hand. With two or more, a small row of
  icons opens above the slot (a bush, a rabbit pair, a stag, a duck, a fish, a wolf); tap one.
- **Placing (ghost, then tap):** while a species is in hand, its valid ground glows softly and everything else stays
  dark. The first tap on a glowing spot shows a faint **ghost** of the group there. Tapping the ghost again (or the
  small "Place" chip by it) places it; tapping elsewhere on valid ground moves the ghost; tapping the slot or outside
  puts it back. Tapping dark ground does nothing but a soft "no" wobble of the ghost.
- **No undo** after placing (Keeper, not King). No moving or removing animals later; the knock is the only touch on
  wolves.
- **The arrival plays at once** while the Keeper watches: the placement is the Keeper's own act, so it skips the
  moment director's queue (B12). A history line, a glass-voice line in the people's words ("the sky sent game"), and
  its first-time book entry.

---

## 7. The village tames only what's needed

At most **2 pens** in a small village, chosen by need (decisions 2026-11-07): a **duck pen** (ducks taken from the wild
flock, which keeps its floor) and a **fish pond** (a dug pool off the canal). The pen ducks don't count toward the wild
floor or cap. The wolves circle the duck pen **once**, before it's guarded, and that raises choice 4 (#446). After it,
the pen is safe either way. Both pens are reserved ground like the thicket. #431.

---

## 8. The arc, revised [est]

| # | Beat | The Keeper | What happens | Era |
|---|---|---|---|---|
| 0 | Barren | Wipe | As today | Tribe |
| 1 | First mist | Mist | As today; crickets with the first green | Tribe |
| 2 | **First berries** | **Place the thicket** (meadow 10 %) | The gatherers' first job; the berry kid | Tribe |
| 3 | **Game** | **Place the rabbits** (meadow 15 %) | Tracks, the first hunt, hides; at meadow 20 %: **the camp** | → Camp |
| 4 | The one seed | Plant and tend | Unlocks once the thicket is placed and the camp has started (2026-11-08) | Camp |
| 5 | The forest | — | Jays spread it | Camp |
| 6 | **Deer** | **Place the deer** (5 grown trees) | The deer hunt, the antlers, dawn at the water, the white stag | Camp |
| 6b | **Wolves** | **Place the wolves** (deer ≥ 5 for a year, wood 20) | The first howl, the pack hunt, the meat thief (choice 1) | Camp |
| 7 | The water rises | Mist; wipe the droplets | Reeds, frogs, dragonflies come by themselves | Camp |
| 7b | Spring | — | The lost pup (choice 2); the dog arc | Camp |
| 8 | **Ducks** | **Place the ducks** (pond 12) | Eggs, feathers, ducklings; the young dog chases them, then learns | Camp |
| 9 | **Fish** | **Place the fish** (ducks + reeds) | Fishing begins: the line, spears, the rack | Camp |
| 10 | Village | (pond 20, trees 8, ducks and fish placed) | The duck pen and fish pond, the weir, the wolves at the pen (choice 4) | → Village |

Steps 6–6b and 7–9 are the two branches and can interleave. Six placements across the tribe and camp, each one a
scene, so the camp has a "what next?" on about every visit.

---

## 9. Save and existing boxes (D7)

- Per species: `state`, `readyMonth`, `placedMonth`, `placedCell`, `atFloorSince`, `restocks`; the thicket's centre and
  radius; the wolves' den cell and boldness; each dog's generation, look parameters and keeper id.
- **Old saves:** a species already present when the save loads (rabbits that arrived, wild bushes) counts as
  `Placed` at its current spot; wild bushes outside a single thicket area stay but never spread (they thin by the
  existing rule, `bushThin`). Jars open for the rest. No forced reset.

## 10. Debug panel (D8, CLAUDE.md rule)

Ready every jar · place X here (the next tap places, no ghost) · jar again (restock now) · species to its floor ·
white stag now · wolves bold (boldness 1) · meat thief now · lost pup now · next dog generation · wolves at the pen now.

## 11. Where each ticket lands

| Ticket | Spec sections |
|---|---|
| #417 the jar | §2, §3, §6 (the sim side), §9 |
| #418 the thicket | §5.1 |
| #419 rabbits | §5.2, §3.1 camp gate |
| #420 deer | §5.3 |
| #421 ducks and fish | §5.4, §5.5 |
| #422 shy and restraint | §4.2, §4.3 |
| #423 the life slot | §6 |
| #424 arrivals and the waiting hint | §2.4, §6, B12 |
| #425 the quadruped | P0 |
| #426–#430 scenes | §5, B13–B17 |
| #431 pens, #432 weir | §7, §5.5, B18–B19 |
| #433 first run and arc | §8, §3.1, §10 |
| #434 pacing | every [est] here |
| #437 wolves, #438 dogs | §5.6, §5.7 |
| #439–#447 wolf and dog scenes, audio | §5.6, §5.7, B20–B26 |
| #435 done | §8 end to end, no cheats |
