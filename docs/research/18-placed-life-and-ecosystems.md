# 18 — Life you place, and what the people do with it

Research date: **2026-11-07**. The user, after playing with the Keeper's kit:
*"Why are the bunnies just coming in? You should be the one placing them, once you've put enough water to allow
for them. Also berry bushes: one bunch, and you choose where. Same with water: over time you water and water and get
a little pond, and then you place the ducks and then the fish. Which ecosystems could we grow? Forest, then deer and
people hunting deer. What interactions can people have with deer? They go hunt them, with a nice animation. Same
with fish and fishing. Slowly your town grows, uses the resources you've given them, and interacts with them in a
cool way."*

This is research for a discussion. It is not a plan, and nothing here is decided.

Markers: **[V]** read in the source, **[S]** secondary or search summary, **[K]** a game or fact as I know it, not
re-checked, **[code]** read in the repo, **[est]** my guess to test. ★ = my recommendation.

---

## 0. TL;DR

- **It reverses two earlier decisions, and I think rightly.** keepers-hands §3.2 removed the Herd tool (*"animals
  come by themselves when the land can hold them"*), and research 15 §5 records *"no bush placement (the user didn't
  like it)"*. Today rabbits arrive on their own once the meadow is big enough, and bushes spread on their own up to a
  cap of 60 [code: `EcoRabbitTests`, `EcoBushes`, PR #399]. The new idea makes those moments the player's doing.
- **It's what real keepers do.** You don't put fish in a new tank. You **cycle** it first, then add a few hardy fish,
  wait, and add more. Stocking a new tank too soon is common enough that the hobby has a name for what goes wrong:
  *new tank syndrome* [S]. "The land has to be ready before the jar opens" is how a real keeper thinks, so it fits
  pillar 1 (a real terrarium keeper, not a god).
- **The model ★ — "the land opens the jar":** each species has a habitat need (meadow, forest, open water). When the
  land meets it, that species shows up in the kit, ready to place. You choose **where** to put a founding group. They
  breed up to what the land holds. That's Viva Piñata's *visit → resident* ladder [S], except that the player places
  the animal instead of waiting for it to wander in.
- **Place the big ones, let the small ones come ★.** The Keeper places the few species that change what the people do
  (berry thicket, rabbits, deer, ducks, fish). Small life (crickets, songbirds, frogs, dragonflies, butterflies)
  still arrives by itself. So placements stay few and each one matters, and the box still comes alive while you're away.
- **Ecosystems:** meadow → woodland → pond and wetland in the camp; the stream or canal and the fields in the village;
  orchard, bees and kept animals by the town (§4). In the village, the people **tame what you gave them** (ducks
  penned, a fish pond, a pen of goats), so care moves up as it does with coppicing.
- **Interactions are the payoff (§5).** Every species gets at least one way the people *use* it (hunt, fish, gather)
  and one way they just *live with* it (kids chase ducklings, everyone stops to watch a stag drink at dawn). Each one
  is a beat sheet in research 17's style: the verb touches its target, and the target answers.
- **Nothing dies out (pillar 4).** The hunt floor exists already [code: `SimCore.HuntFloor`]. Over-hunted animals get
  **shy** (they keep farther from the camp) and never go extinct, and the people learn restraint ("leave the does in
  spring") as a story beat.
- **The catch:** this is a new feature, and we're in the polish phase ([[project-polish-phase]]). It needs a new
  milestone. Your call.

---

## 1. What exists today [code]

| Thing | How it works now |
|---|---|
| Rabbits | Rabbit tracks show first; rabbits arrive by themselves once there's enough meadow; the camp gate is 20 % meadow plus rabbits (keepers-hands §2.2). Hunts lower the count but never below `HuntFloor`; hides dry, then become tents |
| Berry bushes | They grow out of the meadow on their own into thickets (cap 60, a no-bush ring round the fire, PR #399). Berries follow the seasons, and a picked bush recovers over a few days (`EcoBushes`) |
| Pond | Misting raises the water table; the lowest hollow turns into a pond. People fish there already (`LifePlanner.Fish`, `FishingView`, `FishBeats`, #364) |
| Frogs | Sound only (`FrogCroak`) |
| Deer, ducks | Not in the game |
| Kit | Cloth, mister, seed (one), tweezers; the stick comes in the village |

So the change is mostly **who starts each population**, plus new species and their animations. The ecology layers,
the hunt and fishing already exist.

---

## 2. Reference games

| Game | How life gets into the world | What we take | What we avoid |
|---|---|---|---|
| **Viva Piñata** [S] | Each species has **visit** requirements (what makes it come in), **resident** requirements (what makes it stay) and **romance** requirements (a pair plus a house). The Squazzil visits once there's a hazelnut tree, and stays once it has eaten 3 hazelnuts. The Sparrowmint visits once there are 2 resident Whirlms | **A visible ladder**: one species readies the land for the next. A species' needs read as plain land facts | Its many houses and shop menus |
| **Reus** [S] | Giants place plants, animals and minerals; humans settle near them and use them. Humans also **get greedy** and go to war | **People come to and use what you placed.** Placement combos (an animal next to a plant boosts both) | Greed, punishment and war (pillar 4). Reviewers called it plodding and hard to care about, because the people were only a score |
| **Equilinox** [S] | You place plants and animals; each species needs the right environment and enough food, and happy ones breed | **Needs + breeding**: you place a few and they multiply | The points economy |
| **Terra Nil** [S] | You restore biomes in stages; in the end, animals return to the habitats you made | The reward of seeing animals in land you built | Animals as a checklist to complete |
| **Banished** [K] | A hunter's lodge in a forest: deer numbers depend on the forest around it, and over-hunting thins them | **Local habitats**: the deer are where the trees are | Starvation spirals |
| **Animal Crossing, Stardew** [K] | Fishing as a small toy: the shadow, the wait, the bite | The bite rhythm (we already have it, #364) | Fishing as the *player's* chore. Here the **people** fish and the player watches |
| **WorldBox** [K] | Spawn anything, anywhere, any number | — | No limit means no meaning. Our jar opens only when the land is ready, and only for a founding group |

**What they share:** the strongest examples (Viva Piñata, Equilinox) tie each species to a **need you can see**, and
make the arrival a **moment**. Reus shows the other side: when the people are only a score, nobody cares what you
placed. Our people are characters, and that's where the advantage is.

---

## 3. The rule: the land opens the jar ★

### 3.1 The loop
1. **The land gets ready.** The player keeps doing what they already do (misting, tending the tree, raising the
   water). The habitat for a species grows.
2. **The kit says so, without a bar.** A small creature icon wakes up in the kit rail (it wiggles once, like the
   kit's other first-time reveals). In the box there's a hint the people notice too: the hunters look at the empty
   meadow and talk with their hands, or a child crouches at the empty pond.
3. **The player places.** Pick the species, then tap a spot. Spots the animal can live in glow softly while the tool
   is in hand; the rest stay dark. Effect only, no hand (keepers-hands §1): a pair of rabbits drops lightly from the
   rim and shakes off; fish arrive the aquarium way, as a bag that floats on the pond for a moment and then opens.
4. **The people react at once** (the first-mist rule): they gather, point, a child runs after the rabbits. The glass
   voice has a line ("the sky sent game"). It goes in the history.
5. **The population grows** toward what the land holds. The player has made the start; the land does the rest.

### 3.2 Limits
- **One founding group per species ★**, the way there's one seed. The jar empties after you place it. That keeps it a
  moment, not a resource. [est: rabbits 2, deer 3 (a stag, two does), ducks 2 + 3 ducklings, fish one school of ~6]
- **Re-stocking** only when a species has sat at its floor for a long time [est: 2 game years]: the jar opens again.
  It's a gentle way back, not a shop.
- **Too soon isn't possible:** a species that isn't ready simply isn't in the rail. No failure and no dead fish.
  New tank syndrome is in the theme, never in the mechanics.

### 3.3 Where you place it matters (lightly)
These choices make placement worth more than a tap, and none of them can go badly:
- **Near the camp** → short walks; hunters and gatherers work more often and you see more scenes. **Far away** → the
  people go on longer outings (a little hunting party crossing the box, which is a scene too).
- **Berry thicket by the tree** → the real nurse-shrub effect (research 15 §3): the tree's weeds grow a little slower
  under the thicket [est]. A small reward for a thoughtful spot, not a requirement.
- **Ducks or fish** → which end of the pond. The reeds grow where the ducks are; the fishers walk to the fish.
- **Deer at the forest's edge or deep in it** → seen often at the edge; shy and rare deep in.

### 3.4 What still comes by itself
Crickets with the first green, songbirds on the bushes, jays with the tree's seeds (they plant the forest, research
15), frogs and dragonflies at the pond, later butterflies and bees on wildflowers. These are **signs**, not
resources: the people don't depend on them. Absence still brings life, so pillar 4 and keepers-hands §5.4 hold.

---

## 4. Which ecosystems we can grow

Read across a row: what makes the land ready, what the Keeper places, what comes by itself, what the people get.

| # | Ecosystem | Ready when | The Keeper places | Comes by itself | The people get | Era |
|---|---|---|---|---|---|---|
| 1 | **Meadow** | First green spreading; ~10 % meadow [est] | **The berry thicket** (one bunch; it spreads slowly from there) | Crickets, songbirds | Berries | Tribe → camp |
| 2 | **Meadow with game** | ~20 % meadow (today's camp gate) | **Rabbits** (a pair) | Rabbit tracks *after* you place them | Meat, **hides → tents** (the camp starts) | Camp starts |
| 3 | **Woodland** | The Keeper's tree is grown and the jays have spread a wood [est ≥ 8 grown trees] | **Deer** (a stag and two does) | Jays, squirrels, an owl at night, mushrooms in autumn | Venison, hides, **antlers** (tools, the chief's mark), the bow becomes a craft | Camp |
| 4 | **Pond** | The water table breaks the surface; the pond reaches a minimum size [est] | **Ducks** first | Reeds, frogs, dragonflies | Eggs, feathers (fletching for the arrows) | Camp → village gate |
| 5 | **Living pond** | Ducks settled + reeds grown [est 1 game year] | **Fish** (one school) | A heron sometimes | Fish: spears, then baskets and a weir | Camp → village |
| 6 | **Stream and canal** | The stick's first canal flows (village) | (nothing new, or **trout** upstream) | Kingfisher | The **weir and basket traps** (the people build it), the mill | Village |
| 7 | **Fields** | Farms | — | Mice, then an owl or barn cat | Grain; weeds for the tweezers | Village |
| 8 | **Kept animals** | Village + the species you gave them | — (the people tame them) | — | Penned ducks; a fish pond; goats if the box has a hill | Village → town |
| 9 | **Flower meadow and orchard** | Seed pouch on meadow (keepers-hands §8.5) | **Bees** (a hive in an old tree) [maybe] | Butterflies | Honey, wax; the beekeeper | Town |

Notes:
- **The order is the ladder.** Rows 1–5 build on each other, like Viva Piñata's species that need other species
  first: ducks before fish (the reeds the ducks bring make the shelter fish need), rabbits before deer (a meadow before
  a wood). It gives the camp a clear next step at every visit.
- **Predators: left out ★.** A fox or wolves would balance the rabbits and deer for real, but they're a threat to read
  and a loss to watch. Maybe a heron or an owl as a sign, never something that takes from the people's stock.
- **Other settings later:** the oasis box (research 14) would swap these for desert life (lizards, goats, date palms,
  a spring with fish). Same rule, new rows.
- **Domestication (row 8) is the village's version of "care moves up".** The camp hunts the wild animals the Keeper
  gave; the village keeps them. It also follows real history: Neolithic farmers kept animals that started out wild [K].
  Goats or sheep fit only a box with a hill; ducks and a fish pond fit any box with water.

---

## 5. What the people do with them (the heart of it)

Rule from research 17: **the verb touches its target, and the target answers.** Each species gets **one use** (they
take something) and **one way of living with it** (they just enjoy it). The second kind makes the box feel lived in,
and it costs nothing in balance.

### 5.1 Berry thicket
| Scene | Beats |
|---|---|
| **Picking** (use) | Walk up with a basket → reach into the bush, the branch bounces back → the basket fills (berries visible) → carry it home on the hip. The picked bush looks a little bare (exists, `EcoBushes`) |
| **The kid who eats them** (living) | A child picks with the gatherer, eats every second berry, purple mouth; the grown-up takes the basket off them |
| **Birds** (living) | A songbird lands on the bush, the gatherer shoos it off with a hand flap, it comes back |
| **Drying** | Berries spread on a hide mat in the sun by the tent; in autumn, the dried berries are the winter food |

### 5.2 Rabbits
| Scene | Beats |
|---|---|
| **Snares** (use) | A hunter kneels, sets a loop at a run; next morning they check it (often empty, a shrug; sometimes a catch, held up) |
| **The chase** (use, exists in part) | Hunters close in slowly, a rabbit bolts, others scatter across the meadow (research 17 already asks for this) |
| **Hides → tents** (exists) | Hides on a drying frame → sewn by the fire → a new tent goes up |
| **Kids chase bunnies** (living) | A child runs after one and never catches it; falls over; the rabbit sits and looks back |

### 5.3 Deer ★ (your example)
| Scene | Beats |
|---|---|
| **The hunt** (use) | Two or three hunters leave the camp in a line, bows on their backs → at the forest's edge they crouch, one points → slow steps, everyone freezes when the stag lifts its head → the shot: an arrow arcs → **hit**: the herd bolts into the wood, the stag stumbles and lies down (no blood, it just lies still: cute, literal, not gory); **miss**: the arrow thunks into a tree, the deer bolt, the hunters stand up and shrug → the carry: the stag hangs from a pole between two hunters, legs up, back to the camp → the camp gathers; that night the fire is bigger and there's dancing |
| **The antlers** (use) | A stag's antlers go up over the chief's tent: a visible mark of the first deer hunt. Later, shed antlers found in spring (a child brings one back proudly) |
| **Dawn at the pond** (living) | At first light the does come to drink; the people who are awake stop what they're doing and watch; a child holds still. Nobody hunts at the water [a people's rule] |
| **The hunt as a choice** | Fits the visit loop's "one owned choice": *"The hunters saw the white stag. Hunt it, or let it be?"* Hunt: a feast and great antlers. Let it be: the stag shows up at dawn on later visits, and the people tell stories about it |

### 5.4 Ducks
| Scene | Beats |
|---|---|
| **Eggs** (use) | A gatherer wades into the reeds, bends down, comes up with an egg → **the mother duck charges** and chases them out, flapping → they run back up the bank with the egg held high (comedy) |
| **Feathers** (use) | The fletcher picks up feathers on the bank; arrows with feathers show on the hunters' backs from then on (a small visible upgrade) |
| **Ducklings in a line** (living) | Spring: ducklings follow the mother in a row; a child walks behind them, copying the waddle |
| **Feeding** (living) | Kids throw crumbs; the ducks come fast; one child runs away from them |

### 5.5 Fish
| Scene | Beats |
|---|---|
| **Spearing** (use, camp) | Wading knee-deep, spear raised, very still → the strike, a splash → held up wriggling, or nothing and a wet face |
| **Line from a log** (use, exists: #364) | The bite rhythm we have: wait, bob, yank, catch |
| **Basket traps and the weir** (use, village) | The people build a fence of stakes and woven baskets where the canal leaves the pond (real: Mesolithic weirs and basket traps from ~6400 BC [S]). Every morning, two of them lift the basket: fish flapping inside, or empty. It's a daily ritual, the kind you can watch on any visit |
| **Smoking** (use) | Fish on a rack over a smoky fire; smoke drifts up and out of the open top |
| **Swimming** (living) | Summer: kids splash in the shallows; the fish scatter as shadows under the water |

### 5.6 Everything at once
The strongest scenes link species and people: the deer come to drink where the ducks are, and the ducks go up in a
cloud; the fishers take the ducks' feathers for their arrows; the kids who chase rabbits in spring chase ducklings in
summer. That's what *"interacts with those resources in a cool way"* looks like as a box you watch.

---

## 6. Balance without loss (pillar 4)

| Risk | Answer |
|---|---|
| The people hunt a species out | The **floor** (exists for rabbits: `HuntFloor`). The same for every species |
| A floor still looks bad | Over-hunted animals get **shy**: they keep to the far side of the box and come out at dawn. You see fewer of them; none are gone |
| The player feels they placed animals only to have them killed | The people learn **restraint** as a story beat (like coppicing): after a lean season, a notable rules "leave the does in spring", and the hunts pause in spring from then on. The players see that their gift is looked after |
| A chore of re-stocking | One placement per species; re-stocking only after a long time at the floor (§3.2) |
| Too much going on at once | Species come one at a time, gated by the land; a big village has more scenes because it has earned them (the visit loop's pacing rule) |
| Gore | No blood, ever. A caught animal lies still; the carry, the feast and the antlers tell the story |

---

## 7. The arc, revised [est, to tune]

| # | Beat | The Keeper | What happens |
|---|---|---|---|
| 0 | Barren | Wipe | As today |
| 1 | First mist | Mist | As today |
| 2 | **First berries** | **Place the berry thicket** (your spot) | The gatherers' first job; the kid with the purple mouth |
| 3 | **Game** | **Place the rabbits** | The first hunt, hides, the first new tent: **the camp starts** |
| 4 | The one seed | Plant and tend | As today |
| 5 | The forest | — | Jays spread it |
| 6 | **Deer** | **Place the deer** | The first deer hunt, the antlers over the chief's tent, dawn at the water |
| 7 | The water rises | Mist; wipe the droplets | As today; reeds and frogs come by themselves |
| 8 | **Ducks** | **Place the ducks** | Eggs, feathers, ducklings |
| 9 | **Fish** | **Place the fish** | Spearing, the line; the village gate |
| 10 | Village | Stick: the canal | **The weir**, penned ducks, a fish pond |

That's five placements across the camp, each one a scene. The camp gets a "what next?" at about every visit, which
the visit loop wanted.

---

## 8. The tool

- **One "life" slot in the kit rail ★**, next to the seed. Tap it and a small row shows the species that are ready
  (an icon each: a rabbit pair, a stag, a duck, a fish). Most of the time it holds one or none. Same gestures as the
  seed: choose, then tap a spot.
- The land shows where it can live (a soft glow on the valid ground) only while the tool is in hand.
- The alternative is the seed pouch rule (*"the land picks"*: tap meadow → rabbits, water → fish). It's simpler, but
  you asked to choose ducks *then* fish, and with two species ready for the pond you can't pick. So: a small chooser.
- Debug panel (CLAUDE.md rule): "ready every species", "place X here", "jar again", "to the floor", "white stag now".

---

## 9. Cost and risks

| Area | Work | Size [est] |
|---|---|---|
| Sim | Readiness per species; the jar; founding group → population to capacity; floors; shyness; the restraint beat. Rabbits and bushes switch from arriving by themselves to being placed | M |
| Sim | Deer, ducks, fish as small populations (counts, plus a few individuals for the view) | M |
| View | Deer and duck models with walk, graze, bolt and swim animations; fish shadows; the drop-in and the floating bag | L (art) |
| Life | The scenes in §5: the deer hunt and the carry, eggs and the mother duck, spearing, the weir, the dawn watch | L, in AnimBench and AnimLab, one beat sheet each (animation-specs.md) |
| UI | The life slot and its chooser; the glow | S–M |
| First run | Beats 2–3 change (the thicket and rabbits are placed): `Feedback2Playthrough`, `EcoPhonePlaythrough` and the slice smoke test need updating | M |
| Assets | CC0 animals: Quaternius has animated low-poly animals (deer, fox and others) under CC0 [K, check and log in assets-log.md]; ducks may have to be made. KayKit and Kenney have few animals [K] | ? |

**Risks:** the art and animation cost is the biggest; deer and ducks have to read at ~55 px dolls on a phone (the
default camera, decisions.md). Five placements might feel like a checklist if the scenes behind them are thin, so
it's better to do fewer species well (§10, Q6).

---

## 10. Questions for you

1. **Placed or arrived:** the Keeper places the big ones (thicket, rabbits, deer, ducks, fish) and the small life
   comes by itself (§3.4) ★ · everything is placed · only the ones you named?
2. **One founding group, then the land does the rest** ★ · or more placements over time (a second pair, more fish)?
3. **Berries:** one thicket you place, which then spreads slowly on its own ★ · one thicket that never spreads (the
   people only ever have that bunch) · several you place one by one?
4. **Predators:** none, only signs (a heron, an owl) ★ · a fox or wolves as a real balance?
5. **The village tames them** (penned ducks, a fish pond, goats on a hill box) ★ · they stay wild for the whole game?
6. **First build:** the deer (the hunt, the carry, the antlers, dawn at the pond) as the showcase, with the rabbit
   and thicket placements, since they rework what exists ★ · the pond pair (ducks, fish) first · all of it as one
   milestone?
7. **Polish phase:** this is new work. Make it a new milestone after the polish issues, or pause the polish for it?

## Sources
- [Piñata Journal wiki: piñata visit / resident / romance stages](https://pinatajournal.wiki/wiki/Piñata) [S]
- [Viva Piñata wiki: Whirlm](https://vivapinata.fandom.com/wiki/Whirlm) [S]
- [TrueAchievements: Viva Piñata TIP walkthrough (requirement categories)](https://www.trueachievements.com/game/Viva-Pinata-Trouble-In-Paradise/walkthrough/3) [S]
- [Gamepressure: Squazzil, Sparrowmint requirements](https://www.gamepressure.com/vivapinata/sour-sherbat-sparrowmint-squazzil/z3892) [S]
- [Twinfinite: Reus review](https://twinfinite.net/reviews/reus-giant-driven-gameplay-reshapes-god-game-genre-review/) [S]
- [GameWatcher: Reus review](https://www.gamewatcher.com/reus/review) [S]
- [Fanatical: Equilinox](https://www.fanatical.com/en/game/equilinox) [S]
- [Duke FAQ: cycling your tank and new tank syndrome](https://users.cs.duke.edu/~narten/faq/cycling.html) [S]
- [Aquariadise: new tank syndrome](https://www.aquariadise.com/new-tank-syndrome/) [S]
- [Nature Aquariums: cycling your aquarium](https://natureaquariums.com/blogs/news/the-nitrogen-cycle-explained) [S]
- [Proceedings of the Prehistoric Society: stationary wooden fishing structures in Stone Age Denmark](https://www.cambridge.org/core/product/5A369D414D231A844450162B300CD53D) [S]
- [Fiveable: Mesolithic technologies and subsistence](https://fiveable.me/world-prehistory/unit-6/mesolithic-technologies-subsistence-strategies/study-guide/HIRhMiITlRm5tO6t) [S, study guide]
