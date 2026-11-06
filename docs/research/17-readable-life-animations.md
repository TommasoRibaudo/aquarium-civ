# 17 · Life you can watch: animations that show what's happening

Written 2026-11-06. No Unity was run and nothing was tried on a phone: this comes from reading the code and the docs,
plus web research. Tags: **[V]** read in a primary source (the developer speaking, vendor docs, a published book),
**[S]** secondary (wiki, press, forum, review), **[I]** my own inference from this repo or from general practice.
File refs are `AquariumCiv/Assets/Game/...` unless they say otherwise. Line numbers are from `vs/333-knock-startle` at d79b184.

## 1. The problem

The user's playtest (2026-11-06, close to their words): *"A lot of the animations are abstract and don't help. It
feels like I'm looking at a dead world. Animations should be a big thing. Not realistic, cute, but they must show
what's happening. If it's hunting, someone is actually hunting and the bunnies actually run away, so I can stop for a
moment and watch. Gathering water, the same. Gathering at bushes, the same. Talking, playing: lots of kids are playing,
but they all do the same circle."* They liked seeing things visibly change (a new chief).

Put plainly, the user wants three things:

1. **A verb has an object.** The person doing it touches a real thing in the box (this bush, this rabbit, the water),
   not a spot near it.
2. **The object answers.** The bush shakes and loses a berry, the rabbit bolts, the water ripples, the bucket comes up full.
3. **Variety where people look most.** Kids and talkers are the most numerous and the most visible, and they repeat
   the same loop.

The scale limit is unchanged: a doll is about 30 px tall at full-box zoom (life-in-the-box §2) [I, est]. The fix is
not more detail on the doll. The fix is **bigger, slower and clearer cause and effect between a doll and the
thing it is working on**.

## 2. What we have

### 2.1 How a move is drawn

- `Life/LifeTypes.cs:7-38`: about 150 `ActivityKind`s. `View/DollMotion.cs:49-113` (`StyleOf`) folds them into
  about 50 `DollStyle`s. Each style is a pure function of time: arm angles, lean, squash, and a small offset
  (`DollMotion.cs:149-624`). No rigs, transform only (life-in-the-box §9).
- One prop per doll, picked in `View/PeopleView.cs:762-813` (`PropOf`). **Small props are not drawn at all**, and
  that includes `Prop.Ball` (it falls through to `_ => PropKind.None`, `PeopleView.cs:804`). So the kids' "ball"
  has never been on screen.
- `DollPose.hit` marks the contact frame of a stroke (`DollMotion.cs:32-33`, set at 199, 231, 246, 545). **Nothing
  reads it outside the tests**: no chips, no dust, no sparks, no dip on the thing that was hit. The doc comment says
  "later a puff of chips or sparks". That later has not come.
- Two people chatting take turns by loop step and a phase offset (`DollMotion.cs:336-341`, `PeopleView.cs:429`).
- Clock: the morning is 80 real s for 5 view hours, so **one view hour is about 16 s** (`Life/LifeClock.cs:36-43`).
  A 1.25-2 h play or work block is 20-32 s of one looping move.

### 2.2 Inventory

"Target" says whether the activity is placed at the real thing (a bush cell, a rabbit, a puddle) or at a fixed land
spot (`Woods`, `Pond`, `ForestEdge` are one cell each: `Life/SettlePlacePositions.cs:354-357`).
"Reacts" says whether anything other than the doll changes.

| Activity | Who / era | What the player sees now | Target | Reacts? | Reads at default camera? [I] | Should show |
|---|---|---|---|---|---|---|
| **Play** (`Day.cs:583, 612-620`) | Kids, all eras; most kid hours | `Hop`: hop round a **0.1 u circle** at the slot, arms swinging (`DollMotion.cs:381-393`). Prop is Ball (not drawn), Stick or none (`Day.cs:641`). Walking there: Run | Home or gathering place; pond, puddle or hill sometimes (`Day.cs:631-639`) | No | **No.** Every kid draws the same small circle; "playing" never reads as a game | Several games that **use space and each other**: tag, a ball passed round, hide and seek, chasing rabbits, splashing (§5 #1) |
| Toddle, Dance, Snowball | Babies; weddings (`Scenes.cs:94`), festival; snow | **Same `Hop` circle** (`DollMotion.cs:59`) | – | No | No. A baby, a wedding dance and a snowball fight all look the same | Toddle: wobble after the carer. Dance: a ring that turns as one. Snowball: a throw, a hit, a flinch |
| Swim / Splash | Kids, summer; mist reaction | `Swim`: body sunk 0.1 u, arms sweep (`DollMotion.cs:410-416`). On a mist puddle that may sink into dry ground [I, unchecked] | Pond or nudge | No | Weak | Splash rings, a jump in, kids splashing each other |
| Stomp / WashHands | Kids / adults at a mist puddle (#266) | Stamp in place; a crouch (`DollMotion.cs:400-408`; `Scenes.cs:460-465`) | **Real puddle** | No splash [I] | Fair | A drop puff on every stamp; ripples |
| **Gather** (berries) | Tribe and camp gatherers, anyone on tribe work (`Work.cs:80, 137-143`) | `Pick`: crouched, the right arm goes up and down, basket in hand (`DollMotion.cs:251-261`), facing the bush (`PeopleView.cs:620-626`) | **Real bush cell** (#288) | **Yes, but small**: a berry dot drops every 5 s (`BushView.cs:12-32`) | Half. The bush is real, but a 7-dot bush losing one dot is below what reads at phone size | The bush **shakes** on each pick, a berry **hops into the basket**, the basket **fills** |
| Gather (roots, before green) | Everyone at the tribe stage (`Work.cs:129-130`) | Same `Pick` at the stream bed | Fixed `Stream` | No | No (#310: "Are they gathering food? By the pond?") | A dig: dirt puff, a root pulled up and held up, dropped in the basket |
| Eat at a bush | Kids, summer/autumn (`Day.cs:593-594`, `Work.cs:70-75`) | `Sit` next to the bush; the bush loses berries | Real bush | Same small drop | No: sitting doesn't say "eating berries" | Standing at the bush, hand to mouth (the `Taste` style already exists, `DollMotion.cs:599-607`) |
| Eat (meals) | Everyone | `Sit` (`DollMotion.cs:54-55`) | Fire, home, field | No | No | Hand to mouth now and then; a bowl passed round |
| **FetchWater** (`Day.cs:714-721`, `Work.cs:101`) | 30 % of adults at dawn; cook | Walk with an empty bucket, then **stand still** for 0.2 h (~3 s; `FetchWater` has no style, so it falls to `Stand`, `DollMotion.cs:112`), then walk home with a full or muddy bucket (`PeopleView.cs:791-792`) | Pond, else a puddle or the stream (real once the pond exists) | No | **No.** The bucket fills while the doll stands there | Kneel, **dip the bucket under**, ripple ring, lift with effort, walk back leaning under the weight |
| **Hunt** (#283, `LifePlanner.Hunt.cs`) | Hunters, camp era only (`Hunt.cs:113`) | Fire → take spear → walk out → crouch-stalk a **real rabbit** → strike (wind-up and thrust, spear stays in the hand, `PeopleView.cs:509-532`) → miss: rabbit bolts, hunter shrugs; catch: rabbit over the shoulder, skin (`Prepare`), cook. A kid follows 30 % of the time and cheers a catch | **Real rabbit** | **Yes**: the target ignores the hunter until the strike (`RabbitView.cs:294-299`), then scatters; a catch removes it | Mostly. But the strike is **0.06 h ≈ 1 s** and the bolt ≈ 2 s (`Hunt.cs:72-73`), the spear never leaves the hand, and the stalk creeps 0.01 u (`DollMotion.cs:211-216`). Easy to miss; nothing pulls the eye there | Spear **flies** and lands; a hit-stop; other rabbits near also bolt; the hunter **holds up** the catch; the walk home is a small parade |
| Stalk at ForestEdge (non-eco) | Hunters off the ecology | `Crouch` at a fixed spot (`Work.cs:148`) | Fixed | No | No | (Legacy path; only matters off the eco land) |
| Fish (`Work.cs:149-152`, night `Day.cs:1038`) | Fishers, camp on | Rod out, slow bob, a tug every 4.5 s (`DollMotion.cs:274-284`) | Fixed `Pond` spot | **Nothing is ever caught** on screen | Half: the pose is clear, the payoff is missing | Every few tugs, a fish **flips out** in an arc into the basket, with a splash ring |
| Chop (Woods) | Woodcutters, camp on | `Chop`: wind-up, fast strike, body dip (`DollMotion.cs:218-235`) | Fixed `Woods` cell, not a particular tree | No (no chips, the tree doesn't shake) | The rhythm reads; the cause and effect doesn't | Tree **shakes**, chips puff on `hit`; now and then a tree falls (ties to keepers-hands §8.3) |
| Knap / ThrowPot | Toolmaker at the fire; potter | `SitStrike`, `hit` flag set, unused | Fire | No | Weak | Sparks or chips on the hit; the finished spear handed to a hunter (`Work.cs:159` already says "hands tools to the hunters") |
| Hoe, Sow, Weed, Harvest | Farmers, village on | Distinct rhythms (`DollMotion.cs:237-272`) | Field lot | No dirt, no crop change per stroke | Rhythm reads, effect doesn't | Dirt puff on the hoe's hit; harvest leaves a cut row |
| Carry / Stack / Prepare (stash, tents) | Everyone | Walk with load; `Lift` bend | Stash pile, tent frame | Pile grows (#150); tent goes up (#284) | Good: carrying is our clearest signal (life-in-the-box §1, Banished) | Keep; add a set-down bounce |
| Tent raising (#284, `Tents.cs:180-221`) | Builders + one kid | Carry, `Hammer`, watch, cheer | **Real tent lot** | Tent appears | Good | The hammer's `hit` → dust; the frame wobbles |
| **Chat / Court / Visit** | Adults; evenings at the fire | `Talk`: the talker bobs and lifts an arm, the other is still; they swap by loop step (`DollMotion.cs:329-345`). Chatter marks overhead (`ChatterView.cs`, research 12) | Partner (faces them, `PeopleView.cs:420`) | The marks | Half. Turn-taking exists but is a 6 mm bob; marks carry most of it | Listener **nods**, speaker **gestures at** the listener, a laugh beat, sometimes a hand-over or a hug |
| Argue / Mediate | Rivals | Fast bob, arm up (`DollMotion.cs:337-340`) | Partner | – | Fair | A step in, a step back; one storms off |
| StoryTell / Listen | Elders, kids at the fire | `Speech` vs `Sit` | Ring | No | Fair | Listeners lean in; kids jump at the scary bit |
| Sit, SitOut, Nap, Sleep, Indoors | Everyone | Sit with a slow sway; Nap lies down; Sleep/Indoors hidden | – | – | Fine as rest | Rest is fine. The problem is when **most** dolls on screen are resting |
| WatchWork | Apprentices, elders | `Stand` | Master | No | No | Copy the master a beat late (already done for `CopyMaster`, `Day.cs:644-660`); point now and then |
| Reactions to the Keeper and glass, relic moments (#92, #161) | Everyone | Many one-shot styles told once: Peer, Knock, Flinch, HopBack, etc. (`DollMotion.cs:459-615`) | Glass, relic, knock point | Yes | **Good**: these are the best we have, because they play once with a clear beginning, middle and end | The model for the rest |
| Rabbits (#282, `RabbitView.cs`, `RabbitDoll.cs`) | – | Graze, sit up, hop between green cells; scatter when someone comes close; a knock startles the close ones (d79b184) | – | They are the reaction | Good | Kids chasing them (§5 #1); the hunt's other rabbits bolt too |
| Pond life (`PondLife.cs`), bush bird (`BushView.cs`) | – | Frogs dive and a duck moves off when a person comes close (`PondLife.cs:27`) | – | Yes | Good, but rare | Use them as the "answer" more often |

Debug cheats that already exist: "Start a hunt now", "Next hunt succeeds / misses", "Spawn rabbits here", "Rabbit
tracks now", "First bush now", "Puddle visit", "Puddles now", "Ducks now", "Frog(s) now", "Force chat", "Bicker"
(`View/SliceDebugPanel.cs`, `SimDebugPanel.cs`, `EcoDebug.cs`).

### 2.3 What the inventory says

- **The model is right, and the gaps are specific.** The best parts (the hunt's real rabbit, the bush cells, the
  relic one-shots, the tent raising) already follow "the verb touches its object". The rest are loops with no target
  and no answer.
- **Three cheap misses explain a lot of the "dead" feel** [I]: the unused `hit` flag (no work ever makes a puff), the
  ball that is never drawn, and `FetchWater` falling back to `Stand`.
- **The `Hop` circle carries five different activities** (Play, Toddle, Dance, Snowball, and the wedding dance).
  Kids are a large share of the dolls on screen in the day, and play is most of their hours (`Day.cs:577-621`). That
  matches the user's "they all do the same circle" exactly.
- **The hunt is built but quick.** The beats that pay off (strike, bolt, catch) are 1-2 s inside a hunt of about
  30 s, and nothing marks that one is going on. A player who isn't already watching that corner misses it.

## 3. What other games do

The evidence here is thin. Few developers write about *why* small figures read, so much of this table is [S] or [I].

| Game | What they do | Lesson for us | Evidence |
|---|---|---|---|
| **The Settlers** (Blue Byte) | Each trade has its own work animation (the woodcutter fells a tree, the stonecutter strikes stone), and every step of the goods chain is a figure carrying something visible between buildings. The German word for the appeal is *Wuselfaktor* (the ant-farm bustle) | Each work verb gets its **own** animation **and** its effect on the world. The chain is watched, not read | Reviews and forums [S]: [Hexus](https://hexus.net/qaikn), [POV-Ray forum](https://news.povray.org/povray.off-topic/thread/%3C4f7d783c%241%40news.povray.org%3E/), [heise](https://heise.de/-11260484) |
| **Banished** | Its solo developer first set a firm "NO ANIMATION" rule ("a box for characters that hopped around"), then added simple humans. For his next game he plans "a walk cycle holding each and every tool" and "different animations for every tool and thing the villagers can do" | Even a dev who chose no animation for cost concluded that per-task animation is what the next game needs | Luke Hodorowicz, [Development Toys](https://shiningrocksoftware.com/2021-03-15-development-toys/) [V] |
| **Timberborn** | The most-voted kind of player request is "more beaver animations, immersive visuals, idle activity": task-specific actions like gnawing trees | Players of a much-praised builder still ask for the verb to be shown. This is a common, lasting want | [Feature Upvote thread](https://timberborn.featureupvote.com/suggestions/219933/more-beaver-animations) [S] |
| **Kingdom Two Crowns** | Archers wander out by day and shoot rabbits and deer, which flee; a kill drops a coin you can see. Rabbits come from tall grass and vanish in winter | Hunting as an **ambient show** of hunter, fleeing prey and a visible payoff. Very close to our rabbits | [Kingdom wiki](https://kingdomthegame.fandom.com/wiki/Archer) [S] |
| **Pikmin** | Tiny characters carrying a big object together "looked just like ants carrying a cicada"; the sound changes with how many carry. The leaf exists because "the character was tiny" and needed something to stand out | **Group carrying** and one big readable feature beat detail. Scale reads through comparison with big objects | Nintendo, [Ask the Developer Vol. 10](https://www.nintendo.com/whatsnew/ask-the-developer-vol-10-pikmin-4-part-1) [V] |
| **Lemmings** | Characters about 8 × 10 px. Each job (dig, build, block) **changes the terrain** as you watch, which is how a tiny sprite reads | At 30 px the **effect on the world** carries the verb more than the pose does | Mike Dailly via [PCGamesN](https://www.pcgamesn.com/the-making-of-lemmings) [S] |
| **The Sims** | "Make the people really dumb, and the environment and objects really smart." The animations for using an object live **with the object**. A two-Sim interaction is its own object | Put the animation and the answer **on the target** (bush, pond, rabbit), not only on the doll. A two-person moment is its own little object with a shared clock | Will Wright via [PC Gamer](https://www.pcgamer.com/games/the-sims/will-wright-says-the-original-sims-ai-was-actually-too-good-almost-anything-the-player-did-was-worse-than-the-sims-running-on-autopilot) [S]; Forbus and Wright, [Programming Objects in The Sims](https://qrg.northwestern.edu/papers/Files/Programming_Objects_in_The_Sims.pdf) [V] |
| **Townscaper** | Birds land on new roofs, leave when you crowd them, come back; nothing explained | Small, unexplained answers to what's going on make it feel alive | Reviews [S] ([PC Gamer interview](https://pcgamer.com/townscapers-developer-on-how-its-radically-casual-design-is-inspiring-a-new-wave-of-low-stress-builders-to-adapt-the-blueprint) has no word on the birds) |
| **Foundation** | The devs added a festival "to break the rhythm of the villagers' daily toil" | Loops go stale; set pieces break them | Already in life-in-the-box §1 [V] |
| **Spiritfarer** | The hug grew into "a core part of the game's heart"; nearly everyone can be hugged | One warm two-person animation, used often, can carry a lot of feeling | Nicolas Guérin, [Game Developer](https://gamedeveloper.com/design/inside-the-thoughtful-design-of-thunder-lotus-i-spiritfarer-i-) [V, interview] |
| **Rain World** | Creatures are animated procedurally, and the code is written so a lizard's movement **shows its intent** (hunting looks like hunting) | Procedural motion can read as intent if it's driven by the goal (the target), not by a timer | [GDC 2016 Animation Bootcamp](https://gamedeveloper.com/art/video-animating-i-rain-world-i-and-its-many-squishy-stretchy-creatures) [V, talk; I didn't watch it all] |
| **"Juice it or lose it"** (Jonasson and Purho, 2012) | Layering tweens, squash and stretch, particles and small sounds onto the same simple game makes it feel alive | Puffs and squash on contact are the cheapest life there is | [Talk summary](https://gamejuice.pages.dev/resources/juice-it-or-lose-it) [S] |
| **The Illusion of Life** (Thomas and Johnston) | The 12 principles. **Staging**: show one idea clearly. **Anticipation**, **squash and stretch**, **follow-through** | At 30 px, exaggerate: a big wind-up, a hold on contact, a follow-through on the object | Book [V]; [summary of squash and stretch](https://en.wikipedia.com/wiki/Squash_and_stretch) [S] |

Not found: anything with numbers on **how many** "interesting" scenes should be on screen at once, or measured
readability thresholds for tiny 3D figures on phones. Searches on Against the Storm, Tiny Glade, Anno and Foundation
turned up nothing about how their figures are animated. The density and timing figures in §4 are mine [I].

## 4. Principles for us

Each one is meant to be checked by a tester agent with a screenshot or a short capture.

1. **The verb touches its real target.** Every work verb stands at, and faces, a real thing in the box: this bush,
   this rabbit, this water's edge, this tree. No fixed `Woods`/`Pond`/`ForestEdge` spot where a real thing exists.
   *Test:* pause during the activity and the doll's hand or tool is within one doll-width of the thing.
2. **The target answers on the contact frame.** Every `hit` makes a puff (chips, dust, droplets, sparks) and moves
   the target (a shake, a ripple, a bolt, a berry jump). The user's "the bunnies actually run away" is the rule.
   *Test:* the target visibly changes within 0.2 s of the hit.
3. **Every animal reacts to people.** Rabbits already do. Frogs, ducks, birds and later sheep follow the same rule.
4. **A 3-second read at the default camera.** Someone who looks at a doll for 3 s can say what it's doing, in a
   word ("fishing", "playing tag"), with the sound off. *Test:* the tester names 5 random dolls from a screenshot plus
   a 3 s look.
5. **A clear end beat.** Each work loop ends in something you can see once: the full bucket lifted, the fish in the
   basket, the rabbit held up, the berries tipped onto the pile. A loop with no end beat is ambience, not a story.
6. **No two neighbours do the same thing in step.** Kids next to each other play different games, or the same game
   in different roles (chaser and runner). *Test:* in any screenshot of the green, no 3+ kids share one move.
7. **Something to watch is always on screen** [I, est]. In daylight at the default camera there is at least
   **one story in progress** (actor, verb, target, answer) at any moment, and **a new one starts every ~10 s**. When
   the sim has nothing on, the planner starts a cheap one (kids chase a rabbit, someone draws water).
8. **Exaggerate, then hold.** Moves are 1.5-2× real size (like the props, Choice 5). Wind-up before strike, a 0.1 s
   hold on contact, a follow-through on the target. Cute, not realistic.

## 5. Animation upgrades, by impact

Cost: **S** about a day of agent work, **M** 2-4 days, **L** a week or more [est]. Each comes with a debug cheat so
the user and the tester can reach it on demand (CLAUDE.md "Debug panel stays current").

| # | Upgrade | Why it's high | Cost | Issue | Cheat |
|---|---|---|---|---|---|
| **1** | **Kids play real games.** Replace the one `Hop` circle with 4-5 small group games run by a "play group" with its own shared clock: **tag** (a chaser runs at a runner, a touch swaps roles, squeals as chatter marks), **ball** (a drawn, oversized ball kicked between 2-3 kids in arcs, rolling to a stop), **hide and seek** (one counts face-down at a tent, the others crouch behind bushes or tents, found → run back), **chase the rabbits** (kids run at the herd, rabbits scatter with `RabbitHerd.Scare`, kids give up and laugh), **splash** (pond or puddle: drop puffs, two kids splashing each other). Toddle becomes a wobble after the carer. Dance becomes a turning ring. | Kids are the most numerous moving dolls by day, and this is the user's sharpest complaint | M | new (touches `Day.cs` KidDay/PlayKind and `DollMotion`) | "Kids play: tag / ball / hide / chase rabbits / splash" (one button each, at the gathering place) |
| **2** | **Contact puffs and target shakes.** Consume `DollPose.hit`: one pooled puff (chips at a tree, dust at a hoe, sparks at the knapper, droplets at water, grass bits at a strike) plus a shake on the target (bush, tree, tent frame, stone). One mesh, one draw, like `ChatterView`. | Turns every existing work loop from "a doll waving" into "a doll doing something to something". The flag is already computed | S | new | "Puffs on/off" (to compare), "Shake all targets once" |
| **3** | **Drawing water, readable.** Give `FetchWater` its own style: kneel at the real water's edge, dip the bucket under, a ripple ring on the water, a beat, lift with a lean, walk home slower and lower with the full bucket. Muddy water: a brown puff. | The user named it. Cheap, and it happens every dawn | S | new (P8 water, near #266/#267) | "Water run now" (sends 2-3 people with buckets) |
| **4** | **Berry picking you can see.** On each pick: the bush shakes, a berry dot jumps in an arc into the basket, the basket shows a few berry dots (fuller with each pick), and the end beat tips them onto the stash. Kids eat standing at the bush with `Taste`, not sitting. Root digging gets its own dig, with a dirt puff and a root held up. | Named by the user. The target is already real (#288), only the answer is too small | S | #288 (and #310) | "First bush now" (exists), "Pickers to the nearest bush now" |
| **5** | **The hunt, made watchable.** Longer stalk with visible creeping (bigger steps, pauses when the rabbit sits up), the **spear flies** in an arc and sticks in the ground; a 0.1 s hold on the hit; on a miss the rabbit zigzags off, **other rabbits nearby also bolt**, and the hunter walks to pick up the spear; on a catch the hunter **holds it up** (`HoldUp` exists) and the kid cheers, and the walk home is slow enough to notice. Optional: people at the fire turn to look when the hunter comes back. | It's the main sight of the camp era (#283). It's already right in structure, only too quick and easy to miss | M | #283 | "Start a hunt now", "Next hunt succeeds / misses" (exist); add "Slow-mo the next hunt" for sign-off |
| 6 | **Talk with gestures and two-person moments.** The listener nods on the talker's beats. The talker turns an open hand toward the listener. A shared laugh (both bob) closes a chat. New pair beats, each with a shared clock: **hand over** (berries, a spear from the toolmaker to the hunter, a bowl at the fire), **hug** (a return, a reconcile, a wedding), **walk together** side by side. | Talking is most of the evening. Pair beats are the "they have relationships" signal (Spiritfarer, Sims) | M | #327 (chatter) or new | "Force chat" (exists), "Hand over now", "Hug now" |
| 7 | **Fishing pays off.** Every 2-4 tugs, a fish flips out in an arc into the basket, with a splash ring and a frog or duck moving off. | One small change makes a loop a story | S | #286 or new | "Fish bite now" |
| 8 | **Wood comes from a tree.** Woodcutters work a real grown tree cell: the tree shakes on each hit, chips fly, and now and then a small tree falls slowly, then gets cut into logs that are carried. | Needs the forest (beat 5) and the felling question (keepers-hands §8.3) | M-L | new, after keepers-hands §8.3 | "Fell a tree now" |
| 9 | **Meals and the fire.** Hand to mouth at meals; one person passes bowls round the ring; someone pokes the fire and sparks rise; the cook stirs a pot with steam. | Evening is when most people are on screen together | S | new | "Meal now" |
| 10 | **"A story starts" pacing.** A small director (in the planner) checks every few seconds whether a story in progress is visible on screen; if none, it starts a cheap one near the camera's focus (kids chase a rabbit, someone draws water, a pair hands something over). | Puts principle 7 into practice. Without it, good animations can still all happen off screen | M | new | "Story density: off / normal / high" |
| 11 | Snowball fight: a throw arc, a snow puff, a flinch, a return throw. | Seasonal; cheap once #1's play group exists | S | new (after #1) | "Snow day" (if not already there) |

Top five by "dead world" impact: **#1 kids' games, #2 contact puffs, #3 water, #4 berries, #5 the hunt.**
#2 comes first in build order, because #3, #4, #5, #7 and #8 all use its puff and shake.

## 6. Technical approach on mobile with our peg dolls

**Stay procedural and transform-only** [I]. Our dolls are rigid parts (body, two stub arms, one prop). Skinned
animation, vertex animation textures (VAT) and GPU skinning solve a problem we don't have: many *deforming* meshes.
Unity's own crowd post says their texture-baked instancing exists because "skinning runs on the CPU and is
submitted to the GPU one character at a time" ([Unity blog](https://blog.unity.com/engine-platform/animation-instancing-for-skinnedmeshrenderer) [V]).
Ours have no skinning to move. Around 60 dolls × 4 transforms is a few hundred transform writes a frame, inside the
≤ 0.5 ms that `PeopleView` already measures (`PeopleView.cs:53`) [I, to confirm on the phone].

What's new is not the doll's pose. It's **events, objects and shared clocks**:

1. **One-shot beats on top of loops.** The relic moves (`Peer`, `Knock`) already play a story once from
   `progress`/`since` (`DollMotion.cs:145-148`). Use the same pattern for anticipation → act → hold → follow-through
   (the bucket dip, the spear throw, the held-up rabbit). Easing curves only, no keyframe assets.
2. **A hit event.** When a doll's `hit` goes from 0 to 1, `PeopleView` raises `WorkHit(who, place, style, worldPos)`.
   A new `HitFx` pool draws the puff, and the target's own view (`BushView`, a tree, the pond) reacts. This follows
   The Sims' idea: the **object** owns its answer.
3. **Puffs: one mesh, one draw.** Build them like `ChatterView`: a handful of shapes made once with `MeshBuilder`,
   rebuilt in world space each frame for the live puffs (at most ~20). Or use one shared `ParticleSystem` with
   `Emit()` calls. Both are a single draw call [I]. Keep the colours in the palette.
4. **Shared objects with no physics.** The ball, the thrown spear, the leaping fish and the flying berry are
   pooled transforms on **parametric arcs** (start, end, height, time), owned by whoever runs the beat (the play
   group, the hunt plan). It's deterministic and cheap, and the planner and the view always agree. The hunt already
   works this way (`HuntStep` coordinates).
5. **Group and pair controllers.** A play group or a pair beat holds a shared clock and the roles (chaser and runner,
   giver and taker) and hands each member a target point and a facing. That's how the chat turn-taking works today
   (`PeopleView.cs:429`), only with roles instead of a phase offset.
6. **Rendering.** Keep the SRP Batcher path the dolls use now. In URP, GPU instancing and the SRP Batcher don't mix;
   a `MaterialPropertyBlock` takes a renderer off the batcher ([Unity 6.3 manual](https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html) [V]).
   So no per-doll property blocks for tints: use the palette texture (research 07). Shader-side wobble (a vertex sine
   on bushes and trees for the shake) is fine and costs nothing on the CPU.
7. **Cost guard.** Beats and puffs run only for dolls inside the camera view. Off-screen dolls keep their loop
   math, which already costs little. The story director (#10) works from the view rectangle.

## 7. Open questions for the user

1. **Which kids' games first?** I'd start with tag, ball and chasing the rabbits (they use what's already there).
   Hide and seek needs cover (tents, bushes) and is subtler. Do you want splashing too, or keep water for the adults?
2. **Chasing the rabbits:** fine for kids to scare the herd now and then, or should kids leave the rabbits alone
   so the hunt stays the only rabbit story?
3. **The thrown spear:** a throw (the spear flies and sticks) or a lunge (it stays in the hand)? #283 says "throws or
   lunges". A throw reads better at our size [I].
4. **How busy?** Principle 7 suggests a story in progress at all times and a new one every ~10 s. Is that the calm
   you want, or too busy for a terrarium you leave open?
5. **Slow the key beats?** Should the strike, the dip and the catch play in slow motion for a moment (a hit-stop of
   ~0.1 s, or a bit longer), even if it bends the clock a little?
6. **Hugs and handing things over:** a warm "they love each other" signal. Is a hug in tone for faceless peg dolls,
   or too much?
7. **Puffs:** chips, dust and droplets, cartoon-style (a few solid shapes in the palette) or soft and subtle?
   I'd go cartoon-style for readability.
