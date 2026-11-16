# People and things

**Core rule (user, 2026-11-14):** everything in the box is used by the people, for **work** and also for **life**:
kids play on or with it, grown-ups sit, lean, gather, show it off, pray or react. A thing nobody comes near is
unfinished, and so is a thing used only for work. Every new thing ships with its row here, its uses and a debug
cheat that triggers them ([game-design-practices.md](../game-design-practices.md) §15).

This came out of the first outside playtest: watching the people deal with the things of their world was the
clearest pleasure.

**Reviewed by the user 2026-11-14** (#691): the proposals below are approved as written unless marked; the order below is confirmed.

Legend: **W** = work / extract, **L** = life (play, social, ritual, reaction). "Today" is what the code does as of
2026-11-14 (audit of `Life/`, `View/`, `Sim/`). "Add" lists proposed uses. They are proposals, not built, and the
user picks them. **Gap** marks a thing that has no use, or no life use.

## Camp (and the tribe before it)

| Thing | Today | Add | Gap |
|---|---|---|---|
| Fire | W feed, cook, knap, stack wood · L evening sit, chat, stories, eat | warm hands in winter, kids poke sticks in it, roast a berry on a stick, **roast rabbit or deer meat on a spit** | **built** B29.a–d (#692 part 1, `View/CampLife.cs`; clips wait for sign-off) |
| Stew pot | none of its own (only as a Show me place) | W fill, stir, ladle bowls at supper (#676) · L kids peek in, someone sniffs and nods, it stands cold off the fire when no meal is on | **built** B30.a–e (#692 part 1, `View/CampLife.cs` + `MealDirector`; clips wait for sign-off) |
| Stash pile | W tip loads, sort the basket · L hide-and-seek base | L kids count berries, someone sneaks a snack and gets caught | spec B41 (2026-11-15, waits for sign-off) |
| Tents | W raise, mend, sweep, sleep · L watch a raising, visit, chat at the door | L kids peek out, hang a toy or a charm, sit in the doorway at dusk | spec B40 (2026-11-15, waits for sign-off) |
| Hide rack | W scrape hide | L kids duck under it | **Gap (L)** · spec B42 (2026-11-15, waits for sign-off) |
| Fish rack | W hang and take down fish | L kids count the fish, a kid sneaks one and gets caught | **Gap (L)** · spec B42 (2026-11-15, waits for sign-off) |
| Berry bushes / thicket | W gather · L kids eat berries, first-bush inspect | L hide in the thicket, peek at rabbits from it, berry-stained faces | thicket: **Gap** · spec B43, B71 (nest) (2026-11-15, waits for sign-off) |
| Nest (in the biggest thicket, #709) | W a gatherer takes an egg now and then, never the last (B71.d) · L kids creep up, peer in and count the eggs, step back when the bird puts its wings out (B71.c) | — | built (#709 view): cheats in the "Nest (#709)" block (place, spring, lay, take, hatch, chicks fly off, next stage, gatherer takes an egg, kids look, show me); clips wait for sign-off |
| Wild trees | W chop, carry · L woods games, hide-and-seek | L climb a low branch, nap in the shade, lean on the trunk | spec B44 (2026-11-15, waits for sign-off) |
| Fallen branches | W gather | L sword fight with sticks, a stick horse | **Gap (L)** · spec B36 (2026-11-15, waits for sign-off) |
| Keeper's tree | never chopped; charms hang there | L sit under it, touch the bark, kids circle it, **a quiet visit after a death** (the user's favourite). More quiet visits, proposed: a newlywed pair walks there after the wedding, a parent brings the newborn, an old one sits there on their last days, the eldest tells the kids who planted it | **Gap (L)** · spec B32 (2026-11-15, waits for sign-off) |
| Saplings, seedlings, young trees | none | W water or ring a sapling with stones · L a child "plants" a twig beside it | **Gap** · spec B34 (2026-11-15, waits for sign-off) |
| Grass, tall grass | flattened by the hunt; hide cover | L roll in it, lie back and watch the sky, pick flowers, blow a grass whistle | **Gap** · spec B33 (2026-11-15, waits for sign-off) |
| Vines, weeds | trimmed or weeded in the town only | W pull them in the camp too · L kids trip, swing or make crowns (#685) | **Gap (camp)** · spec B53 (2026-11-15, waits for sign-off) |
| Pond | W fish, fetch water, eggs, ice fishing · L swim, splash, ice slide, chase ducks | L skip stones, float a leaf boat, sit with feet in the water | spec B45 (2026-11-15, waits for sign-off) |
| Mist puddles | W scoop · L stomp, wash hands, splash | L look at their reflection, kids race to the next one after the mist | spec B46 (2026-11-15, waits for sign-off) |
| Reeds | hunt cover; eggs at the shore | W cut reeds to weave · L reed whistles, hide in them | **Gap** · spec B47 (2026-11-15, waits for sign-off) |
| Stream bed | water fallback | L kids follow it, build a little dam | **Gap** · spec B48 (2026-11-15, waits for sign-off) |
| Hill | L explore, look at the glass, run up (village on) | L sit on top at dusk, roll down it (camp too) | spec B49 (2026-11-15, waits for sign-off) |
| Feather pole | L pray (#566) | L **tie a new feather after a good event** (the village founded, a newborn, a marriage), kids dare to touch it | **built** B31.a–c (#692 part 1: `SimCore.PoleFeathers.cs`, `View/CampLife.cs`; clips wait for sign-off) |
| Rocks, pebbles | none | L sit on a rock, stack pebbles | **Gap** · spec B35 (2026-11-15, waits for sign-off) |
| Paths | walked and worn | L kids hop or race on them, people meet and chat on the way | **Gap** · spec B50 (2026-11-15, waits for sign-off) |
| Glass | L look, gaze, press hands, tap back, gather | | |
| Rabbits | W hunt · L chase, sighting | L a kid holds a kit | spec B39.a (2026-11-15, waits for sign-off) |
| Deer | W hunt · L watch the arrival, white stag | L watch them graze from the edge, kids creep up and the deer bolt | **Gap (L)** · spec B38 (2026-11-15, waits for sign-off) |
| Ducks | W eggs · L kids chase | L throw crumbs | spec B39.b (2026-11-15, waits for sign-off) |
| Fish | W fish (a shadow noses the hook before a bite, B16.e) · L watch the arrival · L kids kneel at the edge and try to catch one by hand, never do (B39.c, built 2026-11-15, `FishGrabGame`) · the Keeper sees them as shadows and through the glass (B70) | L kids peer into the water | built (#708 view): cheats "Fish: the shadows", "Fish: show them at the glass", "Fish: startle", "Kids try to catch a fish by hand"; clips wait for sign-off |
| Frogs | none (they flee and croak) | L kids catch one and let it go | **Gap** · spec B37 (2026-11-15, waits for sign-off) |
| Wolves | stand and watch; white wolf | L shout and wave torches to drive them off (decided), hold the kids close | **Gap** · spec B55 (2026-11-15, waits for sign-off) |
| Dogs | none in `Life/` | W go on the hunt, guard · L fetch, kids ride, a dog sleeps by the fire | **Gap** · spec B56 (2026-11-15, waits for sign-off) |
| Relics | L relic moments | (being replaced by decorations, #448–#480: each decoration ships with its uses) | |
| Remembering stone | L mourn with a flower | L leave a small gift on a birthday or a feast | spec B51 (2026-11-15, waits for sign-off) |
| Apart camps, boat, lookout tower | W chop, fish, weave, row · L dares, couples at the tower, waves | | |
| Lanterns at the tents | lit at the gather place only | W light each lantern at dusk · L kids chase moths at them | **Gap** · spec B52 (2026-11-15, waits for sign-off) |
| Choice props (charm pole, name stone, baskets, long table, offering basket, goat fence, fish crate, name peg, axe stump, staff, flour sacks, roof bird, woodpile roof) | none of their own (only the Show me place) | each one gets one W and one L use that fits its story (e.g. long table: a shared meal; name stone: kids trace the names) | **Gap (all)** · spec B54 (2026-11-15, waits for sign-off) |

## Village and town

| Thing | Today | Add | Gap |
|---|---|---|---|
| Fields | W sow, hoe, weed, harvest · L inspect, bless, watch | L kids run between rows, a scarecrow made together, harvest dance | spec B57 (2026-11-15, waits for sign-off) |
| Homes | W sleep, sweep, washing, kindling, brew, babies · L sit out, visit, kids play | L repair or decorate the house over time (fits #686) | spec B57.d (2026-11-15, waits for sign-off) |
| Well | W fetch water | L gossip at the well, kids drop a pebble and listen | **Gap (L)** · spec B57 (2026-11-15, waits for sign-off) |
| Windmill | W mill, open sails | L kids watch the sails, race the sails' shadow | **Gap (L)** · spec B57 (2026-11-15, waits for sign-off) |
| Lumber mill / timber yard | W saw, stack | L sit on the log pile, balance on a log | **Gap (L)** · spec B57 (2026-11-15, waits for sign-off) |
| Market | W mind stall, haggle · L shop | | |
| Tavern | W serve, barrels · L drink, sing | | |
| Church / shrine | W ring the bell · L service, sit with the old | | |
| Workshops, smithy, kiln | W hammer, weave, pot, stone · L inspect | L kids watch the sparks, show off a new pot | **Gap (L)** · spec B58 (2026-11-15, waits for sign-off) |
| Guildhall | L meeting | | |
| Bakery | W knead | L kids wait at the door for a crust | **Gap (L)** · spec B58.c (2026-11-15, waits for sign-off) |
| Pasture, hives | W herd, tend hives | L kids pet goats, run from a bee | **Gap (L)** · spec B58 (2026-11-15, waits for sign-off) |
| Green / plaza | W trim · L speech, court, dance, festival, play, lanterns | | |
| Streets, midden | W sweep | L kids play hopscotch, people stop to talk | **Gap (L)** · spec B58 (2026-11-15, waits for sign-off) |
| Building site | W direct, inspect, saw | (both life uses dropped by the user; work only for now) | |
| Bridges | walked | L lean on the rail, drop sticks and race them | **Gap** · spec B58 (2026-11-15, waits for sign-off) |
| Fences, pens | none | W fix the fence · L lean on it, kids climb it | **Gap** · spec B58 (2026-11-15, waits for sign-off) |
| Jetty, orchard, quarry, dyer, watermill, outbuildings | no activity of their own | W one fitting job each (pick apples, dye cloth, tie up the boat) · L one life use each | **Gap** · spec B59 (2026-11-15, waits for sign-off) |

## Order (confirmed 2026-11-14)

The playtest sees the camp, so it goes first:
1. The camp gaps the player sees in the first hours: stew pot, grass, saplings, rocks, fallen branches, Keeper's tree, vines and weeds, frogs, deer.
2. The choice props: one W and one L use each.
3. Dogs and wolves (the chain is built, the people's side is not).
4. Village and town gaps.

**Animations are signed off by the user (2026-11-14).** Every use is first written as an animation spec in [animation-specs.md](animation-specs.md): who does it, when in the day, the beats in order, poses, props in hand, timing and what the player should read. The user signs off the spec, then the clip in the AnimLab review, before it goes into the game. Nothing merges unsigned.

Each step is a polish issue listed under the audit issue. Each is "done" when the user has signed off its clips, a close shot at phone portrait shows the
use, and there is a debug cheat for it.
