# Life in the box: what the people do all day (draft, 2026-10-29)

Status: **draft for the user's confirmation** (issue #53). Expands [game-definition.md](game-definition.md) §4 "Life
in the box" (user: the people "need to go around and be doing things, not just walk around"). Feeds the activity
planner (#29, pure C#) and the view (#30, peg dolls). Binding inputs: the 2026-10-29 rows of
[decisions.md](../decisions.md) (inherited lord; the innkeeper replaces the miller in the town; slice lifespan 55–80,
retirement at 65, apprentices from 50; families carry on; the evening talk closes long sessions; the shaping toolset;
shared charges refilled by cleaning and by happy people), [notable-roles.md](notable-roles.md),
[first-session-and-check-in.md](first-session-and-check-in.md), [research 10 "Decision"](../research/10-shaping-the-land.md),
the [style kit](spikes/style-kit.md) and the [Growth Lab](spikes/growth-lab.md).

It's all **view-side**: the economy and its dice are unchanged. **[est]** = my guess, to tune in play.

---

## Choices for the user

Recommended option first, marked ★.

1. **How long a day lasts on screen.** (A game day at 1 h = 1 year is about 10 real seconds, too fast to watch, so the
   view runs its own day clock.) ★ **One view day per game month, about 5 real minutes** (≈ 3.5 min of daylight,
   1 min of evening, 30–45 s of night): a 3-minute check-in sees work, a meal and maybe the evening; it steps with the
   sim, and each month the season moves on · follow the player's real clock (night when it's night for you) · a short
   90-second day loop.
2. **Night.** ★ **Short and lit**: windows glow, the fire or the lanterns burn, a few night owls (the tavern in the
   village, the watch in the town); if you open the app at night, people step out with lanterns to wave · skip night
   (a long dusk, then dawn) · a real night (everyone indoors, dark).
3. **Seasons in the view.** ★ **Follow the sim month** (a game year is a real hour, so a winter lasts ~15 min): both
   the look and the activities change (sowing, harvest, snowballs) · the look only · no seasons in the view.
4. **Activity icons over people.** ★ **None at full-box zoom**: where they are, their prop and their rhythm say what
   they're doing; a speech or thought bubble now and then for notables close up and for the pinned person · a small
   icon over every busy person · icons only when zoomed in.
5. **Prop size.** ★ **Props 1.5–2× real size** (an axe, a basket, a sheaf on the head), so they read at full-box zoom
   (Kingdom's lesson: the tool is the job) · true scale.
6. **Sweepers and gardeners in the town** (decided: "you see people cleaning"). ★ **They really clear trash heaps and
   trim overgrowth, slowly** (the sim removes the items; you can still flick faster; the glass stays yours) · cosmetic
   only (they sweep paths and weed the green; the trash heaps stay yours) · they carry heaps to a midden that you empty.
7. **Mourning.** ★ **One quiet evening at a remembering place** (a cairn in the camp, a churchyard by the shrine
   later): people stand still, someone leaves a flower; a small stone stays · the chronicle card only, nothing in the
   box.
8. **A new Keeper interaction** (the user asked for "more and more interactions"). ★ **Hold a finger still on clean
   glass**: people under it look up, kids run over, point and jump at it · tap the glass: everyone near looks up · none
   for now.
9. **"Show me" on a card.** ★ **Fly to the place, and the people involved do a short echo of it** (the newlyweds sit
   on the bench, the new chief stands at the fire) · a staged replay of the whole event · just fly to the place.

---

## 1. What other games do (research)

Markers as in research 09: **[V]** primary source, **[S]** secondary (wiki, press, guide, forum), **[est]** my reading.

| Game | How tiny people read as alive | Lesson for us |
|---|---|---|
| **The Sims** | "Smart objects": each object advertises what it offers (food, fun, rest) and a Sim picks among the best offers by its needs, with a bit of randomness so it doesn't look robotic. The idea grew out of SimAnt's pheromones [S] | **Buildings advertise activities**, people pick. A new building brings its activities with it, no planner rewrite. Keep a little randomness among the top choices |
| **Banished** | Villagers walk to work, carry what they make to storage, walk home to eat and warm up; long walks cost output, so players build homes near work [S] | **Carrying between places** is the clearest "busy" signal, and the walk itself is content. Ours costs nothing (view only) |
| **Stardew Valley** | Each villager has a hand-written schedule: time → tile, facing and a looping animation, varying by season, weather and day [S] | **A schedule of places and times, with one looping pose at each**, is enough for "they have a life". Season and weather switch the schedule |
| **Foundation** | Villagers loop work, needs and free time; the devs found the loop too strict and added a harvest festival "to break the rhythm of the villagers' daily toil" [V] | A daily loop alone gets samey: **festivals and story scenes must break it** |
| **Kingdom (Two Crowns)** | A villager's job is the tool in their hand: pick up a hammer, become a builder; a bow, an archer; archers hunt by day and man the walls at night [S] | **The prop is the job.** At tiny sizes a silhouette plus one big tool beats any icon. Day and night jobs differ |
| **Townscaper** | No people, but birds land on roofs, fly off when you build next to them and come back; laundry lines and boots on doorsteps; none of it explained [S] | **Undocumented little reactions to what you do** are the charm. Signs of life (washing, smoke, open doors) count as much as people |
| **Tiny Glade** | Sheep wander your paths, ivy grows, the sun sets, fireflies at night [S] | Ambient animals and light changes carry a lot of "alive" for little cost |
| **RimWorld** | Every pawn shows a one-line "what I'm doing now" (job report) when you select it [S] | **Tap a person → one line of what they're doing**, and why ("taking flour to the baker") |
| **Dwarf Fortress** | Taverns where people drink, socialise and perform; performers lift the mood of watchers; idle dwarves can get "stuck socialising" [S] | Evening social places with a small show (a singer at the fire). Social time must be **capped** so work still dominates the day |
| **Animal Crossing** | Villagers have hobbies (reading, exercise, music) that change what they do, carry and say [S] | **A trait shows as a hobby in free time** (the singer sings, the dreamer stares at the sky) |
| **Ostriv** | Citizens have their own motivations, needs and skills; the developer still lists "more believable behaviors, social activities, animations" as future work [S] | Believable small-town life is hard and is mostly animation and social scenes, not economy |
| **WorldBox** | (No usable source on its unit behaviour.) From play: units are a few pixels; you read the world by crowds, colours and buildings, not single people [est] | At full-box zoom, **groups and places** read; single people read close up |

What we take: buildings advertise activities (The Sims); schedules of places with one looping pose (Stardew); carrying
as the main "busy" signal (Banished); the prop is the job (Kingdom); festivals break the loop (Foundation); a one-line
job report on tap (RimWorld); traits as hobbies (Animal Crossing); undocumented small reactions (Townscaper).
What we avoid: needs that can fail (hunger, cold, the "stuck socialising" bug), a strict loop with no events.

---

## 2. Scale: what reads where

A peg doll is ~0.22 u tall in a box ~6 u wide (style kit, Growth Lab grid 60 × 44 cells of 0.1 u). At full-box zoom
on a portrait phone that's **about 30 px tall and 10–15 px wide** [est]. So:

| At full-box zoom (most of the time) | Close up (pinch in, or following a pinned person) |
|---|---|
| **Where** they are (in the field, at the woodpile, round the fire) | The prop's detail (the axe head, the fish on the stick) |
| **Rhythm**: a repeating stroke (chop-chop) vs walking vs still | The pose: kneeling, sitting, leaning on the fence |
| **One big prop**, oversized (Choice 5), and what's on the head or back | Who's who: the role accent (sash or hat band, notable-roles §6), the pin marker |
| **Shapes made by groups**: a ring at the fire, a line in the field, a pair on a bench, a queue at the well | Speech and thought bubbles, two people facing each other |
| **Signs of life**: smoke, lit windows, washing on a line, a puff of wood chips, sparks | Small reactions: a glance up at the glass, a nod |

Rule of thumb: **one readable thing per building at full zoom**; detail is a close-up reward.

---

## 3. A day, and the seasons

### The view day (Choice 1)
One view day = one game month ≈ 300 real seconds [est]. The planner works in **view hours 0–24**, and the view maps
them onto real seconds unevenly so night is short:

| View hours | Phase | Real seconds [est] | What happens |
|---|---|---|---|
| 5–7 | **Dawn** | 20 | Doors open, people step out and stretch; water from the stream or the well; the fire is lit; the baker carries loaves to the market (town) |
| 7–12 | **Morning work** | 80 | Everyone of working age at their work loop or carrying; kids play near home; elders sit out |
| 12–13 | **Midday meal** | 25 | Camp: everyone at the fire, a pot stirred. Village: kids carry baskets out to the fields, people eat at the field edge or on doorsteps. Town: the market and the inn's benches |
| 13–17 | **Afternoon work** | 80 | As the morning; the last trips carry the day's goods to the store |
| 17–20 | **Evening** | 50 | People gather (camp fire → village green and well → town market steps); friends talk, kids play, someone sings; the role-holder speaks; the inn fills. **The evening talk** closes a long session here (first-session §4: the view eases the clock to evening) |
| 20–5 | **Night** (Choice 2) | 45 | People go home, windows light then go out one by one; night owls: the tavern or inn until ~22, the captain of the watch's round with a lantern (town), a fisher by the pond. Nobody is ever shown lying in the open except a nap (the lazy) |

Rest day: one view day in 6 [est] (about half a real hour) is a rest day: no field work, people at the green or the
market, the priest's service at the shrine (village on), kids at games. Festivals replace it when they fall.

### Seasons (Choice 3)
The sim's season is `month % 12`; winter is months 11, 0, 1 (SimCore). Spring 2–4, summer 5–7, autumn 8–10.

| Season | Camp | Village | Town | Look |
|---|---|---|---|---|
| **Spring** | Gatherers on the new growth; kids at the stream | Sowing (scatter from a bag), lambs, a spring tidy of doorsteps | As the village; the dye meadow is cut | Fresh green, blossom on the orchard |
| **Summer** | Long days, fishing, kids swimming | Weeding, hay, the beekeeper's busy time (town) | Long market days, cloth drying on lines | Bright, long dusk; a drought story yellows the grass |
| **Autumn** | Mushrooms and nuts, the hunt | **Harvest**: everyone in the fields, sheaves carried to the mill; the harvest festival | Harvest plus trade carts; the cider goes to the inn | Gold fields, leaves turning |
| **Winter** | Hunters out; the rest by the fire; wood carried in | Little field work: wood, repairs, the potter and brewer indoors; snowballs | Workshops busy (smoke, open doors), the market shorter | Snow on roofs and ground [est: a light dusting, not a white-out], longer night, smoke from every chimney; the eldest's founding story at the fire (`eldestStoryAge`) |

---

## 4. The activity table: notables' jobs and roles

Every notable does their everyday job (notable-roles §1); a role-holder's day is **the role first, then the job**
(notable-roles §2). "Carries" = from the work site along the worn paths to where the goods go (view only: the economy
doesn't move goods). The **full zoom** and **close up** columns say how each one reads (§2).

### Everyone

| Who | Work loop at the site | Carries | Props | Full zoom | Close up |
|---|---|---|---|---|---|
| **Child** (< 16) | No work. Plays near home and the green (§6); at midday carries the meal basket to a parent (village on) | Meal basket | Ball, stick, small basket | Small, fast, in groups | Who they're playing with |
| **Apprentice** (a notable's child taken on by a master of 50+) | Works next to the master and copies each stroke a beat late | Half the master's load | A smaller copy of the master's tool | A big and a small doll doing the same thing | The delay; the master's nod |
| **Elder** (65+, retired) | Sits at the fire or a bench; tells stories to kids in a ring; minds the babies; wanders to look at the work and "advise" | — | Stick, stool | A still doll with a ring of small ones | The talking bob, kids' faces turned up |

### Camp
Camp buildings: the fire (`Camp`), tents (`Hut`), gathering grounds (`Farm`), woodpile (`Lumbermill`).

| Who | Work loop at the site | Carries | Props | Full zoom | Close up |
|---|---|---|---|---|---|
| **Gatherer** | Kneels at bushes or the gathering grounds, picks, stands, moves on; mushrooms after the Keeper's rain | Basket on the hip → the fire | Basket | Crouch-stand rhythm among bushes | Berries in the basket |
| **Hunter** | Walks to the forest edge, crouches, waits, follows a herd; the kill stays off-screen | A hide over the shoulder → the drying rack | Spear | A doll with a long stick heading out or back | The crouch, the hide on the rack |
| **Fisher** | Stands on the bank or wades, casts, waits, a little jerk on a catch | Fish on a stick → the fire | Rod or spear, fish stick | A still doll at the water's edge | The jerk; fish on the stick |
| **Woodcutter** | Chops at a tree (puff of chips), a tree comes down now and then [est: only trees the forest layer can lose] | Log on the shoulder → the woodpile | Axe, log | Chop rhythm + chips; logs moving to the pile | The swing |
| **Toolmaker** | Sits on a log seat by the fire, knapping (tiny tap) | Hands tools to a hunter or woodcutter | Stone, hammerstone | Sits still by the fire | The tap, a handover |
| **Chief** (role) | Morning: walks the camp. Evening: **speaks at the fire** (the evening talk's speaker). Leads the hunt when the Keeper sends a herd (rain and herd call them into view, research 10). Steps between arguing rivals | — | Feather or headband accent | Standing doll at the centre of the ring | The speech bob, the bubble |

### Farming village
Village buildings add the well and green (`Well`), `Windmill`, `Market`, `Tavern`, `Church` (a small shrine).

| Who | Work loop at the site | Carries | Props | Full zoom | Close up |
|---|---|---|---|---|---|
| **Farmer** | Hoes along a row, scatters seed (spring), weeds (summer), scythes (autumn) | Sheaf on the head or back → the windmill | Hoe, seed bag, scythe, sheaf | A line of dolls moving down a field, same stroke | Seed falling, sheaves stacked |
| **Woodcutter** | Chops in the grove or woods; at the lumber mill two people work a long saw | Logs on a handcart → the lumber mill | Axe, saw, handcart | The cart on the path; the see-saw of the two-man saw | The saw's rhythm |
| **Fisher** | From the jetty | Fish basket → the market (or home) | Rod, basket | As the camp, on the jetty | As the camp |
| **Shepherd** | Walks the flock, leans on the fence, crooks a stray back | A lamb in the arms (spring) | Crook | A doll among white blobs, leaning | The lean, the lamb |
| **Potter** | Kneels at the clay bank, throws a pot (spinning wheel), sets pots in a row to dry | Pots stacked on the head → the market | Wheel, pots | A row of pots growing by the water | The spinning wheel |
| **Brewer** | Rolls a barrel (a rolling cylinder) from the store to the tavern; serves at the door in the evening | Barrel (rolled), mug | Barrel, mug | A rolling barrel on the path | The serve |
| **Beekeeper** | The sim lists it for the village, but research 10 moved the meadow to the town: in practice it starts in the town | | | | |
| **Headman** (role) | Morning round of the fields (stops at each, a nod); greets newcomers at the edge; **speaks on the green** in the evening; opens festivals; settles grazing quarrels (herd level-up) | — | Accent sash | One doll visiting every field in turn | The nod; the speech |
| **Miller** (role) | Opens the sails (they start turning), carries sacks in and out, takes sheaves from farmers at the door; flour dust puffs | Flour sacks on the shoulder → homes and the market | Sack | **The sails turning** is the read; a doll at the mill door | Dust puff, the handover |
| **Priest** (role) | Rings the bell (dawn, wedding, death); blesses the harvest at the field edge; sits with the old; climbs the Keeper's hill (hill & hollow) to look at the glass | — | Bell rope, accent stole | A doll walking up the hill alone | Looking up at the glass |

### Industrious town
Town buildings: the village ones plus `Workshop`, `Guildhall` and the town placeholders until M6 (quarry, kiln,
smithy, watch house: notable-roles §1–§2).

| Who | Work loop at the site | Carries | Props | Full zoom | Close up |
|---|---|---|---|---|---|
| **Farmer, woodcutter, fisher, shepherd** | As in the village; the shepherd's wool goes to the weavers | Wool bundle → workshop | As village | As village | As village |
| **Beekeeper** | Tends hives at the dye meadow's edge, a small cloud of bees, lifts a frame | Honey pots → the market | Hives, smoker | Box hives + a dark dot-cloud | The smoker puff |
| **Mason** | Cuts at the outcrop (stone chips), squares a block | Block on a sledge or barrow → the building site | Pick, mallet, block | Pale blocks moving on the path | The chisel tap |
| **Potter** | As the village, plus feeding a smoking kiln | Crates of pots → the market | Kiln smoke | Smoke column by the water | As village |
| **Smith** | Hammers at the anvil, **sparks**, dips in the trough (steam puff) | Tools and horseshoes → workshops, the market | Hammer, anvil | Sparks and the forge glow (also at dusk) | The strike rhythm |
| **Weaver / dyer** | Works a loom by an open door (shuttle back and forth); dips cloth in vats; hangs it on lines | Bolts of cloth → the market | Loom, cloth, drying lines | **Coloured cloth on lines** (dye meadow colours) | The shuttle |
| **Baker** | At dawn carries loaves to the market; kneads; the oven glows | Loaf board on the head | Board, loaves | The dawn walk to the market | Steam from the loaves |
| **Stallholder** | Stands at a stall, calls out (a jump-bob), hands goods over | Restocks from the carts | Stall, goods | Dolls behind stall awnings, shoppers in front | The call, the trade |
| **Sweeper / gardener** (care, §7) | Mornings: sweeps the streets and takes trash heaps to the cart. Afternoons: weeds the green, trims vines | Handcart of rubbish → the midden; cuttings basket | Broom, cart, shears | A doll with a cart stopping at each heap | The broom strokes |
| **Lord** (role, inherited) | Morning round of the market and workshops with the heir trailing behind; holds court on the market steps (sits; people come up, bow, leave); **speaks from the steps** in the evening; leads the yearly procession; answers the Keeper's spring | — | A chair on the steps; accent cloak | A seated doll at the steps, a small one behind them on the round | The bow, the heir copying the lord |
| **Priest** (role) | As the village; blesses the storm's new pond | — | As village | As village | As village |
| **Innkeeper** (role) | Sweeps the inn's doorstep, rolls barrels in; **at dusk carries tables out** (more under the Keeper's lamp); serves; takes the orchard's cider | Barrels, tables, cider jugs | Tables, mugs, lantern | Tables appearing outside at dusk, a crowd round them | The serve, the laugh |
| **Guildmaster** (role) | Walks the workshops inspecting (stops, leans in, nods or shakes the doll's whole body); chairs the guild meeting at the guildhall (the tavern until it exists); hangs the guild banner | — | Banner, accent | A doll stopping at each workshop | The nod or the shake |
| **Master builder** (role) | Stands at the building site pointing, measures with a rod; the scaffolding is theirs; directs the mason's blocks | — | Measuring rod | A doll by the scaffolding, arm out | The pointing |
| **Merchant** (role) | Haggles at the big stall, loads a cart, counts coins; sends a cart off along the main road and meets it coming back | Cart of goods (the town's longest trips) | Cart, coin bag | **The cart on the main street** | Coins counted |
| **Captain of the watch** (role) | Lights the lanterns at dusk (from the Keeper's lamp, research 10), walks the night round, finds lost goats and children (the "lost" story) | Lantern | Lantern, accent | A moving light at night | The search |

A role-holder's trait colours how this looks (notable-roles §4, planner hints from #28): the singer sings at the
fire most evenings, the grump sends people home early, the lazy naps on the green after lunch, the worrier lights
extra lanterns, the kind sits with the old.

---

## 5. The crowd: activities from buildings

The crowd is a count in the sim, not workers. The planner gives each crowd slot a stable index and draws its job
from the buildings, as The Sims' objects advertise what they offer. Kids are ~25 % of the crowd [est]; crowd adults
never get roles. A building's slots fill in the order below, so a small settlement shows its most important work first.

| Building (era look) | Crowd slots [est] | Activities offered |
|---|---|---|
| **Fire** (camp) | 1 cook | Stirring the pot, feeding the fire; everyone's evening and midday spot |
| **Tent / hut / house** | its residents | Doorstep sitting, washing on a line, chopping kindling, sweeping the step; home at night |
| **Gathering grounds** (camp `Farm`) | 2 | Picking, as the gatherer |
| **Field** (`Farm`, village on) | 2–3 | Field work by season; the midday basket |
| **Woodpile** (camp `Lumbermill`) | 1 | Splitting and stacking |
| **Lumber mill** | 2 | The two-man saw, stacking planks |
| **Well and green** | 0 (a place) | Water carrying at dawn and dusk (a yoke with two buckets), gossip at the well, games on the green |
| **Windmill** | 1 | Sack carrying |
| **Market** | 2 stallholders | Shopping: the crowd's free time goes there with baskets (it matches the Growth Lab's market trips) |
| **Tavern / inn** | 1 server | Drinking on the benches in the evening, a singer |
| **Church / shrine** | 0 (a place) | The rest-day service, weddings, mourning |
| **Workshop** (town) | 2 | Weaving, dyeing, whatever its craft is |
| **Guildhall** (town) | 0 (a place) | Guild meetings, the banner |
| **Town care** | 1 sweeper per ~12 people [est] | Sweeping, trash to the cart, weeding (§7) |
| **Keeper's shaping** | as it says | The tools' "who reacts" column (research 10): a herd brings hunters, a dye meadow beekeepers |

If a building has more slots than the crowd can fill, the slots are left empty in that order (the field gets its
first farmer before the mill its second sawyer).

---

## 6. Social moments

Driven by the sim's links (`partnerId`, `friendId`, `rivalId`, `mentorId`, `parentId`) and its chronicle kinds.
When an event happens while you watch, the planner stages its **scene**; when it happened while you were away, the
card's "Show me" plays a short **echo** (Choice 9).

| Moment | Who | Where and when | What you see |
|---|---|---|---|
| **Friends chat** | `friendId` pairs | Midday or evening, at the fire, bench or well; at least once a day when both are free | Two dolls facing, taking turns to bob; a laugh (both lean back) |
| **Rivals argue** | `rivalId` pairs | When their paths cross at a public spot; at most once a day, and not every day [est] | Lean in, fast bobs, one arm up; they walk off opposite ways. The chief or headman may step between |
| **Courting** | Two single adult notables who are friends (the teaser's pair) | Evening walk; a bench or a **decorated spot** (research 10's decorate) | Walking side by side, sitting close, a flower picked |
| **Wedding** (`wedding`) | The couple, everyone free | A procession from home to the fire or shrine; the bell | A line of dolls, a ring round the couple, a cheer, then a dance |
| **A birth** (`birth`) | Neighbours, the family | Next day at the home | Visitors at the door, a tiny bundle held up |
| **Kids play** | Children | Daytime near homes, the green, the water | Tag and chase round the fire, splashing in rain puddles, running up the Keeper's hill, a raft, snowballs |
| **Apprentice** (`apprentice`) | Master and apprentice | At the work site | The apprentice copies a beat late (§4) |
| **Elders' stories** (`elder`) | Elders, kids | Evening at the fire; the eldest's founding story in winter | A ring of small dolls round a still one |
| **Festival** (`festival`, trait feasts, the Era Festival relic) | Everyone | The fire, then the green, then the market square; replaces a rest day | Bunting and tables, a ring dance, a singer, the leader's speech |
| **Reconciliation** (`reconcile`) | Ex-rivals | At the fire | The two sit together for the first time |
| **Lost and found** (`lost`) | The lost person, a search party | Evening into night | Lanterns spread out; the captain of the watch in the town; the return gets a cheer |
| **Weather stories** (`storm`, `snow`, `fog`, `rainbow`, `ducks`, `sheep`) | Everyone outdoors | As they happen | Shelter under eaves, snowballs, walking slowly in the fog, pointing at the rainbow, kids chasing the ducks |
| **Role moments** (#27: won, contest, election, death in office) | The notables | The seat | A gathering at the seat; the winner stands up front; the loser walks home alone (notable-roles §5) |
| **Mourning** (`death`, Choice 7) | Family, friends, the priest | The evening after, at the remembering place | Still dolls, heads bowed (a forward lean), a flower left; the next morning is normal. The death itself is never shown: the person just isn't there |

---

## 7. Care workers in later eras

Decided (2026-10-29, "Care moves up"): in later eras the people handle their own trash and gardens, **visibly**; the
glass is always the Keeper's (game definition §5 rule 5).

| Era | Who cares | What you see | What stays the Keeper's |
|---|---|---|---|
| **Camp** | Nobody | The trash heaps are the Keeper's to flick away | Glass, trash, overgrowth |
| **Village** | Everyone a little, cosmetically | People sweep their own doorsteps and weed their crofts; a spring tidy | Glass, trash heaps, overgrowth (the people can't reach the roofs yet) |
| **Town** | The **sweeper/gardener** job (notables) plus crowd sweepers (§5) | Morning: a sweeper with a broom and a handcart stops at each heap and wheels it to the midden. Afternoon: weeding the green, trimming vines off roofs with shears. Under a grump or a worrier in office, busier | **The glass, always.** Under Choice 6 ★ you can still flick heaps faster than the sweepers get there, and they cheer when you do |

Under Choice 6 ★ the sim needs a small change: in the town, the oldest trash item and a little overgrowth go away
every N real hours per sweeper [est], so the view has something real to show them doing. That touches care state, not
the economy.

---

## 8. Reactions to the Keeper

Practices §4: every touch gets visible feedback within a frame or two. Reactions **interrupt anything** for 1–4 s,
then the person picks up where they left off.

| The Keeper does | The people do |
|---|---|
| **Returns** (> ~15 min away, decided) | Everyone outdoors turns to the camera and **waves** for 2–3 s, more of them the longer you were away [est]. At night they step out with lanterns (Choice 2) |
| **Wipes** grime off the glass | People under the clearing patch stop and **look up** (whole-body lean back), shade their eyes; at a full clean, everyone **cheers** (a jump) and someone says "Look at that sky!" |
| **Flicks trash** away | Those nearby clap (a quick double bob); kids run to where it vanished |
| **Prunes** a vine off a roof | The family steps out of the door and waves |
| **Rains** (rub) | People stop, look up, hold their hands out; some run under the eaves with baskets over their heads; **kids splash**; later the gatherers or farmers walk over (the tool's "who reacts", research 10) |
| **Other shapings** (grove, herd, channel, lamp, hill, outcrop, spring, dye meadow, decorate) | The nearest look and **point**; a few walk over to look (that doesn't count as "used", research 10); then the tool's job or role arrives (the chief leads the hunt, the captain lights the night round from the lamp) |
| **Holds a finger on clean glass** (Choice 8) | People under it look up; kids run to stand under it, point and jump; they scatter when you lift |
| **Taps a person** | They stop, look up and wave; the one-line job report shows ("Ivo Lund is taking logs to the woodpile") |
| **Follows a pinned person** | Now and then they glance at the camera (≤ once a minute [est]) |
| **Leaves the box messy** | Mess dims the sunlight and slows the work pace (−25 % at the cap, Gate 1): work strokes and walking slow by the same amount, people glance up at the dim sky now and then. Wistful, never pleading |
| **Watches an era arrive** | Everyone gathers at the centre and looks up together |

What they think the glass is grows with the eras, **only through what they say** (decisions, "Relics"): the sky
(camp), the Keeper's window (village), something a little shrine faces (town).

---

## 9. Animation budget (peg dolls, procedural only)

Peg dolls have **no rigs and no faces** (style kit). Every move is a transform of a few rigid parts, driven by code
or by the shader (per-instance values), never a skinned clip.

**Parts per doll:** body + head (one mesh), two stub arms (separate, so they can swing), and one prop in a slot:
hand, head, back, shoulder or pulled behind (a cart). Kids are the same at 0.68 scale.

| Move | How (transform only) | Used by |
|---|---|---|
| **Walk bob** | Small hop on a sine (exists in the spike), slight waddle side to side | Every walk |
| **Run** | Faster, higher bob, forward lean | Kids, a search party, rain |
| **Turn / face** | Yaw towards a target | Talking, waving, looking at a shaping |
| **Lean** | Tilt the whole doll (forward = bow, work, mourning; back = look up, laugh) | Everywhere |
| **Crouch / kneel** | Squash Y to ~0.7 and lower | Gatherer, potter, hunter waiting |
| **Sit** | Lower onto a seat, slight squash | Fire, bench, steps, toolmaker, elders |
| **Nap** | Lie at 90° on the grass | The lazy only, daytime |
| **Work stroke** | An arm (and the prop) rotates through an arc on a rhythm, plus a body dip on the hit, plus a puff (chips, sparks, dust, steam) | Chop, hoe, hammer, knap, scythe, sweep |
| **Two-person stroke** | Two dolls with opposite phase | The long saw |
| **Carry** | Prop parented to a slot: on the head (sheaf, loaves, pots), in the arms (lamb, basket), on the shoulder (log, sack), on the back (bundle), pulled (handcart: wheels turn) | Every carry |
| **Roll** | A barrel rolls in front of the walker | Brewer, innkeeper |
| **Talk bob** | Alternating small bobs between two facing dolls | Chat, speeches (one doll bobbing, facing a ring) |
| **Argue** | Faster, bigger bobs, lean in, one arm up | Rivals |
| **Wave** | One arm up and wiggle | Return, a tap, prune |
| **Cheer / clap** | A jump; a double bob with both arms up | Full clean, wedding |
| **Dance** | Hop while circling a centre | Festivals, weddings |
| **Point** | One arm out towards a target | Shapings, master builder, rainbow |

**Ambient signs of life** (as cheap as a doll, often cheaper): chimney and fire smoke, lit windows, doors open by
day, washing on lines, the windmill's sails, cloth drying, sparks at the forge, sheep and goats, ducks.

**Budget [est, to measure on the TestFlight build]:** up to ~60 dolls drawn (town: ~45 people, plus animals) in
one or two instanced draws; ≤ 20 prop meshes, all code-built in the palette; the planner runs once per view hour per
person (not per frame); per frame only positions along a path and the move sines, ≤ 0.5 ms CPU on a mid-range
phone. Practices §7: steady 30 fps minimum.

---

## 10. Rules for the activity planner (#29)

1. **Pure function of time.** Pure C#, no UnityEngine. `Plan(person, viewDay, viewHour)` → activity kind, spot,
   prop, move. Deterministic from a hash of (seed, person, view day), like `JobRoll`: it never draws from the sim's
   streams and never writes the sim. Opening the app after a catch-up costs nothing: the planner just asks "what is
   everyone doing now".
2. **Every person, every view hour, has a valid activity and a spot** (#29's test). A missing job site falls back to a
   chore at home, never to wandering.
3. **No aimless walking.** Every walk has a destination and follows the path network, using the same walking cost and
   the **same trip types as the Growth Lab's wear pass** (home → green, field or workplace, market), so the paths you
   see worn are the ones people walk.
4. **Busy share.** During work hours, ≥ 70 % of visible working-age adults are in a work loop or carrying, ≤ 10 % walk
   empty-handed [est]. Kids and elders don't count.
5. **Pace.** An activity lasts 20–90 real seconds before it changes [est]: the box changes while you watch, without
   jitter. Work strokes and walks slow with the sim's pace penalty (up to −25 %).
6. **Spread.** Each spot has a few slots (offsets); at most one doll per slot; a crowd queues.
7. **Notables:** role first, then job; role beats at fixed times (the leader's evening speech; the captain's dusk
   lanterns; the baker's dawn walk). Partners eat and go home together; kids stay near their parents' home or the
   green; the heir trails the lord.
8. **Relationships:** friends meet at least once a day when both are free; rivals argue at most once a day and not
   every day [est]; reconciled pairs sit together.
9. **Traits** colour the day through the #28 planner hints only (the lazy naps, the grump goes home early, the singer
   sings); the economy is never touched.
10. **Interrupt order:** Keeper reaction > story scene (wedding, festival, mourning, an era) > role beat > the day's
    schedule. A reaction lasts ≤ 4 s; a scene ≤ one view day.
11. **Story scenes** come from chronicle entries of the current month (kind, people, building). A scene that
    happened while you were away is not replayed; it is echoed when you press "Show me" (Choice 9).
12. **Never show:** a death, a kill (the hunt stays off-screen), anyone hungry, cold, hurt or crying. Mood shows only as
    pace and a glance at the sky. Neglect never shows as suffering (pillars 2 and 4).
13. **Crowd** slots are stable by index, so a crowd member keeps their look and job while the population stays above
    their index; their job comes from the buildings (§5), never from the economy.
14. **Seasons and weather** switch the schedule, as in Stardew: the planner reads the season from the month and the
    weather from the month's story flags (snow, storm, fog) and the Keeper's rain.
15. **Night** keeps a few night owls (Choice 2) so an open at night still has life.
16. **The debug panel** (CLAUDE.md): set the view hour, skip a view day, force a season and weather, start each scene
    (wedding, festival, mourning, argument, chat), "make everyone wave", show each person's job report.

### What the sim must expose

| The planner needs | In the sim today | Missing (who adds it) |
|---|---|---|
| Era, month, season | `era`, `month` (season = month % 12) | — |
| Notables: job, age, alive, trait, family links | `notables` (job, age, partner, friend, rival, mentor, parent) | — |
| Roles and their holders | — | `RoleSeat` list (#27); trait planner hints (#28) |
| Buildings and where they are | `buildings` (type, id, slot, founded) | Positions, doors and footprints (M2, the Growth Lab's `SiteMap` / `Placer` into `Sim/Settle`) |
| The path network | — | Tiers and walking cost per cell (M2, `PathNet`) |
| Land features (pond, forest edge, bushes, herds, groves, outcrops, meadows) | `HasFeature` stubs; `nudges` (kind, slot, credited) | Feature positions; `Nudge` x, z (Growth Lab gap 6) |
| Who lives where | — | A home per family (a hut id): the sim or a planner rule ("each family's home is its founder's hut") |
| Population and kids | `population` (a count) | Nothing: the planner splits it (~25 % kids [est]) |
| Story events this month | `chronicle` (kind, month, notable ids, building id) | Fine as is; a festival's month is already a chronicle entry |
| Mess and pace | `grime`, `overgrowth`, `trash` (slot, offset), `OutputMultiplier` | Trash x, z (Growth Lab gap 6); under Choice 6 ★, town sweepers clearing items |
| The era waiting | `eraReady` | — |

---

## Sources

- The Sims: [Wikipedia](https://en.wikipedia.org/wiki/The_Sims_(video_game)) [S], [Game Maker's Toolkit, "The genius AI behind The Sims"](https://gameindustrylibrary.com/documents/gmtk-the-genius-ai-behind-the-sims) [S], [transcript](https://amara.org/videos/cVoJS4OdmVql/en/4330642) [S], [Grand Text Auto on The Sims](https://grandtextauto.soe.ucsc.edu/2008/03/13/ep-83-the-sims/) [S].
- Banished: [Steam discussion, travel and productivity](https://steamcommunity.com/app/242920/discussions/0/3277925755447184887) [S], [Steam discussion, food in transport](https://steamcommunity.com/app/242920/discussions/0/5051317596398416741) [S].
- Stardew Valley: [Modding: schedule data](https://stardewvalleywiki.com/Modding:Schedule_data) [S], [Modding: NPC data](https://stardewvalleywiki.com/Modding:NPC_data) [S].
- Foundation: [Polymorph Games dev news](https://www.polymorph.games/foundation/news/category/dev-news/) [V] (Harvest Festival post: "break the rhythm of the villagers' daily toil").
- Kingdom Two Crowns: [Wikipedia](https://en.wikipedia.org/wiki/Kingdom_Two_Crowns) [S], [Magic Game World, subjects](https://www.magicgameworld.com/?p=27271) [S].
- Townscaper: [Wikipedia](https://en.wikipedia.org/wiki/Townscaper) [S], [These Old Games review](https://www.theseoldgames.com/search/label/app) [S], [IHBC news](https://newsblogs.ihbc.org.uk/?p=27666) [S].
- Tiny Glade: [GamingOnLinux](https://gamingonlinux.com/2024/06/tiny-glade-is-a-gorgeous-castle-doodling-game-thats-real-clever-to-relax-with) [S], [80.lv interview](https://80.lv/articles/cozy-and-relaxing-castle-building-sim-tiny-glade-launches-soon/) [S].
- RimWorld: [Steam discussion, the "Standing" job report](https://steamcommunity.com/app/294100/discussions/0/1762481957305165633) [S], [RimWorld wiki, Work](https://mail.rimworldwiki.com/wiki/Work) [S].
- Dwarf Fortress: [DF wiki, Tavern](https://dwarffortresswiki.org/tavern) [S], [DF wiki, Performer](https://dwarffortresswiki.org/performer) [S].
- Animal Crossing: [Nookipedia, Hobby](https://nookipedia.com/wiki/Hobby) [S].
- Ostriv: [gamedev.net dev thread](https://gamedev.net/forums/topic/682846-city-building-strategy-game-in-development-ostriv/5318156/) [V], [IndieDB roadmap](https://www.indiedb.com/games/ostriv) [V].
- WorldBox: no usable source found; the row is my reading [est].
- Our own: [style kit](spikes/style-kit.md) (peg dolls), [Growth Lab](spikes/growth-lab.md) (paths from wear, the box's scale), `AquariumCiv/Assets/Game/Sim` (SimState, SimCore.Jobs, SimCore.Eras).
