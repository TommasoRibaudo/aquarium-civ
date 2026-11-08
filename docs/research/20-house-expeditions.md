# 20 · Expeditions into the Keeper's house (research, 2026-11-07)

Status: **direction approved by the user 2026-11-07** (§0); open questions in §6. Its own milestone, needing more
research, curves, design, art and a UI rethink before any build. Comes from the user's idea (2026-11-07): in the village and town
eras the ecosystem is finished (people farm and tame), so the Keeper's-hands work of P9 winds down. The new loop is that
**notables leave the tank and explore the Keeper's house** (like Fallout Shelter's wasteland), fight a little (cockroaches,
insects), and bring back **gear for themselves** and **decorations / artifacts for the tank**. The player picks who goes
and what they carry.

Builds on: [game-definition](../design/game-definition.md) (eras, session cadence, fair-to-your-wallet rule),
[notable-roles](../design/notable-roles.md), [relics-and-glass-voice](../design/relics-and-glass-voice.md),
[chronicle-and-history §5](../design/chronicle-and-history.md) (belief stages), [research 09](09-relics-and-powerups.md),
[research 16](16-visit-loop-events-and-choices.md) (one choice per visit), [research 18](18-placed-life-and-ecosystems.md).

---

## 0. Decided (user, 2026-11-07)

- **Two kinds of item only: equipment and decorations.** Equipment feeds the same loop: it helps notables bring back
  better things (room to build on later). Decorations are the true keepsakes, placed in the tank.
- **Notables wear their equipment** in the tank: a pen carried as a sword when they walk around, a cool hat, and so on.
- **Notables only. Nobody dies.**
- **Names in the people's voice** (Pikmin style), yes.
- **The closed-world rule is lifted.** The tank already has newcomers from outside; the house is the story it tells.
  The outside is revealed but **mostly not seen**: postcards and a few glimpses.
- **The relics go.** Sky Crumb, Keeper Doll and the rest of the current relics are removed and **replaced by this**.
- **The first explorer to see the tank from outside says "glass" first** (replaces the Lens).
- **Empty roles are fine**: the notable is simply away, the card says so, people may say a line about it.
- **Postcards**: yes, and **updates during the trip** too (a postcard mid-trip: facing the cat, running from it,
  fighting a bug).
- **Where**: the village starts in **the Keeper's room** (where the tank stands), with finds close to what the people
  know. Then, slowly, the bathroom, the kitchen and more.
- **Loot is kept separate** from the things you place (as below: equipment vs decorations).
- **Its own milestone**, with a lot of research, balancing curves, design, art and **UI back to the drawing board**.

Second round (user, 2026-11-07):
- **How they get out: they build their own way.** A rope thrown up to the rim; they climb up, climb down the outside,
  and go. Sometimes someone falls a bit (fun, never hurt).
- **Party size: 1 notable in the village, up to 3 in the town.**
- **What the relics did** must be analysed in the milestone: list what is lost and what changes. Already settled:
  - **The first-session surprise** (Sky Crumb falling): not needed, drop it as a goal.
  - **Box 2's founders** can bring **other things** (decorations, not relics).
  - **The Keeper Doll → a doll they steal from the Keeper's room** on an expedition. It still opens belief stage 2.
  - **The Festival Mask stays, reworked**: the user never saw the festival or the mask. **The first chief makes it**, so
    it's a memory of them, and it becomes a **decoration you can place**.
  - **The Left Sandal: removed.**
- **The camp has no expeditions.** The **breadcrumb (Sky Crumb) can stay in the camp** as the camp's one thing from
  outside (just not as the first-session surprise).
- **The old relics come out of the build during this milestone**, not before.
- **Inventory and placement need thorough UI design**: placing decorations (and the mask) is a system of its own, not
  just a list.

## 1. What the reference games do

| Game | Loop | What to take | What to avoid |
|---|---|---|---|
| **Fallout Shelter** (wasteland) | Drag a dweller out the door with a weapon, outfit, stimpaks. They walk alone in real time; a text log grows. The longer out, the better the loot and the tougher the enemies; health drains. Recall any time; the trip home takes a share of the time out. Luck → loot, Endurance/Strength → survival. Later: 3-person **quests** with scripted rooms and tap combat. | The core: **pick a person + kit, send, come back later, read what happened, unpack**. "Longer = better but riskier", recall any time. Gear shown on the person. | The **text log** (our user wants it *seen, not read*). Death. Speed-ups for premium currency. Tap combat with lots of screens. |
| **Pikmin 2** (treasures) | Tiny beings carry giant human objects home; each gets a grand, wrong name ("Dimensional Slicer" for a pizza cutter) and a series in the Treasure Hoard (kitchen, appliances, Christmas…). | **Human objects named by tiny people.** Themed sets to complete. The carrying back is the moment (everyone hauling one bottle cap). | Weight/value math. |
| **Tinykin** | A tiny explorer in a giant 1990s house; each room is a level (kitchen, bathroom, bedroom); locals live in them; a museum to fill. | **Rooms as places**, each with its own creatures and look. A collection you see filling. | It's a platformer: we don't control the explorer. |
| **Neko Atsume** | Leave out goodies; cats come while you're away and leave mementos. | Away-time loop with **a gift waiting** on return. | — |
| **Arrietty / The Borrowers** | Tiny people "borrow" from a human house: a pin as a sword, a sugar cube as a haul, the cat as the great danger. | The **tone**: brave, careful, cosy. Household objects reused at their scale. | — |
| **Grounded** | Shrunk kids in a garden; ants, spiders, weevils; gear from bug parts. | A readable bestiary of household bugs. | Real combat, horror spiders. |
| Retainer / garrison missions (FFXIV, WoW, Monster Hunter Rise's Argosy) | Send followers on timed jobs, success chance from stats and gear. | Simple **odds from stats + kit**, shown before you send. | Mission boards, chores. |

The best match is **Fallout Shelter's send/return loop, with Pikmin's treasures and Arrietty's tone**. We fix Fallout's
weak spot (you read about it but never watch it) by making the trip and the return something you can see.

## 2. How it fits the game we've already decided

**The closed-world rule (2026-11-03) is the biggest point.** "Nothing comes from or goes to outside" is a rule about
*text*, so it doesn't forbid this. But expeditions make the outside **real**, so the rule changes from "there is no
outside" to "**the outside is the Keeper's house, and the people find it out bit by bit**". That fits the belief stages
very well:

| Belief stage | Era | What the people think | What expeditions can be |
|---|---|---|---|
| 1 · the sky wall | Camp | The wall is the world's edge | None (the camp shapes the land with you) |
| 2 · the Keeper's window | Village | Someone looks in through a window | **The first trips, close by**: the Keeper's table/shelf the tank sits on. They think it's *the Keeper's land*, a holy place. Finds are relics of the Keeper ("the Keeper's Moon" = a coin) |
| 3 · a wall, and a room beyond | Town | Glass, four edges, a vast room | **Real exploring**: room by room, with maps, a guild of explorers. The scholars name things *because the explorers brought them back* |

That gives the town's "a room beyond" a cause you play, which is better than the Lens alone. **Conflict to settle:**
relics-and-glass-voice Choice 4 says the people first say "glass" when the Lens is set up. With expeditions, the first
explorer to see the tank from outside could be the one who says it.

**Other rules it touches:**
- **Seen, not read** (2026-11-06): no Fallout-style log. See §4.
- **One choice per visit** (research 16): "who goes, with what" *is* that choice. It fits the 2–5 check-ins a day
  without adding chores.
- **Relics** (research 09: never rolled, timed or bought; every relic is born in a story): expedition loot is rolled, so
  **keep it a separate collection** ("Finds" or "Treasures"). Relics stay story-born. A find can *become* a relic when a
  story happens around it (the first find ever, or the one a notable was hurt bringing home).
- **Fair to your wallet**: no speed-ups, no paid energy, no paid gear. Just as cleaning is never sold.
- **No war, ordinary people, never show fear for long**: fights stay small and funny (shooing a cockroach with a pin),
  and **nobody dies out there**. The worst that can happen is they come home bruised and with less.
- **Roles**: a notable who is away leaves their role empty for a while. That makes a small, real choice ("send the
  miller? the mill stands still for a day") and gives the tank a story ("the village waited at the gate").
- **The Keeper's cat** (the town gossip already says the Keeper has one): the house's great danger and a running joke.
  It is never fought, only avoided. Maybe one day it is befriended.

## 3. A loop that fits

1. **Village unlock**: an era-moment beat. The Keeper lifts the lid / lowers a ladder (a pencil? a ruler?) over the rim.
   The bravest notable climbs it and comes back with one thing. Everyone gathers.
2. **On a visit you send one expedition** (village: 1 at a time; town: 2–3 [est]). You pick:
   - **Who** (1 notable in the village; a party of up to 3 in the town). Traits matter: *brave* fights better,
     *clever* finds more, *kind* brings back a lost creature, *dreamer* finds odd things.
   - **Where**: a room on a house map that grows (table → windowsill → kitchen floor → under the sofa → the bathroom →
     the attic…). Each room has its own bugs, finds and look.
   - **Kit**: 1–2 slots of gear (needle spear, thimble helmet, button shield, matchstick torch, raisin rations,
     a thread for climbing). The game shows the odds before you send ("Ivo will likely come back with 2–3 finds").
3. **They're away for a few hours** (fits the 4–5 h between visits). You can watch (§4) or recall them early.
4. **The return is the moment**: you see them walk up the ladder and haul the find over the rim, everyone gathers, the
   kids poke it, and it gets a name in the people's voice. Then you **place it** in the tank (decoration) or **give it**
   to a notable (gear).

**Two kinds of loot (as the user described):**
- **Gear** for notables. Shown on their model and card. Makes later trips better. Some is useful in the tank too (the
  thimble becomes the village well bucket?).
- **Finds** to place: a marble, a coin, a toy soldier, a Lego brick, a lost earring, a seashell, a bottle cap. Pikmin
  style, in sets ("the Keeper's Table set", "the Kitchen set"). Placed in the tank, people **use them**: the coin is a
  plaza, the bottle cap a bath, the soldier a statue they leave offerings at. That's where it meets research 18 (placed
  things that live) and the shelf/museum.

**Names in the people's voice**, by belief stage: village "The Keeper's Moon" (a coin), town "A round of stamped copper,
very heavy; the scholars think it is money" (the same coin, seen better).

**Light fights**: auto-resolved, from trait + gear + luck. Bestiary: ant, cockroach, silverfish, woodlouse, moth, house
spider (big, slow, sits in a web: avoid, don't fight), dust bunny (a joke enemy), and the cat (never fought). A fight
costs health and can make them come back early. No death.

## 4. Making it seen, not read

Options (from cheapest to dearest):
- **A. Postcards**: each trip comes back with 1–3 snapshots (a picture of the notable next to a giant cockroach on the
  kitchen tiles), which go into the Book. Cheap with prebuilt scenes. You *see* moments but not the trip.
- **B. The room view, live**: tap the trip and the camera goes out of the tank into that room at tiny scale; you watch
  the notable walk, sneak, shoo a bug and pick things up (a simple scripted walk, like a diorama). Each room is one
  small set built from free kits (Kenney furniture, KayKit). Best fit for "seen", and gives a deep-session reason.
- **C. A + the return always live**: the trip is shown as postcards, but the walk back, climbing the rim and the
  tank's welcome always play live.

**Recommendation: start with C, plan for B.** The return is the moment that matters most, and it happens in the tank we
already build. B can come room by room.

## 5. Size, risk and timing

- **It's a new feature** and the project is in the **Polish phase** (no new features) with **P9 Placed life** queued.
  So this is a design for **after P9**, probably its own milestone (P10?).
- **New systems**: inventory, gear slots, a map of rooms, trip simulation, a finds collection, placement (shares
  research 18's placing). New art: the house rooms at tiny scale, bugs, ~20–30 finds.
- **Risk: it takes the eye off the tank.** The game is a tank you watch. If the best stuff happens outside, the tank
  gets dull. Fix: what comes back *changes the tank* (placed finds get used, gear is seen on people), and most trips are
  watched from the tank (C).
- **Risk: it turns into chores** (send, wait, send). Fix: one trip per visit in the village, no timers to skip, nothing
  rots if you don't send anyone.
- **Risk: it breaks the scale joke if it's overused.** The house is a mystery revealed slowly; a few rooms per box.
- **Across boxes**: is the house the same for every box (desert oasis, mountain village)? The same house with a new
  room per box could tie the shelf together: box 2's people find box 1 on the shelf next door.

## 6. Questions for the user

Answered 2026-11-07 (§0): outside gets real (1), village = the Keeper's room then other rooms (2), postcards (4),
no death (6), loot separate (7), explorer says "glass" (8), equipment shown in daily life (9), own milestone (10).

Second round answered 2026-11-07 (§0): 3 rope they build, 5 one/three, 11 partly (full analysis is milestone work),
12 camp keeps the breadcrumb, 13 relics come out during the milestone. Next: the milestone's research and spec.

Sources: Fallout Shelter exploration ([gamepressure](https://www.gamepressure.com/falloutshelter/exploration/z47a8d),
[Giant Bomb](https://giantbomb.com/wiki/Games/Fallout_Shelter),
[GameSkinny on stats](https://www.gameskinny.com/5qs4s/fallout-shelter-which-stats-are-best-for-exploring-the-wasteland),
[Android Central on quests](https://www.androidcentral.com/fallout-shelter-adds-quests-enhanced-combat-and-much-more));
Pikmin 2 treasures ([Pikipedia](https://www.pikminwiki.com/Pikmin_2_treasures),
[Modern Amenities Series](https://pikminwiki.com/Modern_Amenities_Series)); Tinykin
([GamingTrend review](https://gamingtrend.com/tinykin-review-captain-olimar-has-competition/),
[Quarter to Three thread](https://forum.quartertothree.com/t/tinykin-exploring-a-house-being-the-size-of-an-ant-my-goty-2022/157851)).
Neko Atsume, Arrietty, Grounded and the retainer/garrison systems are from memory.
