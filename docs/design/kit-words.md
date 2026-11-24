# The Keeper's kit: every word, old and new (#668 phase 1)

For the user to sign off. Every new line is `[est]` until then. The look is in `kit-mockups.html`; the inventory and the
build plan are in `kit-redesign.md`.

## The voice

- The kit talks to the Keeper (you), in the present tense. Hints are one instruction; notes are one fact.
- A tool's name is a plain noun. Under the name, two to four words say what it does ("wipes the glass").
- A hint says what to do with the finger, then one short tail about the view. The tail is the same everywhere:
  "Two fingers move the view." (#587: with a tool in hand, one finger is the tool and two fingers pan, turn and zoom.)
- Life is named as a group ("the rabbits"), never as a species code or a jar state. No "unlock", "locked", "slot", "cap".
- Closed world: only the box, the land, the glass, the people and the Keeper's tools. Nothing outside the tank.
- Counts are a small "×2" on the tile, never a sentence.
- One tree is special: the first seed grows **your tree** (the Keeper's tree, never plucked). Later seeds from #726 grow
  ordinary **young trees**. Today's lines say "the box's one tree", which #724 (B3, more seeds) makes untrue.

Checked before writing: `docs/decisions.md` (2026-11-02 kit, 2026-11-08 life slot, 2026-11-14 #724), and
`docs/overnight/open-questions-signoff.md` rows 18, 37, 43, 54, 59 (the village line shows only for later steps, not the
first; kept). Nothing below re-opens a signed-off call.

## 1. The bottom bar (the Kit button)

| Where (file:line) | Old | New `[est]` | Why |
|---|---|---|---|
| `UI/GameHud.cs:172` | Kit | Kit | Keep. |
| `UI/GameHud.cs:172` (box without living land) | Clean | Clean | Keep (no kit in that box). |
| `UI/GameHud.cs:172` | Done (cloth in hand) | **[cloth] Cloth** · Done | #661: the bar shows what is in hand. |
| `UI/GameHud.cs:165` `PutAway` | ✕ Put away | **[icon] Mister** · Put away | #661. Same for every tool: "[icon] Tweezers · Put away", "[icon] Trowel · Put away". |
| (same) seed in hand | ✕ Put away | **[seed] Seed** · Put away | #661. |
| (same) life in hand | ✕ Put away | **[rabbits] The rabbits** · Put away | #661, names the group as #643's hint does. Longest: "The wolves", "The bushes" (fits at 375 wide, see the mock-ups). |
| `UI/Glass/GlassPieces.cs:70` | New (tag on Kit) | New | Keep. |
| `UI/GameHud.cs:889` dot on Kit | (a dot, no word) | Kit **·** with a soft pulse | #660: "a dot that reads". |

## 2. The kit sheet

| Where | Old | New `[est]` | Why |
|---|---|---|---|
| `UI/GameHud.cs:238` | The Keeper's kit | The Keeper's kit | Keep. |
| (new) section label | (none) | Tools | Mock-ups A and B: the tiles are grouped. |
| (new) section label | (none) | Ready | #662: ready jars first. |
| (new) section label | (none) | Still to come | #662: shut jars apart, with a label. |
| `UI/GameHud.cs:243` | Cloth | Cloth / *wipes the glass* | The tile says what it does. |
| `UI/GameHud.cs:244` | Mister | Mister / *wets the land* | |
| `UI/GameHud.cs:245` | Tweezers | Tweezers / *pull weeds* | #730: saplings move to the new tool. |
| (new, #730) | (none) | **Trowel** / *lifts plants* | Name is the user's call (#730). Candidates from landscaping.md §5: trowel, shears, spade. Trowel fits "lift it out with its roots"; shears would read as cutting. |
| `UI/GameHud.cs:248` | Life (the jar button) | Mock-up A: gone (the jar's contents are tiles). B and C: **Life jar** | "Life" alone read as a mode. |
| `UI/GameHud.cs:364` | Seed | Seed / *grows your tree* (first seed); Seed **×2** / *a young tree* (later seeds) | #724 B3: later seeds are ordinary trees. |
| (new, #726) | (none) | Bushes **×2** / *grow berries* | Earned bushes. The first bush jar stays "Bushes". |
| `Sim/Eco/EcoModel.JarNeed.cs:16-21` `PlainName` | Bushes, Rabbits, Deer, Ducks, Fish, Wolves | same | Keep (tests read them). |
| (new) under each ready group | (none) | Rabbits / *for the meadow*; Deer / *for the wood*; Ducks / *for the pond*; Fish / *for the pond*; Wolves / *for the wood*; Bushes / *grow berries* | Says where it goes, which is what the tap needs. |
| (new) under each shut jar | (none, need only after a tap) | a short need: Bushes *needs your tree*; Rabbits *needs a meadow*; Deer *thicker wood*; Ducks *needs a pond*; Fish *needs reeds*; Wolves *needs deer*. The full `JarNeed` line goes in the note when the jar is tapped. | #662: the need shows without a tap. Tile lines are kept to about 15 letters so they fit an 84 pt tile at 375 wide. |

### The note (one place, one line at a time)

Today the kit has three notes (`UI/GameHud.cs:499-501`) that list the tools. The tiles now say that, so the note says
the one thing that matters now, in this order:

| Where | Old | New `[est]` |
|---|---|---|
| `UI/GameHud.cs:499` (seed waiting) | Cloth wipes the glass. Mister wets the land. Tweezers pluck what overgrows. The life jar holds the seed: plant the one tree. | Your tree's seed is ready. Tap it, then the land. |
| `UI/GameHud.cs:500` (tree planted) | Cloth wipes the glass. Mister wets the land and the tree. Tweezers pluck what overgrows. | (no tool list) Mist your tree when the land looks dry. |
| `UI/GameHud.cs:501` (no seed yet) | Cloth wipes the glass. Mister wets the land. Tweezers pluck what overgrows. A seed comes when the land can hold a tree. | A seed comes when the land can hold a tree. |
| (new) a group is ready | (none) | The rabbits are ready. Tap them, then the meadow. |
| `UI/GameHud.cs:390` a shut jar tapped | its `JarNeed` line | same lines (below), shown in the note |
| `Sim/SimCore.LifeJar.cs:67` | The village waits for: more trees (3/8) · a pond | same (keep; signed off in row 59) |

### What a shut jar waits for (`Sim/Eco/EcoModel.JarNeed.cs`)

These already read well and tests pin several of them (`JarNeedTests`, `LifeSlotPlayTests`, `Playtest1BPlaythrough`).
Only two change.

| Line | Old | New `[est]` |
|---|---|---|
| 25 | The pond is ice. Ducks will come when it thaws. | same |
| 36 | Rabbits need the bushes first | same |
| 41 | Bushes need the seed in the ground first | Bushes need your tree planted first |
| 42, 44 | Bushes / Rabbits need a meadow · need a bigger meadow | same |
| 46 | Deer need a thicker wood (n/m) | same |
| 48 | Ducks need a pond · need a bigger pond | same |
| 51 | Fish need reeds round the pond (n/m) | same |
| 53 | Fish wait for the ducks to settle in | same |
| 57-61 | Wolves need a herd of N deer · a deeper wood (n/m trees) · a hidden den spot in the wood · a bigger wood to hide a den in · wait for the herd to hold its numbers | same |
| `UI/GameHud.cs:408` `PlainReady` | Rabbits are nearly here | Rabbits come at the season's end (it is when the jar opens) |
| `Sim/Eco/EcoModel.JarNeed.cs:77-85` village pieces | more meadow · rabbits in the meadow · the rabbits to grow · more trees (3/8) · a bigger pond · ducks on the pond · fish in the pond | same |

## 3. Hints (toasts when a tool is taken)

| Where | Old | New `[est]` |
|---|---|---|
| `UI/GameHud.cs:201` `MisterHint` | Hold a finger over the land to mist it. Two fingers move, turn and zoom the view. | Hold a finger on the land to mist it. Two fingers move the view. |
| `UI/GameHud.cs:203` `TweezersHint` | Tap or drag over weeds, vines and saplings to pluck them. Two fingers move, turn and zoom the view. | Tap or drag over weeds and vines to pull them. Two fingers move the view. |
| (new, #730) trowel | (none) | Tap a seedling or a bush to lift it out. Two fingers move the view. |
| `UI/GameHud.cs:205` `SeedHint` | One seed: it grows into the box's tree, anywhere, even on bare sand. Tap the land to choose where it goes. Two fingers move, turn and zoom the view. | First seed: "This seed grows your tree, even on bare sand. Tap where it should go. Two fingers move the view." Later seeds: "This seed grows a young tree. Tap where it should go. Two fingers move the view." |
| `UI/GameHud.cs:285` `LifeHint` via `LifeHintFor` (289-293) | The rabbits: tap the glowing ground to choose a spot. Two fingers move, turn and zoom the view. | The rabbits: tap the glowing ground to pick a spot. Two fingers move the view. |
| `UI/GameHud.cs:284` `LifeWaterHint` | The ducks: tap the water to choose a spot. Two fingers move, turn and zoom the view. | The ducks: tap the water to pick a spot. Two fingers move the view. |
| `UI/GameHud.cs:294` | The wolves: tap a glowing den spot in the wood. Two fingers move, turn and zoom the view. | The wolves: tap a glowing den spot in the wood. Two fingers move the view. |
| (new, #731) bushes from the pouch | (none) | Bushes: tap the glowing ground to pick a spot. Two fingers move the view. |
| `View/LifePlaceView.cs:59` | Tap again to set the rabbits down here. / Tap the water again to set the ducks down. | same |
| `View/SeedPlaceView.cs:24` | Tap again to plant the seed here. | same |

## 4. Toasts when something arrives or happens

| Where | Old | New `[est]` |
|---|---|---|
| `UI/GameHud.cs:495` seed arrives | A seed waits in the life jar, in your kit: the box's one tree. Plant it anywhere, even on bare sand. | A seed is in your kit. It grows your tree, even on bare sand. |
| (new, #660) a jar opens | (none, only a dot) | The rabbits are ready in your kit. |
| (new, #726) earned seed or bush | (none) | The village grew. A seed waits in your kit. / The village grew. A bush waits in your kit. / The village grew. A seed and a bush wait in your kit. |
| `View/SeedPlaceView.cs:120` | The seed is in the ground. Mist it, and keep its weeds back. | same |
| `View/SeedPlaceView.cs:120` (moved) | The seed went in a little way from the glass, where a tree has room. Mist it, and keep its weeds back. | same |
| `View/CareView.cs:979` | The glass is sparkling. / All tidy. Look at that sky! | same |
| `View/CareView.cs:183` | Nothing to clean right now. | same |
| `View/CareView.cs:184` | Drag across the glass to wipe it. | same |
| `View/CareView.cs:183` (box without living land) | Drag across the glass to wipe it. Flick junk and weeds out. | same |
| `View/CareView.cs:882` | Flick it out. / Flick it faster to throw it out. | same |
| `Sim/Hints/CleanHint.cs:19` (box without living land) | Tap Clean to wipe the mess. | same |

## 5. Refusals (a tap that can't work says why, #664)

| Where | Old | New `[est]` |
|---|---|---|
| `View/SeedPlaceView.cs:25` | Not there: the seed can't grow in the hollow or the water. | Not there. A seed can't grow in the hollow or in water. |
| `View/LifePlaceView.cs:177-183` (a tap off the glow) | (silent wobble) | Not there. The rabbits need the glowing ground. (water groups: "...need the water.") |
| (new, #731) | (none) | Not there. Bushes already grow here. / Too close to the fire. / Bushes don't grow in water. / No room for more bushes. |
| (new, #730) trowel on your tree | (none) | Your tree stays. The tweezers keep its weeds back. |
| (new, #730) trowel on nothing | (none) | Nothing to lift there. |
| `View/CareView.cs:589` | The cloth is for the glass. Take the tweezers from your Kit for weeds. | same |
| `View/CareView.cs:590` | Take the tweezers from your Kit to pluck weeds. | Take the tweezers from your Kit to pull weeds. |

## Open for the user

1. The plucking tool's name (Trowel, Shears or Spade, or your own).
2. "Your tree" for the Keeper's tree in player text, or keep "the box's tree".
3. The view tail: "Two fingers move the view." (short) or keep "Two fingers move, turn and zoom the view."
