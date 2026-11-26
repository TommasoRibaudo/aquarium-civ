# 22. A second-phase kit? Who builds the village's water things

Research for the user, 2026-11-25. Docs only: no code, no issues. Claims marked **[unverified]** could not be checked
in a source or in the code; **[est]** is a guess.

## 1. The question and why it came up

Two P9 buildings assume a canal:

- **#431** (open, P9): the village builds "a **duck pen** by the pond, then a **fish pond** dug off the canal".
- **#432** (open, P9): "the people build a fence of stakes and woven baskets **where the canal leaves the pond**", and
  two of them lift the basket every morning.

The living land has no canal. In the code a canal is only ever dug by a Keeper shaping gift: `Settlement.DigCanal`
runs for `NudgeKind.Channel` (the village gift) and `NudgeKind.Spring` (the town gift), and for one debug button
(`Sim/Settle/Settlement.cs:517-518`, `:541`, `:599`). A living-land box has no Shape tray at all: "a living-land box has
no Shape tray: the kit is the Keeper's hands" (`UI/GameHud.cs:1341`). So no normal box can ever have a canal, and so
no fish pond off it and no weir where it leaves the pond. (A small correction to the question: it was the **channel**
gift, not only the spring, that dug canals. Both are gone in the living land.)

The user sees two ways in:

- **(A)** someone places them in a second phase, the village phase: the Keeper, or the people;
- **(B)** the people build them by themselves as the town grows, as they build everything else.

A second example came the same day. **The P10 expeditions now start from a building with an airship** in a
Renaissance-punk style (Renaissance flying machines, not steampunk), and the airship is the only way out: the rope to
the rim (#460) is dropped (the user, 2026-11-25; logged on `docs/airship-call`, PR #759, not merged yet). So the
village or town gains a second one-off building with a clear job, and the same question applies: who puts it in the box?

And a bigger idea: a **second-phase Keeper's kit**. Today's kit builds up nature (mist, plant, place life). A second
kit might build up or shape the town. The user is not sure.

**Narrowed by the user, 2026-11-25:** "it's the people who place things in this game. We do shape it." So the Keeper
never places a building, and two questions remain: (1) is the canal a shaping thing (the Keeper's) or a building thing
(the people's), and how do the pen, fish pond and weir then appear (§5); (2) what can a second-phase kit do that shapes
the town without placing buildings (§6). The recommendation is in §7.

## 2. What the game has today

### 2.1 The kit, by phase (living-land box)

| Phase | What the Keeper does | Where |
|---|---|---|
| First run | Wipe the misted glass (cloth) | `UI/GameHud.cs` `BuildKit` |
| Tribe, camp | Mist the land, pull weeds (tweezers), plant **your tree** (the seed), place life from the jar: bushes, rabbits, deer, ducks, fish | kit-redesign.md §1; placed-life.md §2-§3 |
| Camp, village | Earned seeds and bushes (#726-#731, in build), the plucking tool, the **Spade** (#730) | landscaping.md; decisions 2026-11-14, 2026-11-15 |
| Village | The wolves' jar (about year 50 once there is room, decisions 2026-11-13). Choices at held moments | placed-life.md §6 |
| Village, town (planned) | Expeditions into the Keeper's house; **decorations placed in the tank** (P10, research 20) | research 20 §0 |

`KitTool` today is `None, Mister, Seed, Tweezers, Life, Bush, Landscape` (`UI/GameHud.cs:153`). Every tool acts on
nature. Nothing in the kit acts on the town, and nothing is new to the Keeper's hands at the village: the village opens
when the ducks and fish are placed (placed-life.md §3, "the Keeper's gift starts each era"), and then the Keeper's
hands have nothing new to do until the wolves or P10.

### 2.2 How buildings appear

- **The economy decides what, Settle decides where** (`Sim/Settle/Settlement.cs` header): every month each new building
  gets a lot, scored by `Encyclopedia` weights (water, slope, green, nearby kinds). No dice: hashes only.
- **Eras:** camp, village, town (`S.era`). The town economy builds Workshops, Shops, Guildhall, Farms itself
  (decisions 2026-10-30, #114).
- **The Keeper could already steer placement.** Every kind has a `Term.Nudge` weight of 4.0 (`Encyclopedia.cs`), and
  `Settlement.KindFor` turns a building that answers a shaping into the fitting kind: a channel's workshop is a
  **Watermill**, a grove's a **TimberYard**, a herd on wet ground a **Jetty**, an outcrop a Quarry, Kiln or Smithy, a dye
  meadow a **Dyer** (`Settlement.cs:308-334`). `Lot.nudgeId` records which gift a lot answered. This is "the Keeper
  suggests, the people build", and it is fully built. It is just unreachable in the living land.

### 2.3 What the village phase lacks because of it

people-and-things.md (village and town table) marks these **blocked**, all for the same reason ("they stand only as
answers to the Keeper's shaping gifts ... which the living land does not have"):

| Blocked thing | Came from |
|---|---|
| Bridges (lean on the rail, race sticks, B58.h-i) | a path crossing a dug channel (#274) |
| Watermill | the channel gift |
| Jetty | a herd placed on wet ground |
| Orchard, timber yard | the grove gift |
| Quarry, kiln, smithy (kids watch the sparks, B58.a-b) | the outcrop gift |
| Dyer, hives | the dye meadow |
| Pasture (goats) | the herd gift (the pond family's single goat pen came back by a choice, decisions 2026-11-16) |

So the canal gap is one case of a wider gap: **the living land kept the people's building, but lost every building the
Keeper used to cause.** The village looks the same in every box.

### 2.4 What already exists to build on

- `EcoState.duckPen`, `penDucks`, `penGuard`, `penCircledMonth` (`Sim/Eco/EcoState.cs:154-158`): the duck pen's state is
  there (the debug pen sets it); #431 adds where it stands.
- `DigCanal(x, z)` routes a channel from the pond to a spot round lots and the green, and bridges follow paths
  (`Settlement.cs:541`). Any caller can use it, not only a gift.
- The camp has a **dry stream bed** the people use (kids walk it, B48.a; **two kids build a little dam in it**, B48.b;
  `View/CampScenes5b.cs`, `View/CampLife.Camp.cs:870-890`). It is a view place (`PlaceKind.Stream`); whether the sim
  knows it as ground is **[unverified]**.
- keepers-hands.md (2026-11-02) planned exactly the missing tool: the **stick**, "drag a line from water; digs a canal"
  in the village (§3, row 8; tool table). It was never built.

## 3. What was decided or tried before

| Date | Call | What it means here |
|---|---|---|
| 2026-10-29 | "Placement must grow organically from the people's choices, not at random (research 08)" | The people site buildings. A Keeper placing houses goes against it |
| 2026-10-29 | Gate 3: nudge form is **shape the land** (rain, groves, channels); toolset per era (#42): "Village: channel, Keeper's Lamp, hill & hollow" | The first design had a second-phase kit, and it was land shaping for the village |
| 2026-10-29 | "Two kinds of tool (gifts the people answer, with a limit + free landscaping)" | "Player suggests, people answer" was the core model |
| 2026-11-01 | The user: "we will be 'polishing the game' so no new features, just polishing" (project notes) | A new kit is a new feature. Since then the user has added P9, P10 and Seeds and bushes, each confirmed on its own (#724: "a new feature in the Polish phase, so the user should also confirm it is wanted now") |
| 2026-11-02 | The Keeper's hands: "village → dig a canal (mill, fertility) → town"; the kit by the town is "cloth, mister, seed pouch, snips, stick" | The canal was meant to be the Keeper's village act, with the stick |
| 2026-11-06 | Seen, not read: events watched live, "about one choice per visit", literal, cute animations | Whatever brings the canal should be a scene the player watches, not a line in History |
| 2026-11-07 | Placed life: "Taming in the village: only what's needed, at most 1-2 pens ... (e.g. a duck pen and a fish pond)" | The origin of #431. It said nothing about who digs |
| 2026-11-14 | Core rule: everything in the box is used for work **and** life | A canal, a weir, a fish pond each need life uses (kids, grown-ups) and a debug cheat |
| 2026-11-14 | Shaping-only text deleted (wishes, nudge answers, gift teasers, Lamp lines) | The gift model's words are gone; a return of "suggest a spot" needs new ones |
| 2026-11-14 / 15 | More seeds and bushes earned; the Spade plucks grown trees too | The Keeper's nature kit grows; it is the precedent for "a new tool, in Polish, if the user confirms" |
| 2026-11-16 | Goat pen back: one goat, by a choice ("A home here"), not a pasture | Kept animals arrive through the people's story, sited by the sim |
| 2026-11-07 | Expeditions: "They get out by themselves" (a rope to the rim) | The way out was the people's own doing, not the Keeper's |
| 2026-11-25 | Expeditions leave by **airship**, from a building; airship only (PR #759, not merged) | A second one-off building. Its era, how it leaves the box, what it is made from, its look (#454) and any launch or return scene are open for the P10 spec (#453) |

Research 18 §5.5 and its table (row 6, "the stick's first canal flows (village)") were written while the stick was
still the plan. #431 and #432 inherited that assumption.

## 4. What other games do

| Game | Who places | How the player influences | What unlocks with eras | Lesson for us |
|---|---|---|---|---|
| **Populous** (1989) | The people build houses wherever the land is flat | Raise and lower land; the bigger the flat, the bigger the house; a papal magnet says "go here" **[from memory, unverified]** | Powers by mana | The classic split: **god shapes land, people build**. Shaping is indirect town planning |
| **Black & White** (2001) | The god places buildings from scaffolds; villagers build them | The village store's **desire flags** show what the people need; over-helping makes a lazy village ([wiki](https://blackandwhite.fandom.com/wiki/Village_Store)) | Miracles, buildings by tier | The people **ask**; the player answers. A flag that says "we want water here" fits our held moments |
| **From Dust** (2011) | The tribe builds villages at totems | The Breath moves earth, water and lava to make paths ([Wikipedia](https://en.wikipedia.org/wiki/From_Dust)) | Powers per totem village | The god never builds; the god makes the **land** ready, the people come and build |
| **Godus** (2014) | Followers build abodes on plots | The player flattens and sculpts land to make plots ([wiki](https://godus.fandom.com/wiki/Manor)) | Settlement types by cards | Same split. Reviews call the sculpting a chore at scale **[unverified]** |
| **Reus** (2013) | Villages appear on fitting land and pick their own projects | Giants shape mountains, forests, oceans and place resources ([guide](https://guides.gamepressure.com/reus/introduction/z04fcc)) | Giant abilities via ambassadors | The closest match to our nature kit: **the player never builds a house**, and the town still varies by what the land offers |
| **The Universim** | Nuggets build homes themselves; the player places the rest | God powers; **Holy Ground** marks where they build homes ([Steam thread](https://steamcommunity.com/app/352720/discussions/0/1735468693705041032)) | Eras with a tech tree | A light "mark the area" tool is enough to steer self-building people. Players found the powers weak **[thread opinion]** |
| **Before We Leave** | The player places every building; Peeps work them ([TechRaptor](https://techraptor.net/gaming/previews/before-we-leave-preview)) | Direct placement, tech tree | Buildings by tech | Calm, but a manager's game. Not our Keeper |
| **Terra Nil** | The player places machines | Restore greenery, then biomes, then **remove every building** and bring fauna ([wiki](https://wiki.terranil.org/w/Tiers)) | Three tiers, each a new tool set | The **tool set changes with the phase**, and the last phase is a different verb. A second kit per phase is a known, liked pattern |
| **Timberborn** | The player places everything; beavers build it | Dams, levees, and **dynamite** to dig canals ([ModDB](https://www.moddb.com/games/timberborn/news/beavers-discover-dams-dynamite-in-time-for-droughts)) | Landscaping tab unlocks later | Canals as **player** infrastructure, unlocked mid-game, built by the inhabitants |
| **Townscaper** | The player clicks; the rules decide what grows | No resources, no menu of buildings ([Game Developer](https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making)) | None | The player marks *where*, the system decides *what*. Lovely, but there are no people |
| **Tiny Glade** | The player draws walls and paths; doors and details appear by themselves ([PC Gamer](https://www.pcgamer.com/games/city-builder/tiny-glade-review)) | Draw; the game fills in | None | A **drawn line** that the world dresses is a good feel for a canal |
| **Islanders** | The player places each building from a pack | Score by neighbours ([Steam](https://store.steampowered.com/app/1046030/ISLANDERS/)) | New packs on score | Unlock by growth, but direct placement |
| **Dorfromantik** | The player places tiles | Quests on tiles **[from memory, unverified]** | New tiles by quests | Landscape and village are one set; not a split |
| **Kingdoms and Castles, Banished, Ostriv** | The player places all; people walk and build | Direct | Buildings by tech or size **[from memory]** | The classic builder: the opposite of our pillar |
| **Cloud Gardens** | The player plants and places junk; plants grow over it **[from memory, unverified]** | Place and let grow | Levels | Placing **things** that nature (or people) then take over |
| **Pikmin, Tinykin** | The little ones carry and build bridges and walls the player points at **[from memory]** | Point, they do it | New areas | "You point, they do it, together" reads as cute and makes them the heroes |
| **Fata Deum** (2025, early access) | Followers build; the god can "plonk down blueprints" ([PC Gamer](https://www.pcgamer.com/games/sim/fata-deum-is-an-early-access-god-sim-inspired-by-black-and-white-but-i-wish-i-was-just-playing-black-and-white-instead)) | Inspire, curse, blueprints | **[unverified]** | Blueprints in a god game read as a manager's tool |

No idle or terrarium game found in the search uses a "town kit" (Terrarium: Garden Idle and similar are plant-only
**[unverified]**, the search did not cover them).

### 4.1 Four patterns

1. **God shapes land, people build** (Populous, From Dust, Godus, Reus): the most common in god games, and what we had
   with the gifts. Water shaping is the god's tool.
2. **God marks, people build** (Universim's Holy Ground, Pikmin, Black & White's scaffolds): a spot or a line, the
   people do the work, the player watches it.
3. **People ask, god answers** (Black & White's desire flags): the need is shown in the world; the player chooses.
4. **Player places, people work** (Timberborn, Before We Leave, Banished): the canal is player infrastructure. This is a
   manager game, not ours, and the user's 2026-11-25 call rules it out.

Era tool sets (Terra Nil, Timberborn's landscaping tab) are well liked when **the verb changes** with the phase, not
when the same tools get bigger.

## 5. Question 1: is the canal shaping or building?

The user's frame (2026-11-25): **"it's the people who place things in this game. We do shape it."** So the Keeper never
places a pen, a pond, a weir or the airship's building. A Keeper's tray of buildings (Before We Leave or Timberborn style) is
**rejected** on that quote, and on "placement must grow organically from the people's choices" (2026-10-29). What is
left is where the canal sits on the line between the two.

### 5.1 The case for each

| | **Shaping (the Keeper's)** | **Building (the people's)** |
|---|---|---|
| What it is | Like misting, planting and the Spade: the Keeper changes the land, the people react | A work project: the people decide to dig, dig over days, and use it |
| For | Water is nature, and the kit is nature. It was the plan: the channel gift (2026-10-29), then the stick, "village → dig a canal" (2026-11-02). Gives the Keeper a new verb at the village, which the game lacks (§2.1). The old gift answers (watermill, bridges, jetty) come back through `KindFor` | "The people place things". A dig is a great watched scene (seen, not read): a line of diggers, mud, the first water running, kids jumping in. The people choose where, so the canal fits the town they grew. No new tool |
| Against | A canal is a made thing in real life: ditches are dug by farmers, not by weather. A Keeper's straight line through the village can read as the Keeper placing infrastructure | The Keeper still has nothing new in the village. The canal looks the same in every box unless the land makes it differ |
| Code | `DigCanal` from a Keeper gesture; the gift machinery (`Nudge`, `Term.Nudge`, `KindFor`) is built | `DigCanal` from a sim trigger, plus a dig scene; Settle picks the target |

### 5.2 What other games do with water

| Game | Water is | Lesson |
|---|---|---|
| **Timberborn** | The player designs dams, levees and dynamite canals; beavers build them ([ModDB](https://www.moddb.com/games/timberborn/news/beavers-discover-dams-dynamite-in-time-for-droughts)) | Building, but the player plans it: a manager's canal, the opposite of ours |
| **From Dust** | The Breath carries water and earth; the tribe only builds villages ([Wikipedia](https://en.wikipedia.org/wiki/From_Dust)) | Shaping: water is the god's alone |
| **Reus** | Giants make oceans and swamps; villages choose their own projects ([guide](https://guides.gamepressure.com/reus/introduction/z04fcc)) | Shaping the land; the people's projects follow what the land gives |
| **Black & White** | The god's water miracle waters fields; the villagers ask through desire flags ([wiki](https://blackandwhite.fandom.com/wiki/Village_Store)) **[water miracle from memory]** | The god brings water as a gift, the people ask for it |
| **Populous** | Raise and lower land; water is where land is low **[from memory]** | Shaping the ground decides where water stands |
| **Before We Leave** | The player places everything; water is a resource from wells **[from memory, unverified]** | Building, player-placed |
| **Terra Nil** | The player places pumps and irrigators to bring rivers back ([wiki](https://wiki.terranil.org/w/Tiers)) **[detail from memory]** | Water as restoration, a shaping act |
| **Islanders** | No water shaping; buildings score by neighbours ([Steam](https://store.steampowered.com/app/1046030/ISLANDERS/)) | Not relevant beyond "placement is the game" |

God games put water with the god (From Dust, Reus, Populous, Black & White). City builders put canals with the
builders, planned by the player (Timberborn). None has the inhabitants dig a canal of their own will **[none found]**.

### 5.3 A middle way: the Keeper shapes the water's path, the people dig it

The land already has a **dry stream bed** (kids walk it, two kids build a little dam in it, B48.a-b). So:

1. **The Keeper shapes the ground, not the canal.** With the Spade (or a second gesture of it) the Keeper deepens a
   hollow or presses a groove from the pond toward the dry stream bed; or keeps misting the bed until it runs as a
   trickle in spring [est]. This is pure land shaping, like the hill and hollow of the old toolset.
2. **The people see the water and dig.** A held moment: the notable kneels at the trickle, then a line of diggers
   widens it into a ditch to the fields over a few game months, and lines it with stones. Settle picks the exact route
   (`DigCanal` toward the wet groove). If the Keeper never shapes, the people still dig a short ditch later from the
   pond to the driest fields, so nothing is closed off (as the dogs never are, 2026-11-07).
3. **Then the water things appear by themselves**, sited by Settle:
   - **duck pen**: on the pond's shore (needs no canal; `EcoState.duckPen` exists);
   - **fish pond**: dug off the ditch, a step from the pond;
   - **weir and basket traps**: where the ditch leaves the pond, as #432 says;
   - later, by the same `KindFor` answers: a bridge where a path crosses it, a watermill in the town.

Without any canal at all (if the user wants none), the pen stays on the shore, the fish pond is dug a step off the
pond with a one-cell cut, and the weir goes across a bay of the pond. #431 and #432 then need rewording.

### 5.4 The airship's building

It needs no water. The people build it as the village or town grows, like the Guildhall: the economy says when, Settle
says where (open, dry, flat ground with sky above, off the green). Its build is a staged project with the same scene
kit as the dig (gather, build over days, a first float everyone watches). The Keeper can at most shape the ground it
goes on (§6, idea 1). P10 needs it in every box, so it never waits on a Keeper act. Its open details (era, how it
leaves the box, what it is made from, its look, launch and return scenes) belong to the P10 spec (#453).

## 6. Question 2: a second-phase kit that shapes the town without placing buildings

Strongest ideas, all indirect: the Keeper changes the land, the light or what the people know; **the people decide and
build**. Sizes **S** under a day, **M** a few days, **L** a milestone, all [est].

| # | Idea | The player does | The people answer | Core rule (work and play) | Seen, not read | Game examples | Size |
|---|---|---|---|---|---|---|---|
| 1 | **Shape the ground** (level, raise a knoll, deepen a hollow) | Drag the Spade's second gesture over ground | Build on the flat you made; a knoll draws the shrine or the airship's building; a hollow fills and becomes a pond | Sit on the knoll at dusk, roll down it (B49 exists) | The first lot rises on the new flat | Populous, Godus ([wiki](https://godus.fandom.com/wiki/Manor)), From Dust | M (Settle reads slope already) |
| 2 | **Guide the water** (§5.3) | Press a groove, or mist the stream bed until it runs | Dig and line the ditch; then pond, weir, bridge, mill | Bark boats, sticks under the bridge, washing at the steps | A dig scene, the first water | From Dust, Reus | M-L |
| 3 | **Keep a place wild** (a ring of the Keeper's pebbles or a hedge the Keeper grows) | Plant a hedge line or set stones round a meadow | Never build inside; it becomes their commons: a picnic meadow, a games field | Games, picnics, courting | People gather there on festival days | Universim's Holy Ground, in reverse ([Steam](https://steamcommunity.com/app/352720/discussions/0/1735468693705041032)) | S-M (the thicket reserve already works this way) |
| 4 | **Light a spot** (the old Keeper's Lamp, as the dusk light through the glass) | Hold a warm light over a place in the evening | They gather there; in time they build there (an inn, a square) because it is "where the light falls" | Evening sits, dancing, stories | Dusk scenes in the lit place | Black & White's miracles **[from memory]**; our 2026-10-29 Lamp | M |
| 5 | **Gifts of materials** (drop a fallen branch heap, a stone, clay from the pond edge) | Set a material down on the land | A material near the village draws its craft: stone, a quarry or a wall; clay, a kiln; reeds, thatch | Kids climb the stone pile; potters show off pots (unblocks B58.a-b) | They carry it off and build | Reus (resources draw projects), Black & White (wood miracle) | M (`Deposit` and `KindFor` exist) |
| 6 | **Seeds that suggest a use** (an orchard row, a reed bed, a dye meadow) | Plant from the seed pouch (#726) in a line or patch | They tend it and build its keeper: an orchard shed, a dyer, hives | Pick apples, run from a bee (unblocks B58.e) | The first harvest | Reus; our 2026-11-02 seed pouch | M |
| 7 | **Show a need** (the people's wish shows in the world, the Keeper answers with nature) | See a dry field, a sad empty bank; mist, shape or plant there | They then build the thing that need wanted | Depends on the answer | The need is a scene (a farmer looks at cracked soil), not a card | Black & White's desire flags ([wiki](https://blackandwhite.fandom.com/wiki/Village_Store)) | M (wishes were dropped 2026-11-10: "wishes: not for now") |
| 8 | **Wear a path** (draw a finger along the ground) | Trace a line between two places | Walk it, then cobble it, and homes face it | Hopscotch on it (B58.f), stop to talk | The first walkers take it | Tiny Glade ([PC Gamer](https://www.pcgamer.com/games/city-builder/tiny-glade-review)) | S-M (PathNet wears paths already) |
| 9 | **Clear a spot** (the Spade on trees and bushes, already in build) | Pluck trees near the village | They expand into the clearing | Kids play in the new open ground | Building on it | Terra Nil (clearing as a phase verb) | S (it is #730) |

Ideas left out: **weather on demand** (storms were ruled out, 2026-11-02, and the mister already covers rain);
**inspire a project directly** (a menu of projects is placing by another name); **blueprints or marks of a building**
(Fata Deum style; it is the Keeper placing).

### 6.1 What makes a kit, not a list

Terra Nil's lesson (§4): a phase kit works when **the verb changes**. Camp: grow nature. Village: **shape where they
live** (ground, water, the wild places). So the strongest small kit is one tool with two or three gestures, not
nine tools:

- **The Spade grows up at the village** (idea 1, 2 and 9 in one): pluck (today), level or raise (ground), press a groove
  (water).
- **The seed pouch** already exists and can carry idea 6 by what the land picks (keepers-hands §8).
- **Idea 3 (keep a place wild)** is the cheapest new thing and the most "Keeper": the people's commons is a place
  they play, which the core rule loves.

## 7. Recommendation

1. **The canal is the middle way (§5.3): the Keeper shapes the ground and the water's path, the people dig, line and
   use the ditch.** It keeps "we do shape it" and "the people place things" both true. The people also dig a short
   ditch on their own later, so no box goes without.
2. **The duck pen, fish pond and weir are built by the people, sited by Settle**: the pen on the shore first (it needs
   nothing), the fish pond and the weir off the ditch once it exists. For P9 now, if the ditch waits: the pen on the
   shore and the weir across a bay, the fish pond a step off the pond.
3. **The airship's building is the people's**, built as the town grows, never gated on a Keeper act.
4. **The second-phase kit: grow the Spade's gestures at the village** (ground, water, clearing), plus **keep a place
   wild**. Leave light, materials, needs and paths for later; they each bring back old content (lamp, outcrop,
   wishes), and the user turned some down before.
5. Build one shared **"the people's project"** scene (gather, work over days, a first use everyone watches) and use it
   for the ditch, the fish pond, the weir and the airship's building.

This is a new feature in the Polish phase (2026-11-01); the user has confirmed such additions one at a time before
(P9, P10, Seeds and bushes).

### Questions only the user can answer

1. The canal: middle way (you shape the path, they dig), pure shaping (the Keeper's line is the canal), or pure building
   (they choose where, you do nothing)?
2. For P9 now: re-site the fish pond and the weir to the pond until a ditch exists, or wait for the ditch?
3. Which kit ideas to keep (the shortlist is 1, 2, 3 and 9 as Spade gestures plus a wild place)?
4. Should the old shaping answers come back through these tools (bridges, watermill, quarry and kiln from materials,
   orchard and dyer from seeds)?
5. Is this wanted now, inside Polish, or after P9 and P10?
6. The airship's building: built by the people as recommended? Its era, how it leaves the box, what it is made from,
   its look (#454) and its launch and return scenes are for the P10 spec (#453).

## Sources

- [Black & White: Village Store (desire flags)](https://blackandwhite.fandom.com/wiki/Village_Store)
- [From Dust (Wikipedia)](https://en.wikipedia.org/wiki/From_Dust)
- [Godus wiki: abodes and plots](https://godus.fandom.com/wiki/Manor)
- [Reus introduction (Gamepressure)](https://guides.gamepressure.com/reus/introduction/z04fcc)
- [The Universim, Steam discussion on house placement](https://steamcommunity.com/app/352720/discussions/0/1735468693705041032)
- [Before We Leave preview (TechRaptor)](https://techraptor.net/gaming/previews/before-we-leave-preview)
- [Terra Nil wiki: tiers](https://wiki.terranil.org/w/Tiers)
- [Timberborn: dams and dynamite (ModDB)](https://www.moddb.com/games/timberborn/news/beavers-discover-dams-dynamite-in-time-for-droughts)
- [How Townscaper works (Game Developer)](https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making)
- [Tiny Glade review (PC Gamer)](https://www.pcgamer.com/games/city-builder/tiny-glade-review)
- [Islanders (Steam)](https://store.steampowered.com/app/1046030/ISLANDERS/)
- [Fata Deum early access (PC Gamer)](https://www.pcgamer.com/games/sim/fata-deum-is-an-early-access-god-sim-inspired-by-black-and-white-but-i-wish-i-was-just-playing-black-and-white-instead)

In the repo: `docs/decisions.md` (rows dated above), `docs/design/keepers-hands.md` §3 and §8,
`docs/design/placed-life.md` §7, `docs/research/18-placed-life-and-ecosystems.md` §5.5, `docs/design/people-and-things.md`
(village and town), `docs/design/kit-redesign.md` §1, `docs/design/landscaping.md`, `docs/research/20-house-expeditions.md`,
`AquariumCiv/Assets/Game/Sim/Settle/Settlement.cs`, `Encyclopedia.cs`, `Sim/Eco/EcoState.cs`, `UI/GameHud.cs`.
