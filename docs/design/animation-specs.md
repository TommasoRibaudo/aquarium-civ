# Animation spec sheets: B1–B71, the staged moments, placed life, people and things (A3, #356; P9, #416; #691)

Status: **draft, waiting for the user** (2026-11-06). One beat sheet per animation of the Visit loop. Design:
[visit-loop.md](visit-loop.md) §2.6 and §4; research [17](../research/17-readable-life-animations.md) §4 (the eight
principles, P1–P8 below) and §5–6. Every number is [est] until it has been looked at in AnimBench and in an AnimLab clip.
**P9 (2026-11-08):** B12–B26 and the shared quadruped (P0) cover the Placed life milestone (#424–#446), in the part
"Placed life" below.
**Batch 3 (2026-11-15):** B29–B71 cover people and things (#692–#695), the fish at the glass (#708) and the nest (#709), in
the part "Batch 3" at the end; the user's checklist is [batch3-approvals.md](batch3-approvals.md).
No Unity was run for this file. File refs are `AquariumCiv/Assets/Game/...` on `vl/visit-loop` at 20dc42e.

The user (2026-11-06): *"The animation needs to be very well done."* Cute, not realistic, but each move shows what is
happening. A doll is about 30 px tall at full-box zoom on a phone, so everything is 1.5–2× real size, and slow
enough to follow.

**Decided:** the spear is thrown and sticks; kids chase the rabbits and play in the woods and the water; puffs are
cartoon-style; how busy the box is scales with its population.
**Not decided (both are written as optional variants; the user picks on a clip):** hugs between peg dolls (B7, C3);
a short hit-stop on key beats (§0.8).

---

## 0. Shared vocabulary (A1's beat kit implements these names)

### 0.1 Units, scale, clock

| What | Value | Source |
|---|---|---|
| Box | 6 × 4.4 u; site cell 0.1 u; pond water 0.08 u below the land | `Sim/Settle/Terrain.cs:95`, `SiteMap.cs:15` |
| Adult doll | 0.213 u tall (body 0.13, head r 0.043), shoulder 0.046 u off centre, arm 0.075 u, tunic r 0.055 u | `View/PegDoll.cs:31-35` |
| Kid doll | × 0.68 (0.145 u tall) | `PegDoll.KidScale` |
| Zoom scale | dolls are × 1.2 at full-box zoom (`CameraReach.DollScale`). **Puffs and arc objects use the same factor** | `View/Light/Core/CameraReach.cs:24-30` |
| On screen | full box: 1 u ≈ 120–140 px, so **0.01 u ≈ 1.3 px**. Smallest motion that reads: **0.025 u (≈ 3 px)**; aim for 0.04 u | [I] |
| Pose units | `DollPose.offset` is doll-local adult units (x right, y up, **z back**: −z is forward), × doll scale × zoom scale. Arm Euler: x = swing forward (0 hangs down, 90 straight ahead, 180 straight up); +z raises the right arm out to the side (left uses −z). Rest (0,0,∓14). `lean` + tips forward about the feet | `View/DollMotion.cs:19-36` |
| Clock | real seconds per view hour: **dawn 10, work 16, midday 25, evening 16.7, night 5** | `Life/LifeClock.cs:29-43` |
| Walk | 0.31 u/s real at work hours (`RealWalkSpeed`) | `Life/LifePlanner.cs:569-574` |

Today's small offsets don't read: the talk bob is 0.006 u (under 1 px), the stalk creep 0.01 u. **Every new offset in
these sheets is at least 0.025 u.**

*Kit (#355):* nothing new: offsets and arm angles stay `DollPose`'s; one-shot beats ride on top of it (`View/Beats/Beat.cs`).

### 0.2 Beat phases
A **beat** is one told-once action: a list of phases, each with a duration, an easing and the pose keys it moves
between. It runs on `since` (real seconds into the beat), like `Knock` and `Peer` do today (`DollMotion.cs:533-567`).

| Phase | What it is |
|---|---|
| `Approach` | walk or turn into place, facing the target |
| `Antic` | anticipation: the wind-up, the opposite way to the act (a crouch before a jump, the arm back before a throw) |
| `Act` | the fast part (a strike, a throw, a lift) |
| `Contact` | **one frame window**: `DollPose.hit` goes 0→1 here and A1's `WorkHit(who, place, style, worldPos)` fires **once** |
| `Hold` | a held pose right after contact (≥ 0.1 s, P8) so the eye catches it |
| `Follow` | follow-through: overshoot and recoil of the doll, and the target's answer playing out |
| `Settle` | back to the idle or the next loop |
| `EndBeat` | the payoff seen **once per loop** (the full bucket lifted, the fish in the basket): P5 |

*Kit (#355):* `BeatPhase` (+ `Done`), `Beat` (`Beat.Step(phase, s, ease, PoseKey)`, `Sample`, `Pose`/`Apply` over the loop pose, `ContactBetween`), `PoseKey`; the Contact sets `DollPose.hit` and `PeopleView.WorkHit` fires once (`HitEdge`, `WorkHitPoint.HitPoint` = the tool's end).

### 0.3 Easing names
`Linear` · `In` (quad, speeds up) · `Out` (quad, slows down) · `Smooth` (smoothstep, today's `Smooth`) · `Snap`
(cubic in; strikes, ≤ 0.15 s) · `Back` (out-back overshoot, s = 1.7; settles, lifts) · `Bounce` (ball landing:
two bounces at 35 % then 12 % height) · `Wobble(A, f, τ)` (damped sine A·e^(−t/τ)·sin 2πft; shakes, quivers) ·
`Pop` (scale 0 → 1.2 → 1 over 0.15 s; something appearing).

*Kit (#355):* `Ease` + `Easing.Apply(ease, u)`; `Easing.Wobble(t, A, f, τ)`, `Easing.WobbleSeconds`, `Easing.Pop`, `Easing.BounceHeight` (`View/Beats/Easing.cs`).

### 0.4 Puff kinds (B1 draws them; one pooled mesh, one draw, like `ChatterView`)
At most 24 live puffs, 160 bits. Flat low-poly bits in palette colours, no alpha: they **shrink to 0** over the last
40 % of their life (cartoon, and it keeps the SRP Batcher path, research 17 §6.6).

| Kind | Bits | Bit size (u) | Launch | Gravity (u/s²) | Life (s) | Colour |
|---|---|---|---|---|---|---|
| `Chips` | 6 (5–7) | slabs 0.028 × 0.012, spin 720°/s | 0.7–1.0 u/s, cone 50° about "away from the cut face", 35–60° up | 3.0 | 0.5 + 0.2 lying on the ground | pale wood, 1 in 3 bark |
| `Dust` | 3–4 round blobs | r 0.02 → 0.05 (`Out`) | 0.15–0.3 u/s out along the ground, rise 0.04 | 0 | 0.45 | dry soil; wet soil when wet |
| `Sparks` | 5 + 1 flash star | diamonds 0.012; star 0.045 for 0.07 s | 0.5–0.9 u/s, cone 30° about up | +2.0 (knap) / −0.6 (fire: floats up) | 0.35 / 0.7 | ember, yellow (gold for the chief, C3) |
| `Droplets` | 6 (4–10) | teardrops 0.016, stretched ×1.6 along the velocity | 0.7–1.1 u/s, cone 35° about up, or **aimed** at a point | 4.0 | 0.4 | pale water blue; `Mud` = brown |
| `Ring` | 1–2 flat rings on the water | r 0.03 → 0.20 (`Out`), line 0.012 → 0.004 | at water level + 0.002 | – | 0.8; the 2nd ring 0.15 s later at 0.7× | white-blue; brown on muddy water |
| `Grass` | 4 blades | 0.022 × 0.006 | 0.4–0.6 u/s, cone 60° about up | 2.5 | 0.4 | season green |
| `Leaves` | 1–3 (6 on a tree fall) | diamonds 0.02 | from the crown, drift 0.1 u/s, flutter roll ±40° at 3 Hz | falls at 0.35 u/s | 1.2 | season leaf |
| `Steam` | 2 blobs | r 0.025 → 0.05 | rise 0.12 u/s, sideways sine 0.02 u | 0 | 1.2 | off-white |
| `Snow` | 5 | cubes 0.014 | 0.6 u/s | 3.0 | 0.4 | white |

*Kit (#355):* not in the kit (B1 #357 draws them); they start from `PeopleView.WorkHit(who, place, style, worldPos)`.

### 0.5 Answers (what the target does; the object owns its answer, The Sims rule)

| Answer | Motion | Default numbers |
|---|---|---|
| `Sway` | rotate about the base, away from the actor, `Wobble` | bush 11°, 6 Hz, τ 0.18 s (≈ 0.5 s visible); tree crown 6°, 4 Hz, τ 0.3 s (raised from 7° / 4° in the batch-1 Editor pass: they didn't read on a phone) |
| `Jolt` | drop 0.012 u and back, `Wobble` 8 Hz τ 0.12 s | tent frame, stone, stash pile |
| `Squash` | scale y 0.88 → 1, `Back`, 0.2 s | basket or bucket set down, the pile when a load lands |
| `Pop` | scale `Pop` | a berry in the basket, a fish, a new accent |
| `Ring` | puff kind above | any water contact |
| `Bolt` | `RabbitHerd.Scare`: ears up `NoticeSeconds` 0.45 s, then flee 1.6 u/s | rabbits (`Sim/Eco/RabbitHerd.cs:425-462`) |
| `Flee` | a frog hops in, a duck paddles off | `View/PondLife.cs:27` (`FrogFlee` 0.32, `DuckFlee` 0.30) |
| `Flare` | fire flames scale 1 → 1.3 → 1, `Back`, 0.35 s, + `Sparks` | the fire |
| `Fill` | the container prop swaps empty → full at contact, with a `Pop` | bucket, basket dots |
| `Turn` | bystanders within 0.8 u face the event for 1.5 s (`PeopleView.LookAt`, `PeopleView.cs:821`) | onlookers |

*Kit (#355):* not in the kit: each answer is its target's view; the shakes use `Easing.Wobble`, `Pop` uses `Easing.Pop`, `Squash` `Ease.Back`.

### 0.6 Arc objects (pooled transforms on parametric arcs, no physics)
`Arc(start, end, h, T, spin, landing)`: position = lerp(start, end, u) + up · 4h·u(1−u), u = t/T (`Linear` in time:
it reads as a throw). `h` is the apex above the straight line. The object's pitch follows the tangent unless `spin`
is set. **Start is the hand** (shoulder + arm, rotated by the pose), end is a real thing. Owned by whoever runs the
beat (play group, hunt plan, fisher), so the planner and the view agree.

| Object | Size (u) | Landing | Used by |
|---|---|---|---|
| `Ball` | r 0.035 (bigger than a kid's head), bright accent | `Catch` (into the arms) or `Bounce` + `Roll` (decelerate to a stop in 0.8 s) | B2 |
| `Spear` | 0.20 long (prop) | `Stick`: stops at the landing tangent clamped to 30–50° off vertical, `Wobble` 6°, 10 Hz, τ 0.25 s | B6 |
| `Berry` | r 0.022 (1.6× the bush dot, `BushView.cs:215`) | `DropIn`: into the basket, basket `Pop` | B5 |
| `Fish` | 0.05 long, silver-blue, two flips (720° pitch) | `DropIn` | B8 |
| `Root` | 0.06 tan carrot | `DropIn` | B5 |
| `Snowball` | r 0.02 | `Burst` (`Snow` puff on whoever it hits) | B2 (stretch) |
| `Splash` | – (a `Droplets` puff aimed at a point) | – | B3 |

*Kit (#355):* `ArcShot.Throw(start, end, h, T, landing, launchAt, spin)`, `Landing` (Catch, Stick, BounceRoll, DropIn, Splash, Burst), `Arc.Sample` → `ArcSample` (position, rotation, `ArcStage`), `Arc.LandedBetween`, `ArcPool` (Launch, Sample, Release, Sweep) (`View/Beats/Arc.cs`). The meshes are the drawer's.

### 0.7 Groups and pairs (shared clock, roles)
A **group** (play group, dance ring, staged moment) or **pair** (talk, hand-over, walk together, hug) holds one clock
`t0`, a beat index, and per member a **role**, a target point and a facing. Members never run their own loop while in
it. Roles:

- Play group: `Chaser` / `Runner` (tag), `Thrower` / `Catcher` / `Fetcher` (ball), `Charger` (rabbit chase),
  `Splasher` (splash), `Seeker` / `Hider` (hide and seek), `Circler` (woods), `Dancer` / `Centre` (ring),
  `Carer` / `Toddler`.
- Pair: `Talker` / `Listener`, `Giver` / `Taker`, `Lead` / `Side` (walk together), `HugA` / `HugB`.
- Onlookers: `Watcher` (`Turn`), `Cheerer`.

*Kit (#355):* `GroupRole`, `BeatGroup.Cue(i, t)` → `MemberCue` (role, target, face, round, since, desync); `RingGroup` (Dancer/Centre, Watcher), `PairGroup` + `PairKind` (Talk, HandOver, WalkTogether, Hug), `ChaseGroup` (Chaser/Runner) (`View/Beats/BeatGroup.cs`).

### 0.8 De-sync and optional variants
- **De-sync (P6):** every doll's loop already gets its own phase (`PeopleView.cs:428`). In a beat, each member also
  gets: a start delay 0–0.6 s, period × (0.9–1.1) and amplitude × (0.85–1.15), all from a hash of its key (never the
  sim's random stream). Two groups of the same game stand at least 1 u apart; if not, the second picks another game.
  **Exempt:** the dance ring and the festival's last cheer move in step on purpose (that's what a ring dance is).
- **`HitStop` (optional, flag, default off):** on a key contact the actor, the target and the arc object freeze for
  **0.1 s**; the puff spawns as the freeze ends. The plan clock keeps running; the view catches up by shortening that
  beat's `Follow` by 0.1 s. Key contacts: spear lands, fish yanked, tag touch, bucket dip, "found!", tree lands.
- **`Hugs` (flag, on since the user's call 2026-11-06):** B7 and C3; the panel can turn it off.

*Kit (#355):* `Desync.Of(key, salt)`, `Desync.InGroup(seed, i, n)` (no three start within 0.15 s), `Desync.InStep`, `BeatHash` (own seeded hash); `BeatFlags.HitStop` (`Beat.Clock`/`Sample(since, hitStop)`), `BeatFlags.Hugs` (`View/Beats/BeatDesync.cs`, `Beat.cs`).

### 0.9 Principle tests (AnimBench; research 17 §4)

| Code | Test (AnimBench, from `track.json`) |
|---|---|
| P1 Touch | on the `hit` frame the hand or tool tip is within **0.11 u** (one doll-width) of the real target |
| P2 Answer | the target's state changes within **0.2 s** after `hit` |
| P3 Animal | every animal within its alert radius of the action reacts |
| P4 Read | (tester, not AnimBench) names the action in a word from a 3 s look at the default camera, sound off |
| P5 End | each loop has one `EndBeat` phase, and it isn't skipped when the block ends early |
| P6 Desync | no 3+ neighbours within 0.6 u share the same style with phases within 0.15 s (exempt: ring, festival cheer) |
| P8 Hold | every `Contact` is followed by a `Hold` ≥ 0.1 s; key offsets ≥ 0.025 u; key arm swings ≥ 40° |
| Arc | an arc starts within 0.02 u of the hand and ends within 0.03 u of its target |

P7 (a story always in view) is the director's (B11) test.

*Kit (#355):* P1 Touch: `WorkHitPoint.HitPoint` is the `worldPos`; P6: `Desync.InGroup`; P8: a `Beat`'s Contact is ≥ `Beat.MinContact` and its Hold is a `PoseKey.Hold` step; Arc: `Arc.Sample` at `launchAt` and just before `LandAt`.

---

## B1. Contact puffs and target shakes (#357)

*Every stroke that hits something makes a puff where it hits, and the thing it hit moves.* Build first: B3–B10 reuse it.

**Common to every sub-sheet.** Source: A1's `WorkHit` (fires when `DollPose.hit` goes 0→1; set today at
`DollMotion.cs:199, 231, 246, 545`, `PeopleView.cs:524`, read by nothing). `HitFx` picks the puff kind from the style
and the place, spawns it at the tool tip (shoulder + arm direction × (0.075 + tool length)), and calls the target's
answer. Cheats: **"Puffs on/off"**, **"Shake all targets once"**. Tests: P1, P2, P8 for every style that sets `hit`;
a pool test (25 hits in one frame → 24 puffs, oldest dropped, no allocation).

### B1.a Chips (the axe, the woodcutter; B10)
| | |
|---|---|
| Actor · target | woodcutter with `Axe` → a real tree trunk (B10); today the fixed `Woods` cell |
| Contact | `Chop` u = 0.74 (`DollMotion.cs:230`); spawn at the axe head, 0.06 u above the ground |
| Answer | `Chips` 6 bits away from the cut face; tree `Sway` 4° (crown only) |
| Hold | the axe stays at the contact angle 0.1 s (`Chop`'s low 40° held from u 0.74 to 0.80) |
| 3-s read | "chopping: chips fly off the tree" |

### B1.b Dust (the hoe, a dig, a set-down, a stamp)
| | |
|---|---|
| Actor · target | farmer `Hoe` → field soil; digger (B5); a load set down at the stash; `Stomp` on dry ground |
| Contact | `Hoe` u = 0.82 (`DollMotion.cs:246`); dig and set-down at their own beats |
| Answer | `Dust` 3–4 blobs at the blade; on a set-down, the pile `Squash` |
| 3-s read | "hoeing: dust at every stroke" |

### B1.c Sparks (the knapper; the fire poke, B9)
| | |
|---|---|
| Actor · target | toolmaker `SitStrike` (Knap) → the stone in their lap |
| Contact | `SitStrike` u > 0.92 (`DollMotion.cs:199`): make it one window 0.92–0.98 so it fires once |
| Answer | `Sparks` (knap gravity) + flash star; the stone `Jolt` (0.006 u: it's small, the star carries it) |
| 3-s read | "someone knaps a stone at the fire: sparks" |

### B1.d Droplets and Ring (water; B3, B4, B8)
| | |
|---|---|
| Contact | the bucket meets the water (B4), a splash (B3), the yank (B8), a stamp in a puddle (`Stomp`, `DollMotion.cs:400`: hit at the bottom of each bounce, \|sin\| < 0.15, at most one per 0.3 s) |
| Answer | `Droplets` + `Ring` on the water surface; `Mud` colours on muddy water and dry-season puddles |
| 3-s read | "splash" |

### B1.e Grass (the spear in the ground; a rabbit's bolt; a kid's tumble)
| | |
|---|---|
| Contact | the spear's `Stick` landing (B6); the first bound of a fleeing rabbit; a kid landing in a tumble (B2) |
| Answer | `Grass` 4 blades, season green |

### B1.f Shakes on targets
| Target | Answer | Notes |
|---|---|---|
| Bush | `Sway` 11°, 6 Hz, τ 0.18 s + 1–2 `Leaves` (none in winter) + its berry dots move with it | bushes are baked into the land's chunk meshes (`View/BoxView.Eco.cs:69-124`): see "Hard in code" |
| Tree | crown `Sway` 4°, 4 Hz, τ 0.3 s + 1–2 `Leaves` | same |
| Tent frame | `Jolt` (#284 hammer `hit`) + `Dust` at the post foot | `Life/LifePlanner.Tents.cs` |
| Stone, stash pile | `Squash` 0.88 → 1 | `CampStash` |
| **Shake all targets once** | every bush, tree and frame in view sways once, staggered 0–0.4 s | cheat |

---

## B2. Kids' play 1 (#358)

The play group (§0.7) replaces the one `Hop` circle (`DollMotion.cs:381-393`, mapped for `Toddle/Play/Dance/Snowball`
at `DollMotion.cs:59`). The planner hands a block of kid `Play` to a group of 2–5 kids at the same place; the group
picks a game the neighbouring group isn't playing. A 1.25–2 h afternoon block (`Day.cs:604-610`) is 20–32 s: a game
plays in rounds inside it. Kids' runs: `Run` with bounce 0.03 u (today 0.022), lean 14°, arm swing 50°.

### B2.a Tag
*One kid chases, the others scatter; a touch swaps who's "it".*
| | |
|---|---|
| Actors · roles | 3–5 kids: 1 `Chaser`, the rest `Runner`s, on open green round the gathering place (radius 0.6 u) |
| Verb → target | the chaser runs at **the nearest runner** |
| Wind-up | the new chaser stands, spins once (yaw 360° in 0.4 s, `Smooth`), bounces twice on the spot (0.03 u, 0.25 s each) |
| Act | chaser runs 0.5 u/s; runners run 0.45 u/s away, zigzag ±25° every 0.5 s; a runner not chased stops and taunts (left arm z −155 ± 20 at 6 Hz, hop 0.02) |
| Contact | chaser within 0.09 u: its right arm shoots forward (x 95, lean 20, 0.1 s `Snap`) → `hit` |
| Answer | the touched runner jolts (Flinch-like: back 0.04 u, squash 0.9, arms up x 70) and squeals (a chatter "!" mark) |
| Hold | both freeze 0.25 s (it's the tag: they look at each other) |
| Follow | the old chaser runs off, arms up (z ∓150), laughing bob 0.015 u at 9 Hz |
| End beat | after 3–5 tags, or the block's end: all drop onto the grass and sit (`Sit`) panting 1.5 s (offset y 0.008 at 3 Hz) |
| Timing | a round 4–7 s; 4–6 rounds in a 1.25–2 h block |
| Prop · arc | none (a kid with a `Stick` keeps it, held) |
| De-sync | roles differ, so nobody is in step; runner speeds × 0.9–1.1 by hash |
| 3-s read | "kids playing tag: one chases, the others scatter" |
| Cheat | **"Kids play: tag"** (gathers 4 kids at the gathering place) |
| Tests | P1 (touch within 0.09 u), P6, P8, a test that the role swaps on each contact |
| Replaces | `Hop` for `Play`; reuses `Run`, the `Flinch` and `Laugh` keys (`DollMotion.cs:484-523`) |

### B2.b Ball
*Two or three kids toss a big ball between them; now and then it bounces away and someone runs after it.*
| | |
|---|---|
| Actors · roles | 2–3 kids, `Thrower` / `Catcher` (/`Fetcher`), a triangle with 0.35–0.5 u sides |
| Verb → target | throw → **the catcher's arms** |
| Wind-up | peg dolls have no legs, so no kick: a two-handed underhand toss. Squash 0.85, lean −8°, both arms back (x −30), 0.3 s `Out` |
| Act | arms swing x −30 → 110 in 0.15 s (`Snap`), hop 0.03 u, lean +12° |
| Contact | release at arms x 80 → `hit` (no puff); the ball becomes an arc object |
| Arc | `Ball` from the hands to the catcher's chest (0.07 u up), h 0.25 u, T 0.75 s for 0.45 u |
| Answer | the catcher lifts its arms (x 120) when the ball is at its apex (anticipation), catches: squash 0.85, step back 0.03 u |
| Hold | 0.2 s ball hugged (`Arms` slot), then it becomes the thrower |
| Miss (1 in 4) | the ball lands 0.15–0.3 u past the catcher: `Bounce` (h 0.09, 0.03) with a small `Dust`, then `Roll` 0.25 u to a stop in 0.8 s; the nearest kid runs, bends (`Lift` bend 0.4 s), throws from there |
| End beat | the block ends: the last catcher holds the ball up over its head (x 170, both arms) 0.8 s |
| Timing | one throw 2.2–3.0 s (by hash), ~10 throws in a block |
| Prop | **the ball is drawn** (today `Prop.Ball` falls to `PropKind.None`, `PeopleView.cs:805`): `PropKind.Ball`, slot `Arms` while held |
| De-sync | throw gaps by hash; two ball groups ≥ 1 u apart |
| 3-s read | "kids throw a ball to each other" |
| Cheat | **"Kids play: ball"** |
| Tests | Arc (hand to catcher's arms), P1, P5, P8; a held ball never shows in two places |
| Replaces | `Hop`; reuses `Lift` (`DollMotion.cs:316`) for the pick-up |

### B2.c Chase the rabbits
*Kids creep up on the rabbits, charge, the rabbits scatter, the kids give up and laugh.*
| | |
|---|---|
| Actors · roles | 2–4 kids, `Charger`s |
| Verb → target | the **real herd**: rabbits on green within 1.2 u of the kids' spot, not a rabbit a hunt has picked (`RabbitView.HuntOn`) |
| Wind-up | sneak: crouch (squash 0.72, lean 18) and creep 0.05 u steps for 1.5 s, one kid points (`Point`, 0.6 s) |
| Act | charge: `Run` 0.5 u/s at the rabbits, arms up waving (z ∓150 ± 20 at 6 Hz) |
| Contact | the first kid within `AlertRadius` 0.55 u of a rabbit → `hit`; `RabbitHerd.Scare` on every rabbit within 0.55 u of any kid |
| Answer | `Bolt`: ears up 0.45 s, then they flee at 1.6 u/s, 3× the kids; `Grass` at each rabbit's first bound |
| Follow | the kids run on 1.5–2 s, slow down (`Out`, 0.6 s), stop; hands on knees (lean 30, arms x 30) panting 1 s |
| End beat | they laugh (`Laugh`, `DollMotion.cs:513`, 1.2 s), pointing where the rabbits went; one may tumble (see the tumble row): lie 0.8 s, `Grass`, up again |
| Tumble (user 2026-11-06 "sometimes"; as built) | about 1 chase in 4 (`BeatHash`, never the sim's stream) one kid, never the one whose charge scares the herd, trips 0.7 s into its charge: stumble (lean 34, arms out x 95, 0.22 s), dive onto its front (lean 80, arms x 165, 0.16 s), the Contact = a `Grass` puff under the chest (a `PlayFx`, not a `hit`), a small Back bounce, lies 0.8 s, up onto its knees (squash 0.8) and up (0.7 s), brushes its tunic 0.8 s; it slides on 0.08 u and stays there. The others face it while they pant and laugh (`Laugh` points), it laughs last where it fell, then all sit. Cheat **"Kids chase rabbits: a kid tumbles"**; clip `358-chase-rabbits-tumble` (`RabbitChaseGame.Tumble`) |
| Timing | 8–10 s. At most once per view hour per herd; never within 0.5 h of a planned hunt near it |
| De-sync | charge starts 0–0.4 s apart; the creep is in no step |
| 3-s read | "kids run at the rabbits; the rabbits scatter" |
| Cheat | **"Kids play: chase rabbits"** (needs rabbits; "Spawn rabbits here" exists) |
| Tests | P3 (every rabbit within 0.55 u bolts), P2, P5 |

### B2.d Toddle
*The baby wobbles after its carer and plops down now and then.*
| | |
|---|---|
| Actors · roles | `Toddler` (baby), `Carer` (`BabyDay`, `Day.cs:571-576`) |
| Verb → target | follows the carer, 0.15 u behind |
| Motion | carer strolls 0.12 u/s; toddler roll ±10° at 2.5 Hz, bounce 0.01 u, arms out (z ∓70) for balance |
| Beat | every 6–10 s a plop: squash 0.75, down 0.03 u, `Back`, 0.8 s sitting; the carer turns, bends (lean 30) and the baby gets up |
| 3-s read | "a little one toddles after a grown-up" |
| Cheat | (part of "Kids play: ...": "Toddle") |
| Replaces | `Hop` for `Toddle` |
| **In the game (user 2026-11-06 "toddling is fine"; as built)** | a baby of 1 to 4 on its carer's carry (#192) toddles instead of riding when the carer's hands are free and they stay about the camp (a walk ≤ 1.2 u); long walks, busy hands, storm and snow: carried; ≤ 3 toddlers a day in the box (#192's cost). The toddler is a kid doll × 0.8: 0.15 u behind and 0.05 u aside of the walking carer, pottering on a 0.17 u arc at their front-left or front-right while they stand or sit (≤ 0.36 u/s); a plop every 6–10 s (no hit), a carer standing within 0.35 u turns and bends. `View/Beats/ToddleFollow.cs`, `Life/LifePlanner.Toddle.cs`, PeopleView; cheat **"Toddlers walk: all"** (adds a two-year-old if none); clip `358-toddle` (the cheat's play-group toddle: `358-toddle-game`) |

### B2.e Dance ring
*Everyone holds hands in a ring that turns as one, steps in and out.*
| | |
|---|---|
| Actors · roles | `Dancer`s on a ring of r = 0.3 + 0.03 × max(0, n − 6) u; optional `Centre` (the couple, C3) |
| Motion | arms out to the side (z ∓95) so hands meet; the ring turns 25°/s; a hop 0.025 u on each 0.5 s beat, **in step** (exempt from P6) |
| Beats | every 4 beats the ring reverses; every 8 beats all step in to 0.6 r with arms up (z ∓160), a cheer bounce 0.035, and out again (1 s) |
| Centre | the couple turns round each other (orbit r 0.06 u, 90°/s), hands joined (right arms x 70) |
| End beat | the ring stops, everyone claps (`Cheer`, 1 s) |
| 3-s read | "people dance in a ring" |
| Cheat | **"Dance ring now"** |
| Replaces | `Hop` for `Dance` (weddings `Scenes.cs:94`, festival `Scenes.cs:186-188`) |

*Stretch (research 17 §5 #11, not in #358):* `Snowball` keeps `Hop` until it gets the same treatment: a throw like the
ball's, `Snowball` arc h 0.12, `Snow` puff on the target kid, who flinches and throws back.

---

## B3. Kids' play 2 (#359)

### B3.a Splash (pond and puddles)
*Two kids splash each other at the water's edge, and one jumps in.*
| | |
|---|---|
| Actors · roles | 2–4 kids, `Splasher`s in pairs facing each other 0.25 u apart, along the bank or in a puddle |
| Verb → target | splash → **the partner**, from **the real water** (pond bank cell, or `PlaceKind.Puddle`) |
| Stance | at the pond: standing 0.03 u into the water (sunk 0.03, not `Swim`'s 0.1); in a puddle: on it |
| Wind-up | crouch (squash 0.85), both arms down and back (x −30), 0.25 s |
| Act | arms sweep forward and up to x 120 in 0.12 s (`Snap`) |
| Contact | at arms x 60 → `hit` |
| Answer | `Droplets` 8, **aimed** at the partner (h 0.12, 0.35 s) + `Ring` at the hands; the partner flinches 0.3 s later (back 0.04, arms up x 70), laughs (0.6 s) and splashes back |
| Big beat | every ~10 s one kid jumps in: squash 0.8 (0.2 s), hop 0.08 u (0.5 s), lands sunk 0.06 with 2 `Ring`s and 10 `Droplets`; frogs and ducks within `FrogFlee` / `DuckFlee` of the splash `Flee` |
| End beat | they wade out and shake off: roll ±12° at 8 Hz for 0.6 s, 4 `Droplets` |
| Timing | an exchange 1.6–2.2 s |
| 3-s read | "kids splash each other at the water" |
| Cheat | **"Kids play: splash"** (at the pond, else the nearest puddle; "Puddles now" exists) |
| Tests | Arc-like: the aimed droplets land within 0.05 u of the partner; P2; P3 (frogs, ducks) |
| Replaces | `Swim` for kids' `Swim`/`Splash` (`DollMotion.cs:410-416`); `Stomp` gets B1.d droplets |

### B3.b Woods games (once there is a wood)
*Two kids chase each other round a real tree trunk, peeking round it; or two duel with sticks.*
| | |
|---|---|
| Actors · roles | 2 kids, `Circler`s (chaser and runner) round **a real grown tree** (one trunk, not the `Woods` cell) |
| Round the tree | both circle the trunk at r 0.12 u, 0.35 u/s; the runner suddenly reverses (0.15 s stop, `Snap` turn); both peek round it: lean sideways (roll 20°) 0.6 s. A catch is a tag (B2.a contact) |
| Stick duel | 2 kids with `Stick`s 0.18 u apart: raise (x 130, 0.25 s), strike (x 70, 0.1 s `Snap`) → `hit` when the sticks cross: a white flash star (`Sparks` star only) and both jolt back 0.02 u; turns alternate, 1.2 s each; ends with both sticks raised high (x 170) |
| Autumn | kicks through fallen leaves: every few steps 3 `Leaves` thrown up |
| 3-s read | "kids play round a tree" / "kids fence with sticks" |
| Cheat | **"Kids play: woods"** |
| Tests | P1 (within 0.11 u of the trunk), P6 |

### B3.c Hide and seek
*One kid counts at a tent with its eyes covered, the others crouch behind bushes and tents; found, they race back.*
| | |
|---|---|
| Actors · roles | 3–5 kids: 1 `Seeker`, the rest `Hider`s |
| Count | the seeker faces **a real tent** (or tree or stash pile), lean 20, both arms up to the face (x 150, z ∓40), bobs 6 times in 4 s (0.01 u) |
| Hide | hiders run to **real cover** within 1 u (bush, tent, trunk, pile), to the far side from the seeker (cover centre + away × (cover r + 0.06)); crouch (squash 0.7, lean 10); every 2–3 s one peeks: sideways 0.04 u + roll 15° for 0.5 s |
| Seek | the seeker turns (0.4 s), walks with `Look` (yaw ±35°, `DollMotion.cs:453`), visits covers nearest first |
| Contact | seeker within 0.15 u of a hider → `Point` at it (x 90) + hop 0.04 u → `hit` |
| Answer | the hider pops up (squash 1.15 stretch, hop 0.05 u, arms up) |
| Follow | both race back to the tent; the first there touches it (arm x 90) |
| End beat | everyone found: all at the tent, cheer once (`Cheer` 0.6 s); the first found counts next |
| Timing | a round 15–20 s |
| 3-s read | "one kid counts at the tent; the others hide behind bushes" |
| Cheat | **"Kids play: hide and seek"** |
| Tests | P1 (hiders within 0.1 u of their cover, on the far side), P5 |

---

## B4. Drawing water (#360)

*Someone kneels at the real water's edge, dips the bucket under, lifts it full and carries it home leaning under the weight.*

| | |
|---|---|
| Actors | an adult with `EmptyBucket` (30 % of adults at dawn, `Day.cs:714-721`; the cook) |
| Verb → target | dip → **the water's edge**: the shore cell next to water nearest the path; the doll 0.06 u from the edge, facing the water |
| Approach | arrive, settle 0.3 s |
| Wind-up | kneel: squash 0.7, down 0.04, lean 30, 0.4 s `Smooth`; the bucket arm forward (x 45) |
| Act | dip: lean 30 → 42, arm x 45 → 60, 0.35 s; the water is 0.08 u below the land, so the bucket also hangs down 0.06 u (a cartoon reach; see "Hard in code") |
| Contact | the bucket's rim meets the water surface → `hit` |
| Answer | `Droplets` 5 + `Ring` (two rings); muddy: `Mud` + a brown ring + one brown `Dust` |
| Hold | 0.6 s under, a wobble on the bucket (±4°, 5 Hz): it's filling |
| Lift | anticipation: squash 0.65 (0.15 s); then lean 42 → −10 in 0.4 s (`Back`), arm to x 20; the prop swaps `EmptyBucket` → `Bucket` / `BucketMuddy` at lift start (`Fill`), 3 drips at 0.2 s intervals |
| Follow | the weight lands: squash 0.92, down 0.01 u, 0.25 s |
| Walk home | a heavy walk: speed × 0.7, body roll 7° away from the bucket, bucket arm straight down (x 0, z 6), bounce 0.006, stride × 0.8 |
| End beat | at home the bucket is set down: `Lift` bend 0.5 s, bucket `Squash`, 2 drops slop over; then it goes in |
| Timing | at the edge: 0.3 + 0.4 + 0.35 + 0.6 + 0.55 + 0.25 ≈ **2.5 s**, plus 0.5 s spare. Today the block is 0.2 h = **2 s at dawn** (10 s per view hour): make it **0.35 h (3.5 s)** (`Day.cs:720`) |
| Prop | `BucketEmpty` → `Bucket` / `BucketMuddy` (`PeopleView.cs:792-793`) |
| De-sync | 2–3 drawers at one bank stand ≥ 0.2 u apart; start delay 0.4–0.9 s by hash; never two dips within 0.3 s |
| 3-s read | "someone kneels at the pond, dips a bucket and carries it home heavy" |
| Cheat | **"Water run now"** (2–3 people with buckets) |
| Tests | P1 (bucket within 0.05 u of the water surface on `hit`), P2, P5 (the set-down), P8 |
| Replaces | the `Stand` fallback for `FetchWater` (`DollMotion.cs:112`); reuses `Lift` (`DollMotion.cs:316`), `Walk` with a new `heavy` flag |

---

## B5. Berries (#361, folds into #288 and #310)

### B5.a Picking
*The picker reaches, the bush shakes, a berry hops into the basket; when it's full, they tip it onto the pile.*
| | |
|---|---|
| Actors | a gatherer with `Basket` (left hand), at **a real bush** (`PlaceKind.BushCell`, 0.17 u off its centre, facing it: `PeopleView.cs:626-632`) |
| Wind-up | arm pulls back to x 40, 0.15 s; then reaches up and in: arm x 40 → 110, lean 32 → 38, 0.35 s `Smooth` |
| Contact | hand at a berry dot (arm x 110) → `hit` |
| Answer | bush `Sway` 11° + 1–2 `Leaves`; the dot leaves the bush |
| Act | the arm snaps back to x 70 (0.12 s `Snap`); a `Berry` arcs from the hand into the basket (h 0.06, T 0.3 s) |
| Hold | basket `Pop`; 0.2 s still |
| Basket | shows 0 → 5 berry dots on top as picks add up (a dot per pick, up to 5) |
| End beat | after 6 picks or at the block's end: stand, hold the basket up a moment (x 120, 0.6 s), carry it to the stash; there tip it: lean 35, both arms x 80, basket rolled 100°, 4–6 `Berry` arcs onto the pile 0.06 s apart (h 0.05, 0.25 s), pile `Squash` |
| Timing | a pick every 2.5 s (today `Pick` waves every 0.95 s, `DollMotion.cs:251-261`); 6 picks ≈ 15 s |
| De-sync | pickers at one bush start 0.8 s apart by slot; never two shakes of one bush within 0.3 s (the second just adds a leaf) |
| 3-s read | "picking berries: the bush shakes, the berry goes in the basket" |
| Cheat | **"Pickers to the nearest bush now"** ("First bush now" exists) |
| Tests | P1 (hand within 0.05 u of a dot), P2 (dot gone and bush sways within 0.2 s), Arc (hand → basket), P5 |
| Replaces | `Pick` for `Gather` at a bush; `Harvest`/`Weed` keep `Pick` |

**As built, the tip at the stash (#361 follow-up, `TipLoad` / `DollStyle.TipBasket`, clip `361-stash-tip`):** a short block at the pile right after the carry (planner: its 1.8 s come out of the work before the carry, the day's other blocks keep their times). The beat runs from the first frame the doll stands still: lift 0.25 s (arms 95), tilt 0.25 s (lean 35, arms 80, basket rolled 100° by `BerryPickView`, it rides on the chest), shake out 0.25 s, then 3 to 5 berries leave the rim 0.06 s apart from 0.5 s (h 0.05, 0.25 s) onto the pile; the first lands at 0.75 s = the pose's `hit`: pile `Squash` 0.88 → 1 (`StashProps.Bump`) + a small `Dust` puff; the basket shows empty; settle to 1.4 s. The spec's "both arms x 80, 4–6 arcs" became 3–5 (the basket holds 5). Roots are tipped the same way. Cheat: "Tip at the stash now".

### B5.b Digging roots (before green; the stream bed)
*Kneeling in the dry hollow, the digger paws at the ground in puffs of dust and pulls a root up high.*
| | |
|---|---|
| Actors · target | everyone at the tribe stage (`Work.cs:129-130`), at the stream bed: a spot chosen per digger along it, not one point |
| Wind-up | kneel: squash 0.7, lean 35 |
| Act | both arms paw in turn (x 40 ↔ 90 at 3 Hz) for 1.5 s; `Dust` on every other down-stroke (`hit`) |
| Contact | the pull: lean 35 → −15 in 0.3 s (`Back`) → `hit`, a big `Dust` (4 blobs, r × 1.5) |
| Hold | the `Root` held up high (arm x 150) 0.5 s |
| Follow | `Root` arcs into the basket (h 0.05, 0.25 s), basket `Pop` |
| Timing | a root every 4 s |
| 3-s read | "digging up roots" (answers #310's "are they gathering food? By the pond?") |
| Tests | P1, P2, Arc |

### B5.c Kids tasting at a bush
*A kid stands at the bush, plucks a berry, eats it and gives a happy hop.*
| | |
|---|---|
| Actors · target | a kid at a bush (`KidBerryPlace`, `Work.cs:70`): `Eat` at a `BushCell` draws `Taste`, standing, not `Sit` |
| Beat | reach to the bush (arm x 100, 0.4 s) → `hit`: bush `Sway` 5°, dot gone; hand to mouth (x 135, 0.5 s); chew bob 0.01 u at 8 Hz, 0.6 s |
| End beat | every 3rd berry, a happy hop 0.03 u with arms out (z ∓90), 0.4 s |
| Timing | 2.2 s a berry (today `Taste` is 1.4 s, `DollMotion.cs:599-607`) |
| 3-s read | "a kid eats berries off the bush" |

---

## B6. The hunt (#362, folds into #283)

*The hunter creeps up on a rabbit, freezes when it looks up, throws; the spear flies and sticks; the rabbits scatter;
on a catch the hunter holds it up and walks home proud.*

| | |
|---|---|
| Actors · roles | `Hunter` (`Spear`); a `Kid` who follows on 30 % of hunts (`Hunt.cs:71`); the **target rabbit** and every rabbit near it; 2–3 people at the fire as `Watcher`s at the end |
| Verb → target | throw → **the real rabbit** at `targetX/Z` |
| Stalk | from 0.75 u out (`StalkRange` 0.5 → 0.75, `Hunt.cs:73`) to the **throw point 0.4 u** from the rabbit (today the lunge point is 0.125 u, `Hunt.cs:237`). Creep steps: crouch (squash 0.72, lean 18), spear low and back (arm x 40); a step of 0.05 u in 0.5 s, then still 0.7 s; 7 steps |
| Freeze | 1–2 times per stalk the rabbit sits up (planned, so view and planner agree): the hunter freezes mid-step until it grazes again (0.8–1.5 s) |
| Wind-up | rise from the crouch (squash 0.72 → 1, 0.4 s); the spear arm up and back (x 165), lean −12, the left arm points at the rabbit (x 90): **hold the aim 0.35 s** |
| Act | throw: arm x 165 → 60 in 0.12 s (`Snap`), lean −12 → 25, a step forward 0.04 u |
| Contact 1 (release) | arm passes x 110 → `hit`; the spear leaves the hand (prop hidden) and becomes an arc object |
| Arc | `Spear` from the hand (~0.2 u up) to the rabbit (catch) or 0.12–0.2 u past or beside it (miss); h 0.15 u; **T 0.45 s** (slower than real, so the eye can follow) |
| Contact 2 (landing) | `Stick`: the spear stands in the ground at 30–50°, quivers (`Wobble` 6°, 10 Hz, τ 0.25 s), `Grass` + `Dust`. `HitStop` variant here |
| Answer: catch | the rabbit jumps 0.05 u and falls on its side (roll 90°, 0.25 s); **every other rabbit within 1 u bolts** (ears up 0.45 s, flee) |
| Answer: miss | the target freezes 0.15 s, then zigzags off (zigzag 0.06 u, up from 0.035, `RabbitHerd.cs:459`); the others within 1 u bolt too |
| Follow: miss | the hunter's shrug (arms out z ∓70, lean −6, today's `Bolt` pose, `PeopleView.cs:527-531`) + one stamp; walks to the spear, bends and pulls it out (lean 30 → −10, `Back`, 0.4 s, `Dust`), walks back |
| Follow: catch | walks to the rabbit, bends (0.4 s) and picks it up |
| End beat: catch | **holds it up** (`HoldUp`, arm x 160, bob, `DollMotion.cs:609-615`) 1.5 s; the kid cheers (`Cheer`), runs over; a slow walk home (speed × 0.8, a proud bounce 0.012, rabbit on the shoulder) with the kid beside (B7 walk together); at the fire the `Watcher`s `Turn` |
| Timing | Stalk 0.5 h (8 s, today 0.3); Strike 0.2 h (3.2 s, today 0.06 h ≈ 1 s); Bolt 0.15 h (2.4 s); HoldUp 0.12 h (~2 s): `Hunt.cs:73` |
| Prop · arc | `Spear` prop → `Spear` arc → a stuck spear (a pooled object until picked up); `Rabbit` on the shoulder (`PeopleView.cs:799`) |
| De-sync | never two hunts within 1 u at once (the planner already staggers them) |
| 3-s read | "a hunter throws; the rabbits scatter" |
| Cheat | **"Slow-mo the next hunt"** (view time × 0.5 from Stalk to HoldUp), plus "Start a hunt now", "Next hunt succeeds / misses" (exist) |
| Tests | Arc (hand → rabbit ±0.03 on a catch), P2 (rabbit answers within 0.2 s of landing), P3 (all rabbits within 1 u bolt), P5, P8 (aim hold ≥ 0.3 s) |
| Replaces | the thrust in `HuntPose` (`PeopleView.cs:511-533`); `Crouch`'s creep (`DollMotion.cs:211-216`) for the stalk |
| **As built (#362, AnimBench-tuned)** | Timings in real seconds, one table for plan and view (`Life/HuntTiming.cs`): rise 0.40, aim hold 0.35, snap 0.12, **release 0.80, flight 0.45, lands 1.25 = Contact** (the hit the grass puff and the rabbit answer), hold 0.35, settle 0.34, Strike 2.0 s. The stalk is 7 × (step 0.5 s + still 0.7 s) + 1–2 freezes of 0.8–1.5 s = 8.4–11.4 s (`HuntStalk`; step dips squash 0.72 → 0.65, leans 20 → 29, rocks ±5° side to side, arms swing; the rabbit sits up in the freeze windows, the plan's, not the view's). The sim settles at the landing; a hit rabbit stays where it fell until the hunter lifts it. Neighbours within 1 u bolt at the **release** (`Field.Knock`: nearest at once, the rest a moment later), the target at the **landing** (`Scare.Hold`, 0.45 s notice, not 0.15: the knock's constant), `ZigZag` 0.06. Hunter: miss = shrug 1.8 s, walk to the spear (stands 0.1 u short), pull 0.8 s (spear back in hand at 0.36 s, dust), walk home; catch = walk to the rabbit, lift 0.8 s, hold up 1.8 s (rabbit in the hand), home at 0.8× with the kid beside (`KidRun`, `KidEscort`). Not built: the fire's `Watcher`s turning; `HitStop` (flag, off); the B7 walk-together beat (the kid just walks a parallel line). |

---

## B7. Talk and pairs (#363)

### B7.a Talk: nod, open hand, shared laugh
*The talker gestures with an open hand; the listener nods; a shared laugh closes the chat.*
| | |
|---|---|
| Actors · roles | `Talker` / `Listener`, swapping each loop step (today's turn-taking, `DollMotion.cs:336-341`, `PeopleView.cs:429`) |
| Talker | bob 0.012 u (today 0.006); every 2nd beat an **open hand** to the listener: right arm (x 70, z 35), roll 4° toward them, 0.5 s |
| Listener | **nods** 0.15 s after each talker peak: lean 4 → 14 → 4 in 0.35 s, twice; squash 0.97 on each |
| End beat | the last 1.2 s: both laugh (bob 0.015 at 9 Hz, roll ±5°, lean −8, arms to the belly x 40, z ∓30) + a laugh mark; then they turn away |
| Timing | a talker beat 1.3 s (today's period) |
| 3-s read | "two people chatting: one talks with a hand, the other nods" |
| Cheat | "Force chat" (exists) |
| Tests | the listener's nod follows the talker's peak by 0.1–0.25 s; P8 |
| Replaces | `Talk` (`DollMotion.cs:329-345`); `Argue`/`Haggle` keep theirs |

### B7.b Hand over
*One walks up, holds a thing out, the other takes it and looks at it.*
| | |
|---|---|
| Actors · roles | `Giver` / `Taker`. Uses: the toolmaker's spear to the hunter (in the hunt's `TakeSpear`, `Hunt.cs:293`); berries; a bowl at the meal (B9) |
| Approach | the giver walks to 0.14 u of the taker; both face |
| Wind-up | the giver holds it out: arms x 80, lean 10, 0.4 s |
| Act | the taker reaches 0.2 s later (arms x 80) |
| Contact | hands meet → `hit` (giver); the prop moves from giver to taker, `Pop` |
| Hold | 0.3 s both holding |
| Follow | the taker lifts it to look (arms x 100 → 60, `Back`); the giver's arms drop, one nod |
| Timing | ≈ 1.4 s after the approach |
| 3-s read | "one hands something to the other" |
| Cheat | **"Hand over now"** |
| Tests | the prop is on exactly one doll at every frame; P1 (hands within 0.05 u) |

### B7.c Walk together
*Two walk side by side, hand in hand, turning to each other now and then.*
| | |
|---|---|
| Actors · roles | `Lead` / `Side`, 0.1 u apart |
| Motion | one speed (the slower × 0.9), **strides in step** (shared stride phase; a pair is exempt from P6); the inner arms out (z ∓40) so the hands meet; heads turn to each other in turn (yaw ±15° every 2 s) |
| Uses | couples, the hunter and the kid home (B6), the wedding walk (C3) |
| 3-s read | "two walk together, hand in hand" |

### B7.d Hug (flag `Hugs`, on: the user, 2026-11-06 "Hugs are fine. Add them.")
*Arms wide, a step in, a squeeze with a little sway, a step back.*
| | |
|---|---|
| Actors · roles | `HugA` / `HugB`: a return from far, a reconcile, a wedding |
| Wind-up | both open their arms wide (z ∓100), 0.3 s, and step in to 0.09 u centre to centre |
| Contact | arms wrap (x 85, z inward ±10) → `hit`; both squash 0.92, lean 10 into each other; the smaller is lifted 0.02 u |
| Hold | 1.2 s, swaying roll ±6° at 1 Hz |
| Follow | step back 0.05 u, arms drop, a tiny hop 0.02 |
| Timing | 2.4 s |
| 3-s read | "two people hug" |
| Cheat | **"Hug now"** |
| **Where (as built, `View/HugDirector.cs`, `Life/HugRules.cs`)** | the wedding's join after the vow (C3, the moment's own); a **reunion**: the hunter home with a catch (CarryBack → Skin at the fire) and their partner, or a kid of theirs, within 1.4 u; a **reconcile**: two rivals whose bicker the leader stepped into, as it ends; a **parent and a kid** at dawn (6:36–8:00) or on the evening green (18:12–19:30), about one day in three for each. Rare on purpose: a pair once a view day, 2 / 3 / 4 / 6 a day in the box (tribe / camp / village / town), never two within 25 s; only people standing free (not working, walking off, in a game or a moment). A grown-up hugging a kid lifts it 0.02 u. Cheats: "Hug now", "Hugs: on/off", **"Hug: parent and kid now"**; clips `363-hug` (reunion), `363-hug-parent-kid` |

---

## B8. Fishing (#364)

*The rod bobs; a nibble; the fisher yanks, a fish flips high out of the water into the basket, and a frog hops away.*

| | |
|---|---|
| Actors · target | a fisher with `Rod` at **a real bank cell** facing the water (today the fixed `Pond` spot, `Work.cs:149-152`); the line's end is 0.25 u out on the water |
| Loop | today's `Fish` (`DollMotion.cs:274-284`): rod out (arm x 118), slow bob, a tug every 4.5 s |
| Wind-up (bite) | every 2–4 tugs (by hash): a nibble: the rod dips twice (x 118 → 100, 0.15 s each) with a tiny `Ring` at the line's end; lean forward 10, 0.2 s |
| Act | the yank: arm x 100 → 165 in 0.15 s (`Snap`), lean −15 |
| Contact | at the top of the yank → `hit`: `Droplets` 8 + 2 `Ring`s at the line's end |
| Arc | `Fish` from the line's end to the basket on the ground 0.08 u left of the fisher: h 0.3 u (a high flip), T 0.6 s, two flips |
| Answer | basket `Pop`, the fish's tail sticks out of it; a frog or duck within 0.5 u of the splash `Flee`s |
| Follow | arms up (z ∓150) and one hop 0.03, 0.5 s |
| Miss (1 in 3 bites) | the fish only clears the water (h 0.05) and drops back: one `Ring`; the fisher slumps (lean 20, arms limp, 0.6 s) |
| End beat | the session ends: the catch on a stick held up (`FishStick`, camp) and carried to the stash |
| Timing | a bite every 9–18 s; the bite beat ≈ 2 s |
| 3-s read | "someone fishing pulls a fish out" |
| Cheat | **"Fish bite now"** |
| Tests | Arc (line end → basket), P2, P3 (frog/duck), P5 |

---

## B9. Meals and the fire (#365)

### B9.a Eating
*Sitting in the ring, people lift food to their mouths and chew; at the end they lean back full.*
| | |
|---|---|
| Actors | everyone at a meal (`Eat` → `Sit` today, `DollMotion.cs:54`) |
| Beat | a bowl in the left hand (arm x 60); the right hand to the mouth (x 40 → 135, 0.35 s), chew bob 0.008 u at 8 Hz 0.6 s, back (0.35 s); every 3–5 s by hash |
| End beat | the meal's last 1 s: lean back −10 with arms out (`Stretch` short), a contented sway |
| 3-s read | "people eating in a ring" |

### B9.b Passing bowls
*One person goes round the ring from the pot, handing each seated person a bowl.*
| | |
|---|---|
| Actors · roles | one `Giver` (the cook or a kid), the seated as `Taker`s |
| Beat | a B7.b hand over at each person, clockwise from the pot, 1.5 s per person (walk 0.2 u + hand over); the taker starts eating once it has a bowl |
| Prop | a new `Bowl` prop (0.04 u; none exists, `PeopleView.cs:762-813`) |

### B9.c Poking the fire
| | |
|---|---|
| Actors · target | someone with a `Stick` → **the fire** |
| Wind-up | stick raised (x 120), 0.3 s |
| Contact | thrust down-forward (x 60, lean 25, 0.15 s `Snap`) → `hit` |
| Answer | `Sparks` (fire, floating up) + fire `Flare` |
| Hold · follow | 0.2 s; pull back 0.4 s |
| Timing | every 5–8 s, one poker per fire |
| 3-s read | "someone pokes the fire; sparks fly up" |

### B9.d The cook stirs
| | |
|---|---|
| Actors · target | the cook with a stick → the pot (`Stir`, `DollMotion.cs:286-295`) |
| Answer | `Steam` 2 blobs at the top of each stir (1.6 s) |
| Beat | every 4th stir, a taste: `Taste` 1.4 s, then one nod |
| 3-s read | "the cook stirs a steaming pot" |

Cheat: **"Meal now"**. Tests: P2 (fire flare within 0.2 s of the poke), P5 (the meal's end), P6 (eaters' hand-to-mouth out of step).

---

## B10. Felling (#366; may wait for the felling decision, keepers-hands §8.3)

*The woodcutter chops a real tree: chips fly and the crown shakes; now and then the tree creaks, tips slowly and falls,
and is cut into logs that are carried off.*

| | |
|---|---|
| Actors · target | a woodcutter with `Axe` at **a real grown tree** 0.14 u from the trunk (today the fixed `Woods` cell) |
| Chop | today's `Chop` (`DollMotion.cs:218-235`, period 1.15 s); contact u 0.74 → B1.a: `Chips` + crown `Sway` 4° |
| Hold | the axe "bites": arm still 0.1 s |
| Pause | every 4–6 chops, a breather: arm to the brow (x 150) 0.6 s |
| The fall (rare) | the tree creaks: 3 hard shakes (8°); the cutter steps back 0.15 u, `Point`s and a "!" mark; the tree tips about its base 0 → 88° in 1.6 s (`In`: slow start, fast end) |
| Landing | big `Dust` (8 blobs, r × 2) + 6 `Leaves`, a bounce 3° (`Back`, 0.3 s); everyone within 0.8 u `Turn`s. `HitStop` variant here |
| End beat | the trunk is cut into 2–3 logs (3 chops each, `Chips`), and 1–2 people carry them off (`Log`); a stump stays |
| Timing | a fall at most once per view day per wood [est] |
| 3-s read | "chopping a tree" / "a tree falls" |
| Cheat | **"Fell a tree now"** |
| Tests | P1 (axe within 0.11 u of the trunk), P2, P5 |

---

## B11. The background-life director (#367)

*There is always one story on screen to watch; a small camp is calm, a town is busy.*

**What counts as "a story in progress"**: an actor, a verb, a real target and an answer, mid-beat, inside the camera's
view (inset 10 %), in daylight. Each counts once for its length:

| Counts | Doesn't count |
|---|---|
| a hunt from Stalk to HoldUp · a play-group game · a water draw (kneel to lift) · picking with picks · digging roots · a fishing bite · felling · tent raising · a hand-over, a hug, a walk together · a meal's bowl round · a staged moment (C3) | sitting, standing, a lone walk, sleep, a chat without a B7 beat, a loop with no target |

**The rule**: every **3 s** the director counts stories in view. If fewer than the floor for the population, it starts a
cheap one near the camera's focus, in this order, first that's possible: kids chase the rabbits (kids and rabbits in
view, no hunt near) · a kids' ball game · a water run (dawn and morning, water in view) · a hand-over between two
adults close by · a fisher's bite now · a pick (a gatherer to a bush in view).

**Never**: interrupt a hunt or a staged moment; the same kind twice in a row; a kind again within 30 s; take a
worker off their block for more than 0.5 view h (it borrows kids at play and idle adults first); start anything at night.

| Population (research 16 §4.2) | Floor of stories in view | A new one starts about every | Feel |
|---|---|---|---|
| Tribe (≤ 6) | 0; start one if none for 20 s | 20 s | calm |
| Camp (6–10) | 1 | 15 s | |
| Village (10–22) | 1–2 | 10 s | |
| Town (22+) | 2–3 | 6 s | busy |

Linear between rows (as the moment director). Cheat: **"Story density: off / normal / high"** (high = 2× the rate,
for the tester). Tests (SimTests, pure): with a fixed view rect and population, the seconds with no visible story stay
under 20 / 15 / 10 / 6 per minute; no kind repeats back to back. The user signs off the feel.

---

## C3. Staged moments (#370)

Played from C2's queue with the beats above. Each has a cheat.

### C3.a The wedding, organised because the Keeper showed up
*A kid spots the Keeper and runs to tell; everyone gathers; the couple walks in hand in hand through a cheering
aisle; a ring dances round them; they wave to the glass.*
| Beat | What | Time |
|---|---|---|
| 1 Spotted | a kid looks up at the glass (`LookUp`), points, runs to the fire with a "!" mark | 3 s |
| 2 Gather | people stop work and walk to the gathering place; some carry logs for the fire | 8–12 s |
| 3 Aisle | two rows face each other 0.3 u apart; the couple walks in (B7.c) | 4 s |
| 4 Cheer wave | each person `Cheer`s as the couple comes within 0.2 u, so a wave runs down the aisle | (with 3) |
| 5 Vow | at the fire the couple face each other; the chief or an elder `Bless`es (`DollMotion.cs:427`) | 2 s |
| 6 Join | both hands joined (arms x 75) and a hop together; **or the hug (B7.d) if `Hugs` is on** | 2 s |
| 7 Dance | the ring (B2.e) round the couple; kids run in and out | 12–16 s |
| 8 End beat | the couple face the glass and `Wave`; everyone waves to the Keeper | 2 s |

3-s read: "everyone dances in a ring round two people". Cheat: **"Wedding now (the Keeper arrived)"**. Replaces the
procession/dance split at `Scenes.cs:91-100`.

### C3.b The newborn held up to the glass
*A parent carries the baby to the glass and lifts it high; the family cheers behind; the kids press their hands to the glass.*
| Beat | What | Time |
|---|---|---|
| 1 Walk | the parent walks to the glass with the baby in the arms; the family behind | 4 s |
| 2 Wind-up | baby lowered to the chest, squash 0.9 | 0.4 s |
| 3 Lift | both arms up (x 165), the bundle high, a slow sway ±4°; the family `Cheer`s; kids `PressHands` (`DollMotion.cs:467`) at the glass beside | 2.5 s |
| 4 End beat | cradle (arms x 60) and `Bow` | 1 s |

3-s read: "a parent holds a baby up to the glass". The bundle must be ≥ 0.06 u to read. Cheat: **"Newborn to the glass now"**.
Replaces the `Visit` at `Scenes.cs:101-110`.

### C3.c The new chief
*The new chief walks through the ring to the seat; everyone bows in a wave; the gold mark pops on.*
| Beat | What | Time |
|---|---|---|
| 1 Gather | a ring at the fire | 6 s |
| 2 Hand over | the old chief (if alive) hands the headband over (B7.b); else the elder `Bless`es | 1.5 s |
| 3 Seat | the new chief sits, then stands and gives a `Speech` | 3 s |
| 4 Bow wave | everyone `Bow`s, nearest first, 0.12 s apart by distance | 2 s |
| 5 End beat | the gold accent `Pop`s on (scale 1.3 → 1) with gold `Sparks`; `Cheer` | 1 s |

3-s read: "everyone bows to one doll at the fire". Cheat: **"New chief now"**. Extends `RoleMoment` (`Scenes.cs:130-133`).

### C3.d The festival
*Everyone brings something, the fire flares, the chief speaks, the whole camp dances, eats, and jumps together at the end.*
| Beat | What | Time |
|---|---|---|
| 1 Set-up | baskets and logs carried to the green and set down (`Squash`) | 10 s |
| 2 Flare | the fire `Flare`s big (scale 1.6), lots of `Sparks` | 1 s |
| 3 Speech | the leader's `Speech`; everyone listens (`Scenes.cs:111-120`) | 3 s |
| 4 Dance | a big ring (B2.e); kids play ball and tag round it (B2) | 20 s |
| 5 Meal | bowls passed (B9.b), eating (B9.a) | 15 s |
| 6 End beat | everyone jumps at once (`Cheer` hop, in step, once) | 1 s |

3-s read: "the whole camp dances round a big fire". Cheat: **"Festival now"**.

---

## Placed life (P9, #416)

Beat sheets for every scene ticket of the **P9 Placed life** milestone. Design:
[placed-life.md](placed-life.md) (the jar, the ladder, the numbers); research
[18](../research/18-placed-life-and-ecosystems.md) §3, §5 and [19](../research/19-wolves-and-the-first-dogs.md) §5–§7.
Every number is [est] until AnimBench and an AnimLab clip. File refs are `AquariumCiv/Assets/Game/...` on `dev` at
008d66a. The default camera frames the camp (dolls ~55 px on a 2556-px phone, decisions.md 2026-11-06), so 0.01 u is
about 2.5 px there; the 0.025 u floor of §0.1 still holds, and every beat below is written to read at that camera with
the sound off.

**Rules every sheet keeps (user, 2026-11-07 and 2026-11-08):** no blood, ever (a caught animal lies still; a wolf kill
happens behind a bush) · **a wolf never touches a person** and **is never killed** · wolves never come in among the
tents (the closest they get: the bone spot, the rack at the camp's edge, the eyes at the firelight's edge) · one berry
thicket, never built on · no fishing before fish are placed · a placement is a ghost first, then a second tap · the
jar waits for the Keeper.

**Two distances used below:** the **tent ring** is the circle round the fire that holds every tent, plus 0.25 u; the
**firelight's edge** is 1.0 u from the fire at night. Wolf paths are planned outside the tent ring; the planner rejects
any wolf point inside it (tested in B21–B23 and B26).

### P0. The shared quadruped (#425)

Deer, wolves and dogs are **one procedural four-legged doll** (research 19 §11), built like `RabbitDoll`
(`View/RabbitDoll.cs`): low-poly boxes and icos from the palette, one mesh per look, pooled, posed by a small
`QuadPose` (body pitch and bob, head yaw and pitch, leg phase, tail angle, ear angle). No skinning.

| Part | Deer | Wolf | Dog (generation g) | Pup |
|---|---|---|---|---|
| Shoulder height (u) | 0.15 (stag 0.16) | 0.10 | 0.10 → 0.09 | 0.05 |
| Body length (u) | 0.20 | 0.16 | 0.16 → 0.14 | 0.08 |
| Leg length / body | 0.75 | 0.6 | 0.6 → 0.5 | 0.45 |
| Head | long, slim | long muzzle | muzzle × (1 → 0.8) | round, big (× 1.4) |
| Ears | wide, upright | upright, pointed | droop 0 → 1 | upright, big |
| Tail | short white flag | long, hanging straight | curl 0 → 1 (up over the back) | stub |
| Coat | fawn, pale belly; **white stag**: off-white, a gold glint in the eye | grey with a darker saddle; **the grey wolf** (B22) a pale silver face; **white wolf**: white | patch 0 → 1 (piebald blotches) | grey fluff |
| Extra | antlers (stags; size by age), white rump | eyes catch the firelight at night (an unlit emissive dot pair, 0.012 u) | hide collar (from stage 2) | — |

**Drift:** each dog generation adds 0.25 to `earDroop`, `patch` and `tailCurl` (capped at 1) and takes 0.05 off the
muzzle, with ±0.1 from a hash of the dog, so litter-mates differ. Generation 4 looks like a dog (research 19 §7). The
first pup is generation 0: a wolf.

| Gait or pose | Motion | Used by |
|---|---|---|
| `Walk` | 4-beat leg swing, 1.6 steps/s, body bob 0.008 u | all |
| `Trot` | diagonal pairs, 2.6 steps/s, bob 0.015 u; wolves trot in a line, 0.12 u apart | wolves, dogs |
| `Graze` | head down to the ground (0.6 s `Smooth`), small head nods, a step every 3–5 s | deer |
| `Alert` | head up and turned to the cause in 0.25 s (`Snap`), ears forward, tail flag up (deer), still 0.6–1.5 s | all: **the answer to a person, a wolf or a knock** |
| `Bolt` | deer: bounds for 3 s at 1.4 u/s, white rump flashing, leaps of 0.04 u; wolves: a flat run at 1.6 u/s | all |
| `LieDown` | front legs fold, then back, 0.6 s; head on the paws | all; a hunted deer lies on its side (roll 90°, 0.3 s) |
| `Drink` | front legs splay a little, head down to the water line (−0.08 u, see Hard in code item 7), lapping nod at 2 Hz; lifts between sips | deer, wolves |
| `SitHowl` | sit (0.4 s), muzzle up to 70°, hold 2–3 s with a slow chest swell; others join 0.4–1.0 s apart. A bark is a 0.15 s head jab, twice | wolves, dogs |
| `PlayBow` | front low (body pitch −20°), rump up, tail wag at 3 Hz, then a hop sideways 0.04 u | pups, dogs, kids' play |
| `SleepCurl` | lying, body curved into a ring, tail over the nose; a breath bob 0.004 u every 3 s | dogs by the fire, pups at the den |
| `Sniff` | head down 0.3 s, three quick nods, head up | arrivals, the dog tracking |

3-s read targets: a deer reads as **long legs plus antlers or a white rump**; a wolf as **grey, low, tail down**; a
dog as **floppy ears and a curled tail**. Tests: AnimBench holds each gait 3 s and the tester names it at the default
camera (P4). The rabbit keeps its own doll.

---

## B12. Arrivals: placing life, and the waiting hint (#424)

### B12.0 The ghost and the drop (every species)
*You pick the species and tap a glowing spot; faint outlines of the group show there; tap again and they arrive.*
| | |
|---|---|
| Ghost | first tap on valid ground (the glow, #423): the founding group drawn as **flat pale outlines** on the ground (a palette tint at 60 % value, no alpha; `Pop` in 0.15 s), breathing scale 1 → 1.04 over 1.2 s. Another tap on the glow moves the ghost there (a 0.2 s `Smooth` slide). A tap off the glow leaves the ghost where it is and gives a tiny `Dust` "no" at that spot (no sound, no text) |
| Confirm | a tap on the ghost (or the "Place" chip) → **Contact**: the ghost `Pop`s out and the species' arrival beat (B12.a–f) starts on the same frame |
| Camera | `BoxCamera.NudgeToward` the spot, as the first rabbit's sign does (`View/RabbitView.cs:251`) |
| People (the first-mist rule) | within 0.5 s everyone awake within 2.5 u `Turn`s to the spot (`PeopleView.LookAt`, `View/PeopleView.cs:1221`); the nearest grown-up `Point`s for 1.5 s; one kid runs toward it, stops at 0.4 u and `Cheer`s; 2–3 others walk to 0.6 u and stand watching for 4 s. At night the people's reaction waits for dawn (the arrival itself doesn't) |
| Glass voice | one line per species (placed-life.md has the lines), after the arrival's end beat |
| End beat | the arrival's own (below); then the people drift back to work over 3–5 s, not all at once (§0.8) |
| Timing | ghost to confirm: the player's; arrival 2–6 s by species; reaction 6–8 s |
| 3-s read | "something new came into the box, and the people run to see" |
| Cheat | **"Place X here"** (skips the ghost), **"Ready every jar"** |
| Tests | P2 (the arrival starts ≤ 0.2 s after the confirm), P3 (every person within 2.5 u turns); the ghost never shows off the glow |
| Builds on | the first rabbit's sign (`View/RabbitView.cs:226-252`: `Planner.React(KeeperGesture.Sighting)`, `LookAt`, the camera nudge), which this replaces for rabbits |

### B12.a The berry thicket
| | |
|---|---|
| Arrival | three bushes **push up from the soil** at the spot, 0.15 u apart, 0.3 s after each other: `Dust` at the base, the bush scales 0 → 1.15 → 1 (`Back`, 0.5 s), a `Leaves` puff (3) on top, one `Sway` of 6° |
| End beat | the berries show: dots `Pop` on a few at a time over 1 s |
| Then | the thicket spreads only inside its own area (~20–25 bushes, placed-life.md): a new bush at its edge pushes up the same way, at most one a season. Nobody builds on the area |
| 3-s read | "bushes grew where the Keeper tapped" |

### B12.b The rabbits
| | |
|---|---|
| Arrival | a pair drops lightly from above the rim: each falls 0.4 u in 0.5 s (`In`), lands with a `Bounce` (35 %) and a `Grass` puff, then **shakes off** (body roll ±12°, 8 Hz, τ 0.15 s, ears flapping), sits up and looks round (`Alert`), and hops 2–3 hops apart (`RabbitHerd.HopLen` 0.12 u, `Sim/Eco/RabbitHerd.cs:30`) |
| End beat | both graze; the rabbit tracks (#282) show from now on |
| 3-s read | "two rabbits dropped in" |
| Replaces | rabbits arriving by themselves (`Sim/Eco/EcoModel.cs:587-589`) and the tracks-first sign |

### B12.c The deer
| | |
|---|---|
| Arrival | the stag steps out of the trees nearest the spot (or the box's back edge if no tree is within 1 u), **walks** to the spot, stops, `Alert` 1.5 s with the ears turning; the two does follow 1.0 s and 1.6 s behind |
| End beat | the stag lowers its head to graze; one doe keeps watching the camp (`Alert` held 2 s), then grazes |
| Timing | 5–6 s from the trees to grazing |
| 3-s read | "deer walked out of the wood" |

### B12.d The ducks
| | |
|---|---|
| Arrival | a pair glides down from above the rim onto the water: a shallow glide from 0.5 u up (1.2 s, `Out`), feet forward, then **lands with a skid** of 0.15 u along the water: two `Ring`s and `Droplets` (6) |
| End beat | both shake their tails, then paddle a slow circle; one dabbles (tail up, as `View/PondLife.cs:17` does today) |
| 3-s read | "two ducks landed on the pond" |
| Replaces | `PondLife`'s ducks shown by pond size (`DuckPondCells` 20 / 40, `View/PondLife.cs:26`); the view now draws the sim's stock (#421) |

### B12.e The fish
| | |
|---|---|
| Arrival | **the bag**: a clear, slightly blue bag (a squashed icosphere 0.08 u with a tied top) drops onto the water at the spot (`Bounce`, `Ring`) and **floats** 3 s, bobbing (`Wobble` 0.01 u, 1.5 Hz), 6 small dark fish shapes circling inside. Then the knot opens (a 0.2 s `Pop`), the bag tips and **sinks out of sight** in 0.6 s |
| End beat | 6 fish shadows fan out under the water from the spot over 1.5 s (B16.a), with 2 `Ring`s |
| Note | the aquarium way of adding fish (research 18 §3.1); the bag never stays on the water |
| 3-s read | "a bag floated on the pond and fish came out" |

### B12.f The wolves
| | |
|---|---|
| Arrival | at the den spot (the far wood, placed-life.md) **two grey shapes trot out of the trees** 0.3 u, stop, `Sniff` the air (muzzle up, two nods), look toward the camp 1.5 s (`Alert`), then lie down at the den mouth |
| People | the B12.0 reaction, **quieter**: they turn and stand still; nobody runs toward it; a grown-up puts a hand on a kid's shoulder (arm x 70, 2 s) |
| End beat | **the first howl that night**: at the first dusk after placing, B21.a plays by itself (it doesn't wait for the Keeper: a howl carries) |
| 3-s read | "two wolves came out of the far wood" |

### B12.w The waiting hint (a jar is ready and you haven't placed it)
*The people show they feel something missing, a little more the longer the jar waits.*
| | |
|---|---|
| Rule | at most one hint scene per visit, in daylight, started by the B11 director as a cheap story (it counts as one). Its stage grows with time ready: stage 1 in the first game year, stage 2 after, stage 3 after 2 years |
| Thicket | stage 1: a gatherer with an empty `Basket` stands at the meadow and looks round (head yaw ±40°, 2 s); stage 2: two gatherers, one shrugs; stage 3: a kid pulls at the grass and pretends to eat it |
| Rabbits | stage 1: a hunter crouches at the meadow's edge, spear low, looking (`Crouch`, 4 s), then stands and shrugs; stage 2: two hunters, one points at empty grass; stage 3: kids play "rabbit" (one hops, the others chase: `ChaseGroup` with a kid as the runner) |
| Deer | a hunter stands at the wood's edge looking in (head sweep, 3 s); stage 3: a kid holds two sticks up as antlers |
| Ducks · fish | a kid sits at the empty bank, drops a pebble in (`Ring`) and watches it; stage 2: a grown-up kneels and looks into the water; stage 3: someone stands in the shallows with a spear, waiting, then walks out |
| Wolves | none: nobody misses wolves. The kit's dot is enough |
| 3-s read | "they're looking for something that isn't there" |
| Cheat | **"Hint now"** (stage 1–3) |
| Tests | never two hints in one visit; none at night; none for wolves |

---

## B13. The deer hunt, the carry, the feast and the antlers (#426)

**Decided in this sheet [est]:** the deer hunt uses **bows** (research 18 §5.3: "bows on their backs", and deer make
"the bow a craft"); the rabbit hunt keeps the thrown spear (B6, the user's call). The first deer hunt shows the first
bows.

### B13.a The hunt
*Two or three hunters leave in a line, creep to the wood's edge, freeze when the stag looks up, shoot; on a hit the
herd bolts and the stag lies down; on a miss the arrow thunks into a tree.*
| | |
|---|---|
| Actors · roles | 2–3 `Hunter`s with a `Bow` (a new prop: a 0.16 u bark-coloured arc with a string line); the **target deer** (the stag on the first hunt, later a buck or a young male; no does in spring once the restraint beat has played); the herd |
| Verb → target | shoot → **the real deer** |
| Approach | they leave the camp in a line, 0.15 u apart, bows on their backs; 1.2 u from the herd they crouch (squash 0.72) and the lead **points** (arm x 90, 1 s); the others nod |
| Stalk | as B6 (`Life/HuntTiming.cs:46-47`: 7 steps of 0.5 s plus 0.7 s still), side by side 0.2 u apart, from 1.0 u to the **shot point 0.6 u** from the deer |
| Freeze | 1–2 times the stag lifts its head (`Alert`) and **everyone** freezes mid-step until it grazes again (0.8–1.5 s); planned, so view and planner agree (as B6) |
| Wind-up | the shooter rises to one knee; the bow comes up (left arm x 90 holding it), the right arm draws back (x 80, z −40); **hold the aim 0.5 s** |
| Act · Contact 1 | release: the string arm snaps forward in 0.1 s → `hit`; the arrow leaves |
| Arc | `Arrow` (new arc object: 0.12 u, thin; a feathered end once the ducks give feathers, B15.b) from the bow to the deer (hit) or 0.15–0.25 u past it into a trunk (miss); h 0.06 u; **T 0.4 s** |
| Answer: hit | the deer jumps 0.03 u, stumbles three steps, **lies down** on its side (`LieDown`, 0.3 s) and is still. No blood; no arrow shows in it (the arrow vanishes at contact). **The rest of the herd `Bolt`s** into the wood (3 s, white rumps) |
| Answer: miss | the `Arrow` lands `Stick` in a trunk (`Wobble` 6°, 10 Hz, τ 0.25 s) with 2 `Chips`; the whole herd `Bolt`s; the hunters stand and **shrug** together (B6's shrug, `HuntTiming.BoltSeconds` 1.8 s) and walk over to pull the arrow out |
| Follow: hit | the hunters walk to the deer; one kneels and lays a hand on its neck (arm x 60, 1 s: a thank-you); one runs back to the camp for the pole (B13.b) |
| Timing | approach 6–10 s; stalk 8–12 s; the shot 1.5 s; the carry starts ~6 s after a hit |
| Rules | after the restraint beat, no deer hunt in spring; never at the water (B14.a); never below the deer's floor (#422) |
| De-sync | one deer hunt at a time; never while the pack hunts (B20) within 2 u |
| 3-s read | "hunters with bows creep up on deer" |
| Cheat | **"Deer hunt now"**, "Next hunt hits / misses" |
| Tests | Arc (bow → deer ±0.03 on a hit), P2 (the deer answers ≤ 0.2 s), P3 (every deer within 1.5 u bolts), P8 (aim hold ≥ 0.3 s), no hunt below the floor |
| Builds on | B6 (`Life/HuntTiming.cs`, `View/Beats/HuntBeats.cs`, `View/HuntStage.cs`); new props `Bow` and `Pole`; new arc `Arrow` |

### B13.b The pole carry
*The stag hangs from a pole between two hunters, legs up, and they walk it home.*
| | |
|---|---|
| Beat | the fetcher comes back with a `Pole` (0.35 u); two hunters kneel and tie (both arms x 60, a 1.5 s fiddle), then stand together (`Back`, 0.5 s): the deer hangs under the pole, legs tied up, swaying ±6° at 1 Hz |
| Walk | home at 0.7× speed, the two in step on purpose (they carry one thing), bob 0.015 u; the third hunter walks ahead carrying the bows |
| End beat | they set it down by the fire (`Squash` on the deer, `Dust`); everyone in the camp `Turn`s and the kids run up |
| 3-s read | "two hunters carry a deer on a pole" |
| Tests | P5 (the set-down happens); the pole's ends stay in the hands (within 0.02 u) for the whole walk |

### B13.c The feast
*That night the fire is bigger and there is dancing.*
| | |
|---|---|
| Beat | at dusk the fire `Flare`s and stays at scale 1.3 for the evening; a spit over it (two forked sticks and a bar, a roasting shape turning slowly); B9.b's bowl round (`View/Beats/MealBeats.cs`) with meat; then a dance ring (B2.e, `RingGroup`) for 12–16 s |
| End beat | the shooter stands in the middle of the ring (`Centre`) and the others `Cheer` |
| Rule | a held moment (C2's queue): only the **first** deer hunt gets the staged feast; later hunts get the meal only |
| 3-s read | "a big fire and a dance after the hunt" |
| Cheat | **"Deer feast now"** |

### B13.d The antlers over the chief's tent
| | |
|---|---|
| Beat | the next morning the chief and the shooter carry the antlers (a prop, 0.12 u wide) to the chief's tent; the shooter steps up on a log (+0.05 u), raises them (arms x 170) and sets them over the door (`Jolt` on the tent) |
| End beat | everyone near `Cheer`s; the antlers stay for the life of the camp (a consequence prop like `ConsequenceKind.Feather`, `View/ConsequenceProps.cs:26`) and move to the chief's hut in the village |
| 3-s read | "antlers go up over a tent" |

### B13.e The shed antler (spring)
| | |
|---|---|
| Beat | once each spring after the first deer: a kid in the wood bends (lean 35, 0.4 s), lifts an antler (`HoldUp`, arm x 160, 1.5 s) and runs back to the camp holding it high; a grown-up takes it and turns it in the hands (1 s) |
| End beat | the kid `Cheer`s; the antler goes onto the stash pile (`Squash`) |
| 3-s read | "a kid found an antler and runs home with it" |

---

## B14. Dawn at the pond and the white stag (#427)

### B14.a Dawn at the pond
*At first light the does come to drink; whoever is awake stops and watches; a kid holds very still.*
| | |
|---|---|
| When | dawn (view hours 5–7), about every 3rd day with deer and a pond; never while a hunt runs |
| Deer | 2–3 does walk from the wood to the nearest bank and `Drink` 6–10 s, lifting their heads between sips; one `Alert`s at a person for 1.5 s, then drinks again |
| People | everyone awake within 2 u stops (their block pauses for up to 0.5 view h, as B11 borrows) and `Turn`s; a kid within 1 u freezes in a crouch (squash 0.8, arms in) and **holds still** until the deer leave; a grown-up holds a finger up (arm x 140, 1 s: "shh") |
| Rule | **nobody hunts at the water** (a people's rule, placed-life.md): the hunt planner never targets a deer within 0.5 u of the pond |
| End beat | the does walk back into the wood; the kid stands and looks at the grown-up, who nods |
| Timing | 20–30 s |
| 3-s read | "deer drinking at the pond while the people watch" |
| Cheat | **"Dawn at the pond now"** |
| Tests | no hunt plan within 0.5 u of water; P3 (a doe alerts when a person comes within 0.8 u) |

### B14.b The white stag (a staged choice)
*The hunters saw the white stag. Hunt it, or let it be?*
| | |
|---|---|
| When | once per box, a game year or more after the first deer hunt; a big choice (visit-loop.md §2.4: raised only while you watch, never resolves unseen, "Let them decide" is credited to you) |
| Staging | at the wood's edge the white stag steps out and stands (`Alert`, 3 s), a soft gold glint in the eye; two hunters walk toward the camp pointing back, the chief stands, the camp turns to the wood. Then the choice card. The two sides stand as `Life/ChoiceScenes.cs` places them: hunters with bows on one side, a kid with the elder on the other |
| Hunt it | B13.a with the white stag; on a hit the staged feast (B13.c) plays again, and its antlers (bigger, white-tipped) replace the first ones over the chief's door |
| Let it be | the stag walks slowly back into the wood; the hunters lower their bows; from then on it shows at B14.a now and then (1 dawn in 5, at the back, never a target) and a kid points every time |
| Let them decide | the chief's trait picks (as research 19 §6.1) |
| 3-s read | "a white deer at the wood's edge; everyone stops" |
| Cheat | **"White stag now"** |
| Tests | never raised while away; the "let it be" stag is never a hunt target |

---

## B15. Ducks: eggs, feathers, ducklings, feeding (#428)

### B15.a Eggs and the mother duck's charge
*A gatherer wades in, comes up with an egg, and the mother duck chases them out, flapping.*
| | |
|---|---|
| When | spring, at most once a day, with a nest in the reeds (#421) |
| Approach | a gatherer with a `Basket` wades in knee-deep (the doll sinks 0.04 u; a `Ring` every step) to the nest |
| Act · Contact | bends (lean 35, 0.4 s) and lifts an `Egg` (a new prop, 0.025 u, white) → `hit`: the mother duck on the nest **stands up** (0.2 s), wings out |
| Answer | **the charge**: the duck rushes at the gatherer flapping (wing beat 6 Hz, a 0.03 u hop every 0.25 s, neck stretched out) for 1.5 s |
| Follow | the gatherer runs out of the water with the egg **held high** (arm x 165), knees up (bob 0.03 u), the duck flapping behind for 0.6 u; at the bank the duck stops, shakes its tail and swims back |
| End beat | on the bank the gatherer looks at the egg and laughs (`Laugh`); kids watching `Cheer` |
| Rule | eggs are taken only from a nest of 3 or more, so the ducks never drop below their floor (#422) |
| 3-s read | "a duck chases someone out of the reeds" |
| Cheat | **"Egg run now"** |
| Tests | P1 (the hand at the nest), P2 (the duck answers ≤ 0.2 s); the duck never touches the person (stays ≥ 0.05 u behind) |

### B15.b Feathers to arrows
| | |
|---|---|
| Beat | late summer, after the moult: the fletcher bends on the bank and picks up a feather (a small white `Pop` into the hand), holds it up to the light 1 s, puts it in the basket; 3–4 feathers in 6 s |
| Then | from the next day the hunters' arrows and quivers show white tips: **a visible upgrade** |
| 3-s read | "someone picking up feathers on the bank" |

### B15.c Ducklings in a line
| | |
|---|---|
| When | spring, after hatching (#421) |
| Ducks | the mother swims or waddles with 3–5 ducklings in a line behind her (0.025 u each, yellow-brown, 0.04 u apart, each following the one before with a 0.25 s lag) |
| Kid | when the line is on land within 1.5 u of a kid, the kid walks behind them **copying the waddle** (body roll ±10° at 2 Hz, arms out like wings, z ∓60), 0.15 u behind the last duckling; another kid laughs |
| End beat | the ducks reach the water and slide in; the kid stops at the edge and flaps the arms twice |
| 3-s read | "a kid waddling behind a line of ducklings" |
| Cheat | **"Ducklings now"** |

### B15.d Feeding the ducks
| | |
|---|---|
| Beat | 2–3 kids at the bank throw crumbs (berry-sized bits, h 0.08, T 0.35 s, onto the water: `Ring`s); the ducks **come fast** (paddling at 2×); when they climb out, one kid runs away from them (B2's runner), the others laugh |
| End beat | the ducks peck up the last crumbs; the kids walk off |
| 3-s read | "kids throwing food to the ducks" |

---

## B16. Fish: shadows, spearing, the rack, the line (#429)

### B16.a Fish you can see
| | |
|---|---|
| Look | dark fish shapes under the water: flat ellipses 0.05 × 0.015 u, 0.02 u below the surface (a darker tint of the water), up to 8 shown (placed-life.md: about one per 3 pond cells) |
| Motion | they drift in a loose school and turn together now and then (all within 0.3 s); dart 0.2 u when startled |
| Answer | `Flee` from a person in the water or a `Ring` within 0.3 u: they dart off and regroup 2 s later |
| Winter | fewer shown, slower |
| 3-s read | "there are fish in the pond" |

### B16.b Spearing (camp)
*Knee-deep, spear raised, very still; the strike and a splash; a wriggling fish held up, or a wet face.*
| | |
|---|---|
| Actors | a fisher with a `Spear`, knee-deep (sinks 0.04 u) |
| Wait | spear raised (arm x 150), head down (pitch −30), **still** 3–6 s while the shadows drift close |
| Act · Contact | the strike: arm x 150 → 40 in 0.12 s (`Snap`), lean 30 → `hit`: `Droplets` 8 and 2 `Ring`s; the fish near it dart |
| Hit (1 in 3) | the spear comes up with a fish on it, wriggling (`Wobble` 25°, 6 Hz, τ 0.5 s), held up high (`HoldUp`) for 1.2 s |
| Miss | the spear comes up empty; a `Droplets` puff aimed at the fisher's face; they shake the head (yaw ±20°, 4 Hz, 0.5 s) |
| End beat | the catch goes on the fish stick (`FishStick`, as B8's end), then to the rack (B16.c) |
| Timing | a strike every 8–14 s |
| 3-s read | "someone spearing fish in the shallows" |
| Cheat | **"Spear a fish now"** |
| Tests | P2, P3 (fish within 0.3 u flee), P8 |

### B16.c The smoking rack
| | |
|---|---|
| Look | two posts and a bar with fish hanging (reuse `ApartProps.FishRack`, `View/ApartProps.cs:69`) over a small smoky fire; grey `Steam` puffs drift up every 0.8 s |
| Beat | a grown-up hangs the day's fish one at a time (arm x 150, 0.6 s each; the fish `Pop`s onto the bar), then fans the smoke with a hand (arm z ∓80 ↔ ∓40 at 2 Hz, 2 s) |
| Where | at the camp's edge, outside the tent ring (so the meat thief in B22 can reach it without coming among the tents) |
| 3-s read | "fish hanging over smoke" |

### B16.d Kids splashing scatter the fish
| | |
|---|---|
| Beat | B3.a's splash (`View/Beats/KidPlay2.cs`); every `Droplets` or `Ring` within 0.3 u of a fish shadow sends it darting (B16.a); a kid points at the shadows going |
| 3-s read | "kids splashing, fish darting away" |

### B16.e The line waits for the fish
| | |
|---|---|
| Rule | **no fishing of any kind before fish are placed** (user, 2026-11-08). The planner's fishing (`Life/LifePlanner.Fish.cs`) and B8 (`View/Beats/FishBeats.cs`, `View/FishingView.cs`) run only once there is a fish stock; before that the bank is for water (B4), splashing (B3) and the ducks |
| After | B8 as built, but a bite needs a fish shadow within 0.3 u of the line's end: the shadow darts to the hook in 0.4 s just before the nibble, so the bite has a cause you can see |
| Tests | no fishing activity in a box with open water and no placed fish |

---

## B17. Small scenes for berries and rabbits (#430)

### B17.a The berry kid
*A kid picks with the gatherer, eats every second berry, gets a purple mouth; the grown-up takes the basket off them.*
| | |
|---|---|
| Beat | a kid beside a gatherer (B5.a) picks too (B5.c's reach); every 2nd berry goes in the mouth (arm x 135, chew bob 0.01 u at 8 Hz); after 3 eaten the kid has a **purple mouth** (a small dark-purple disc on the head, 0.015 u, until the next meal) |
| End beat | the gatherer looks at the kid and takes the kid's basket (B7.b hand-over); the kid shrugs, grinning |
| 3-s read | "a kid eating the berries they should be picking" |

### B17.b The songbird
| | |
|---|---|
| Beat | a songbird (0.02 u) lands on the bush being picked; the gatherer **shoos** it (arm z ∓90, two flaps, 0.5 s); it flies up 0.15 u, circles once and lands back on the bush; the gatherer's arms drop (a slump, 0.5 s) |
| Note | songbirds still come by themselves (research 18 §3.4) |
| 3-s read | "someone shooing a bird off a bush" |

### B17.c The drying mat
| | |
|---|---|
| Beat | late summer: a hide mat by a tent; a gatherer tips the basket onto it (B5.a's tip), kneels and spreads the berries with both hands (arms x 50 ↔ 70, side to side, 2 s); the berries are a purple patch that darkens over the days |
| End beat | in autumn the dried berries go into a `Sack` carried to the stash: the winter food |
| 3-s read | "berries spread out to dry on a mat" |

### B17.d Rabbit snares
| | |
|---|---|
| Set | evening: a hunter kneels at a rabbit run at the meadow's edge and sets a loop (both hands at the ground, a 1.5 s fiddle); a small stake and loop stay (a prop, 0.03 u) |
| Check | next morning the hunter kneels and **lifts the loop**: mostly empty (2 in 3: a shrug, the loop reset), sometimes a rabbit (`HoldUp` 1.2 s, home on the shoulder as in B6) |
| Rule | a snare catch counts toward the rabbits' floor (`SimCore.HuntFloor` 2, `Sim/SimCore.Hunt.cs:15`) |
| 3-s read | "a hunter checks a snare" |

---

## B18. Village: the duck pen and the fish pond (#431)

At most two pens (decisions.md 2026-11-07: tame only what's needed); a small village may build one.

### B18.a The duck pen
| | |
|---|---|
| Build | 2–3 villagers drive stakes in a ring 0.4 u across at the pond's edge (`Hammer` strikes, `Chips`) and weave branches between them (both arms x 60 ↔ 80, 2 s per span); a gate of two stakes |
| Life | evening: a kid with a `Stick` walks the ducks into the pen (the ducks waddle ahead in a line, the kid behind with the stick out); morning: the gate opens and they waddle to the water |
| Eggs | from now on from the pen: a villager reaches over the fence; no charge, the tame duck only quacks (two head jabs) |
| 3-s read | "ducks herded into a little fence" |

### B18.b The fish pond
| | |
|---|---|
| Build | a small pool dug beside the pond with the stick (`Dust`), joined by a short cut that is then closed with stones (`Jolt`); a villager moves 3–4 fish in with a basket (wade, scoop, carry, tip in: `Ring`s) |
| Life | a villager feeds it (B15.d's throw) and scoops one out now and then with a basket (a fish `Pop`s up, `Droplets`) |
| 3-s read | "someone scooping a fish from a small pool" |

---

## B19. The weir and the basket traps: the morning lift (#432)

*Every morning two villagers lift the basket at the weir: fish flapping inside, or nothing.*
| | |
|---|---|
| Build (once) | where the canal leaves the pond: a fence of stakes across the water (driven in, `Ring`s) with woven baskets set in the gap (research 18 §5.5) |
| The lift | each morning (view hour 7) two villagers wade knee-deep to the basket, bend (lean 35) and **lift it together** (both arms x 60 → 120, 0.8 s, `Back`); water pours out (`Droplets` 10, aimed down) |
| Answer | 2 in 3: 1–3 fish flap inside (each `Wobble` 30°, 8 Hz; one flips out in an arc and back in); 1 in 3: empty, and the two look at each other and shrug |
| End beat | they carry it to the bank between them and tip it into a basket (`Squash`); the trap goes back in (`Ring`) |
| Timing | 25 s |
| 3-s read | "two people lift a fish trap out of the water" |
| Cheat | **"Weir lift now"** |
| Tests | P5; fish only with a fish stock (B16.e); catches respect the fish floor (#422) |

---

## B20. The pack hunt, the ravens and the kill (#440)

*The wolves trot in a line at the wood's edge and test the herd with a rush; mostly the deer get away; sometimes an
old doe goes down behind a bush, and the ravens come.*
| | |
|---|---|
| When | a few times a game year, more in winter (research 19 §5); by day, so it can be seen |
| Approach | 2–3 wolves `Trot` in a line, 0.12 u apart, along the wood's edge toward the herd |
| Antic | at 1.2 u the deer lift their heads (`Alert`); the wolves stop, heads low, 1 s |
| Act | **the test**: the wolves rush (`Bolt`) 0.6 u; the herd `Bolt`s |
| Mostly (3 in 4) | the deer get away into the trees; the wolves slow, stop and **pant** (head bob 4 Hz, a small red tongue 0.01 u, 2 s), then trot back |
| Sometimes (1 in 4) | an old doe or a fawn lags; the wolves close in and pass **behind a bush or the trees' edge**; the doe goes down there, out of sight (no blood; the bush `Sway`s once). The wolves stay there, heads down |
| Answer: ravens | within 5 s, **2–3 ravens** (black, 0.03 u) circle above the spot (r 0.2 u, 0.35 u up, 4 s a circle), then land near it |
| End beat | after 20 s the wolves lie down by the kill; the ravens hop round them |
| People | anyone within 2.5 u `Turn`s; a hunter **points at the ravens** (the sign for B23.b) |
| Rule | a kill counts toward the deer's floor (#422); no chase is planned when the herd is at its floor |
| 3-s read | "wolves chasing deer at the wood's edge" |
| Cheat | **"Pack hunt now (catch / miss)"** |
| Tests | the kill point is hidden from the default camera by a bush or tree (a line-of-sight check; with no cover the chase always misses) |

---

## B21. The howl, the eyes, the knock, the white wolf (#441)

### B21.a The howl at dusk
*One wolf howls, the others join; at the fire everyone falls silent; a child howls back; the wolves answer.*
| | |
|---|---|
| When | most evenings (view hours 18–20); longer after a kill |
| Wolves | at the den one sits and howls (`SitHowl`), the others join 0.4–1.0 s apart; 4–6 s |
| People | everyone at the fire stops talking (chatter bubbles off for 4 s, `View/ChatterView.cs`) and `Turn`s toward the wood (the head only for those sitting) |
| The kid | 1 evening in 3: a kid stands, cups the hands to the mouth (both arms x 130) and **howls back** (head up 40°, 2 s); a grown-up pulls the kid down by the arm (0.5 s); 1 s later the wolves answer (a short `SitHowl`) and the kid grins |
| Sound | a howl chorus (#447); the picture reads without it |
| 3-s read | "wolves howl; the people at the fire turn and listen" |
| Cheat | **"Howl now"** |

### B21.b Eyes at the edge of the firelight
| | |
|---|---|
| When | some nights; more once the pack is bold (#437) |
| Beat | two pairs of eyes (P0's eye dots) glint at **the firelight's edge**, always outside the tent ring; the bodies stay dark |
| Answer | someone throws a stick on the fire (a `Log` arc, h 0.1, T 0.4 s): the fire `Flare`s (scale 1.3, `Sparks`); the eyes blink out, back off 0.3 u, and go |
| 3-s read | "eyes in the dark; someone feeds the fire" |
| Tests | no eye point inside the tent ring |

### B21.c The knock
| | |
|---|---|
| Beat | the Keeper's knock on the glass (exists: `View/GlassVoice.cs:401`, `Life/LifePlanner.Scenes.cs:335`) also reaches wolves within 2 u of the tap: `Alert` toward the glass for 0.3 s, then they **melt into the trees** (`Bolt`, 1.2 s) |
| Rule | the Keeper's only power over the wolves, and a gentle one (research 19 §9) |
| Tests | P3 (every wolf within 2 u answers) |

### B21.d The white wolf (a pure moment, no choice)
| | |
|---|---|
| When | once per box, in a winter after the pack's first pups |
| Beat | at dusk a white wolf stands on the highest ground at the wood's edge and howls (`SitHowl`, 5 s); the pack joins below; **everyone comes out** of the tents and stands looking (all `Turn`; a kid is lifted onto a grown-up's back, 0.6 s) |
| End beat | the white wolf turns and is gone into the trees; a book page |
| Rule | never hunted, no pelt (wolves are never killed) |
| 3-s read | "a white wolf howls on the hill; the whole camp watches" |
| Cheat | **"White wolf now"** |

---

## B22. The meat thief, the scraps choice and the grey wolf (#442)

### B22.a The meat thief
*A wolf slinks to the drying rack; a hunter shouts, everyone waves their arms and a torch; the wolf grabs a strip and
trots off, tail up.*
| | |
|---|---|
| When | at most once a season, at dusk; never when the stash is low (research 19 §4 rule 3) |
| Where | the drying rack (B16.c) or the hide frame (`View/CampView.cs:246-248`), **at the camp's edge**, outside the tent ring |
| Approach | a wolf comes out of the dark low (body pitch −10°, slow `Walk`) and stops twice |
| Antic | a hunter sees it, stands, **points and shouts** (arm x 90, two head jabs) |
| Act | everyone near stands and waves both arms (z ∓150 ↔ ∓100 at 3 Hz); one takes a burning stick from the fire and holds it up (a torch prop) |
| Answer | the wolf snaps a strip off the rack (a small brown strip `Pop`s into its mouth) and **trots off** with it into the dark, tail up |
| End beat | the people stand looking after it; one shrugs, one laughs |
| Rule | the wolf never comes within 0.3 u of a person; if someone walks at it, it leaves at once |
| 3-s read | "a wolf steals meat from the rack; the people shout and wave" |
| Cheat | **"Meat thief now"** |

### B22.b The choice: chase them off, or leave them the scraps? (staged)
| | |
|---|---|
| When | after the first theft, raised while you watch (visit-loop.md §2.4) |
| Staging | the rack with a strip missing; on one side the hunters with a torch and an armful of thorny branches; on the other the elder with a bowl of scraps at the camp's edge (the sides as `Life/ChoiceScenes.cs` places them) |
| Chase them off | the hunters lay a ring of thorny branches round the rack (B18.a's weave, 6 s) and set a torch stake by it, lit each night; the wolves howl from farther off (B21.a at the den only, no B21.b eyes for a season) |
| Leave the scraps | a **bone spot** at the camp's edge (outside the tent ring, ≥ 1.0 u from the fire): a small pile of pale bones; B22.c starts |
| Let them decide | the chief's trait picks (a bold chief shares, a careful one chases) |
| 3-s read | "thorns round the rack, or a pile of scraps for the wolves" |

### B22.c The grey wolf at the bone spot
| | |
|---|---|
| Who | **the grey wolf**: the boldest of the pack, recognisable by its pale silver face (P0) |
| Beat | at dusk the grey wolf comes to the bone spot, takes a bone and lies down 0.2 u from the pile to chew (head tilted, a gnawing bob); the people at the fire **sit still and watch**, talking in murmurs (small chatter bubbles); a kid edges two steps closer and is pulled back |
| Over time | it stays longer each visit and lies a little nearer the light (from 1.3 u to 1.0 u from the fire over a game year), never nearer |
| 3-s read | "a wolf chewing a bone near the camp while the people watch" |
| Tests | the bone spot and the grey wolf stay outside the tent ring and ≥ 0.3 u from any person |

---

## B23. Trailing the hunters, and the ravens' kill (#443)

### B23.a The wolf trailing the hunters
| | |
|---|---|
| When | on a deer hunt (B13.a) while the pack is bold |
| Beat | a wolf follows the hunters far behind (≥ 1.0 u) and stops when they stop; after a hit the hunters leave a **leftovers bundle** (a small wrapped brown bundle, no blood) where the deer lay; once they've gone with the pole, the wolf trots up and takes it |
| Later (the scraps path) | each hunt it trails a little closer (1.0 → 0.6 u) |
| 3-s read | "a wolf follows the hunters at a distance" |

### B23.b The ravens' kill: take it, or leave it? (staged choice)
| | |
|---|---|
| When | after a B20 kill with ravens, raised while you watch |
| Staging | the ravens circling over the wood; a hunter points; the hunters stand ready with bows on their backs and a pole; the choice card |
| Take it | the hunters walk over in a line; at 0.6 u from the wolves they **shout and wave** (B22.a's wave, a bow held up high); the wolves back off 0.5 u, stand, then trot away; the hunters carry the meat home on the pole (B13.b) |
| Leave it | the hunters stand at the wood's edge a moment, then walk home; the wolves eat; the pack is a little bolder |
| Rule | nobody touches a wolf and no wolf touches anyone: the planner keeps ≥ 0.3 u between them, and the wolves always leave |
| 3-s read | "hunters drive wolves off a kill" |
| Cheat | **"Ravens' kill now"** |

---

## B24. The lost pup and the pup at the fire (#444)

### B24.a The lost pup (staged choice)
*A child found a wolf pup alone by the den. Bring it home, or leave it with its mother?*
| | |
|---|---|
| When | spring, on the scraps path; on the chase path two springs later anyway (dogs are never closed off, decisions.md 2026-11-07) |
| Staging | in the wood a kid crouches by a tiny grey pup (P0's pup, 0.05 u) that tumbles toward them; the kid looks back at the camp; a parent walks over; the choice card |
| Bring it home | the kid **picks the pup up in both arms** (arms x 60, held at the chest) and walks home with it, the parent beside; at the fire everyone gathers and the kids crowd round. The dog arc starts (#438) |
| Leave it | the kid sets it down; the pup runs back toward the den (`Trot`, one tumble); the kid watches it go (`Turn` held 3 s). It can come again next spring |
| 3-s read | "a kid carries a wolf pup home" |
| Cheat | **"Lost pup now"** |

### B24.b The pup at the fire
| | |
|---|---|
| Its keeper | the kid who found it (a bond, research 19 §7): the pup follows them, 0.1 u behind (B7.c-style) |
| Scenes (one at a time; B11 counts them) | chews a hide (head shakes ±25° at 4 Hz); plays with the kids (`PlayBow`, and a kid bows back, arms down); sleeps curled by the embers (`SleepCurl`); **steals a fish** from the stash and runs with it in its mouth, a grown-up chases 0.5 u and gives up |
| A bit wild | it never fetches; now and then it growls at a grown-up near the meat (head low, 0.5 s) |
| 3-s read | "a pup playing with kids by the fire" |

---

## B25. The grown dog at the hunt and on guard (#445)

### B25.a At the hunt
| | |
|---|---|
| Flush | on a rabbit hunt (B6) the dog runs ahead into the grass; the rabbits `Bolt` (`RabbitHerd`'s scare, `Sim/Eco/RabbitHerd.cs:411`) toward the hunter, who gets the throw |
| Track the miss | after a deer-hunt miss (B13.a) the dog `Sniff`s where the deer stood, trots into the wood nose down, stops and **barks**; the hunters follow; 1 time in 3 it finds a lame deer, which then counts as a hit (the B13.b carry) |
| Look | the hide collar from stage 2; the generations' drift (P0) |
| 3-s read | "a dog runs ahead of the hunters" |

### B25.b On guard
| | |
|---|---|
| Beat | at night, when a wolf comes within 1.2 u of the camp (B21.b's eyes, B22.a's thief): the dog stands, faces it and **barks** (three head jabs, a 0.03 u hop forward each); the wild wolves `Alert` and turn back into the dark |
| Then | from the first guard on, the meat thief scenes stop |
| 3-s read | "a dog barking at the dark; the eyes go away" |
| Cheat | **"Wolves come near (guard)"** |

### B25.c A litter, and the old dog
| | |
|---|---|
| Pups | next spring 2–3 pups at the dog's side (`SleepCurl` against it, `PlayBow` with the kids); each is the next generation (P0's drift). With the cap of 1–3 dogs (#438), extra pups go with a kid to the box's edge and fade out there, with a history line ("taken in by the people apart") |
| Old dog | grey round the muzzle, lies by the fire more; one visit it is simply gone, with a history line (the existing old-age rule) |

---

## B26. The wolves at the duck pen (village, once) (#446)

*The wolves circled the duck pen last night. A thorn fence, or let the dogs keep watch?*
| | |
|---|---|
| When | once, the first night after the duck pen (B18.a) stands, if wolves are placed |
| Night | 2 wolves trot once round the pen 0.2 u from the fence (10 s); the ducks inside huddle in the middle (quacking head jabs); nothing is taken |
| Morning staging | villagers find tracks round the pen (paw prints, drawn like the rabbit tracks) and point; the choice card |
| A thorn fence | villagers weave thorny branches round the pen (B18.a's weave, darker and spiky) and set a torch stake by it at night |
| The dogs keep watch (only with dogs) | a dog sleeps by the pen gate (`SleepCurl`); seen once: the wolves come, the dog barks (B25.b), they turn back. Then it's routine |
| Rule | either way the pen is safe from then on; wolves never take from a guarded pen |
| 3-s read | "wolves circling the duck pen at night" |
| Cheat | **"Wolves at the pen now"** |

## B27. A path comes: the rack is taken down and set up again (#700)

*A path has grown over a rack. At a quiet hour two people lift the fish off, pull the poles, carry them a few steps and plant them again on free ground; the old spot is left bare.*

**Status: built on `vs/700-beat` (Life/RackMove.cs, LifePlanner.RackMove.cs, View/Beats/RackMoveBeats.cs, View/RackMoveView.cs); AnimLab clips `Tools/AnimBench/clips/700-fish-rack.json`, `700-hide-rack.json`; waits for the user's sign-off.** Built as: the crew start at dusk/night, or by day when empty, or at once with the cheat "Rack moves now"; the Lifter carries the ridge pole and the basket, the Carrier pulls and plants both frames; no walk-in beat of its own beyond a plain walk; the beat runs on the plan's clock in real seconds. The rule that decides *when* and *where* (never on a path cell, move at night or when empty, the nearest free angle) is built: `Life/RackSites.cs`. Until this beat exists the rack simply stands on its new spot at that moment. Applies to the fish rack (B16.c, `Life/FishRackSpot.cs`) and the hide rack (`Life/LifePlanner.Tents.cs`, `HideFrame`); the beat is the same, only the loads differ.

### B27.a The take-down and rebuild
| | |
|---|---|
| When | the first quiet hour after a path covers any cell of the rack (`RackSites`): at dusk or in the night block (`LifeClock` Evening, 17:00-20:00, or Night), or by day when the rack holds nothing. Never while someone is hanging or taking down. Once per move; two racks do it one after the other, never at the same time |
| Actors | 2 grown-ups: the rack-keeper (`Life/LifePlanner.FishChain.cs` `RackKeeper`, or the hide scraper) as `Lifter`, and the nearest idle grown-up as `Carrier`. Others in the block keep their own plans |
| Props | the rack in 3 parts: the ridge pole (`Pole`, 0.4 u, carried on the shoulder by two) and two A-frames (`Frame`, 0.14 u, one under each arm); fish and hides go to the basket first, or to the stash pile if the rack was dry |
| Antic | both turn to the rack and stop 0.12 u from it; the Lifter rubs the back of the neck (arm z 80, 0.4 s) and looks at the path, then at the rack, then at the free ground (3 head turns, yaw +-40, 0.3 s each): "here is the problem, there is the answer" |
| Take down | fish and hides come off one at a time (as the take-down beat, `FishRackSpot.TakeFraction`; arm x 150, 0.5 s each) into the basket. Then the ridge pole is lifted off both frames (`Back` ease, 0.6 s, the bar rises 0.1 u and **tips** 15 deg): the empty frames `Jolt` once. Each frame is pulled up (crouch 0.04 u, arm x 120 -> 40, `Snap` 0.15 s): the frame leaves the ground with a `Dust` puff and a small dark hole stays |
| Contact | the last frame leaving the ground: `hit` once, 3 `Dust` puffs, 4 `Grass` blades |
| Carry | Lifter takes the ridge pole on one shoulder (arm x 160), Carrier takes both frames (one under each arm, a slight lean back); they walk to the new spot at 0.25 u/s, in step (`Lead`/`Side`), 0.3-1.0 u. The basket is carried by whoever is free, or set down at the old spot and fetched |
| Plant | at the new spot: Carrier sets a frame down (crouch, `Squash` 0.2 s) and presses it in with both hands (`Jolt` 8 Hz tau 0.12 s, `Dust` 2); the second frame the same, 0.5 u apart (the bar's length) |
| Raise | both lift the ridge pole to the frames together (arm x 170, `Back` 0.5 s) and **drop it into place**: the bar `Jolt`s, the frames wobble 4 deg, 5 Hz, tau 0.2 s. The Lifter tugs it twice to test it |
| End beat | the fish or hides go back on, one at a time (the existing hang beat, `FishRackSpot.HangFraction`); the Lifter steps back with hands on hips (arm z 70) and nods, and the Carrier waves a hand at the old spot, now bare: "there, out of the way" |
| Old spot | the poles' holes show for the rest of the day (4 dark dots, 0.02 u), then the grass closes over them |
| Timing | take-down 5-6 s, carry 3-8 s by distance, plant 4 s, raise 3 s, re-hang by count: about 20 s real. Fits in one 0.5 view hour at dusk. By day it is allowed only when empty and then skips the hang |
| Close shot | start 2 u from the fire at the default camera; the old spot, the path and the new spot all in frame (they are within 1.2 u of each other) |
| 3-s read | "they took the rack down and put it up somewhere else" |
| De-sync | Carrier starts 0.3 s after the Lifter; the two walk with step offsets (0.9-1.1) as `Lead`/`Side` |
| Cheats | **"Path over the fish rack"** / **"Path over the hide rack"** (`View/SimDebugPanel.cs`, `// #700`), then "Bedtime: clock to 20:00" |
| Tests | P1 (a hand within 0.11 u of a pole at the lift and at the plant), P2 (the frames `Jolt` within 0.2 s of the lift and of the plant), P5 (one `EndBeat`: the re-hang), P8 (every contact held >= 0.1 s; offsets >= 0.025 u). Plan tests: the rack's new centre is never on a path cell (`RackPathTests`); only one rack beat at a time; nobody is tied to the rack by their own duty during it |

### B27.b If a move is hurried or unseen (rules, not a second beat)
| | |
|---|---|
| The Keeper is looking away or the box is paused | the beat still runs on the plan's clock; on return the rack is already planted (the view catches up as for every beat) |
| The game was closed | the rack simply stands on its new spot on load; no beat |
| No free nearby spot | it keeps its old spot, and the beat does not run (a rack on a path is better than none) |

## B28. Hanging out the washing: a line, cloths, and the taking-in (#705)

*She lifts a wet cloth from a basket, shakes it out, pins it to a line, smooths it flat, and steps to the next. When she goes the cloths stay, drying, for a while; then someone takes them in.*

**Status: spec only. Not built; waits for the user's sign-off, then an AnimLab clip, before it goes into `dev`.** Today `ActivityKind.HangWashing` (`Life/LifePlanner.Day.cs:976`, one of the four `DawnChores`; `ChoreProp` gives `Prop.Cloth`) plays `DollStyle.Hang` (`View/DollMotion.cs:508`, #543): both arms swing at x 140 +-25 over 1.6 s with a bundle in the hand and nothing to hang it on. A playtester saw someone miming cloth, and nothing left behind (private note feedback#61). The place is already `SlotSpots.HomeSpot.Line` (0.3 u along the wall from the door, 0.1 u out); this beat gives that place a prop. The same line serves the weaver's `Alt(... HangWashing ...)` (`LifePlanner.Work.cs:361`). Precedent for "left behind for the session": `View/CampBedrolls.cs` (#651).

### B28.a The line and the cloths (the prop)
| | |
|---|---|
| Line | a cord 0.55 u long between two dark-wood stakes 0.17 u high (0.014 u thick), so a doll's raised hand (arm x 150, reach about 0.2 u) reaches it. At the home's `Line` spot, **parallel to the wall**, 0.1 u out. One line per home, shared by housemates |
| Cloths | 4 (up to 5 in a big home), each a flat quad **0.11 w x 0.13 h u** hanging from the cord with a 0.012 u fold at the top, 0.03 u apart. Muted, no two neighbours alike: ochre, faded blue, undyed linen, dull rust. Never white (it reads as paper on grass, as the first bedrolls did), never saturated. One is small (a kid's shirt, 0.08 x 0.09 u) |
| Pins | a 0.015 u dark dot at each cloth's top corners |
| Basket | a low wicker basket (0.05 u) on the ground at the near stake, a wad of the cloth colours at the rim. Present only while the chore runs; the doer carries it in after |
| Where (rule) | the `Line` spot, swapped to the door's other side (`blockSide`) and then out in front when blocked, by the same checks as the kindling block. **Never on a path cell**: the path test #700 added (`CampSpots`, `Life/RackSites.cs`) applies, and a line whose cord would cross a path takes the other side of the door. Never under a tent's roof edge; never within 0.12 u of the fire, a rack or another line |
| On a phone | at the default camera 1 u is 120-140 px, so a cloth is about 14-16 px wide and 17-19 px tall (the small one about 11 px), the cord about 70 px: a row of coloured patches that reads as washing. **Trade-off:** a cloth is about 0.6 of a doll tall (a real shirt is nearer 0.3), so it is oversized; we pick the readable size over true scale, as the doll itself is oversized. Each cloth >= 0.11 u wide, the line clear of the tent's shadow; at a close zoom the cloths scale with the doll

### B28.b The beats (one doll, per cloth; 3 cloths in the dawn window)
| | |
|---|---|
| Approach | she arrives at the near stake with the basket (`PropSlot.Hand`) and sets it down (`Squash` 0.15 s), facing the line. The stakes and cord `Pop` into place (0.25 s) so the eye sees the line put up |
| Lift | bend to the basket (lean 25, 0.25 s), arm x 60 -> 110: the cloth comes up **wet and heavy**, sagging 0.03 u from her fingers (`Wobble` 12 deg, 3 Hz, tau 0.4 s) |
| Shake | stand, both arms x 130 -> 150 -> 130 (`Snap` 0.12 s): the cloth snaps out flat; `Droplets` of 3 |
| Reach | both arms x 160-170, the cloth up against the cord (`Out` 0.3 s); head back (pitch +10) |
| Contact | **pin**: right hand x 175 -> 150 (`Snap` 0.08 s), `hit`, the cloth latches to its pin dot with a one-frame `Jolt` of 0.01 u. A second pin 0.25 s later, `hit` again: the cloth is now a flat rectangle |
| Hold | 0.12 s, hand on the cloth (P8) |
| Smooth | one hand sweeps the cloth top to bottom (arm x 150 -> 100, 0.4 s, `Smooth`) while the other holds the cord; the fold flattens as it passes |
| Step | side step 0.1 u (0.3 s), back to the basket for the next cloth. After the last cloth: shake both hands dry (arm z 40, 2 Hz, 0.5 s) and take up the empty basket |
| End beat | the line **full, swaying** (4 deg, 0.6 Hz, tau 3 s); she looks at it, one nod (pitch -8), and carries the basket in or on to her next block. P5: one `EndBeat` |
| Timing | per cloth: lift 0.6 + shake 0.35 + reach 0.3 + pin 0.35 + smooth 0.4 + step 0.4 = 2.4 s real; 3 cloths 7 s. The chore today is 0.35-0.55 view hours (3.5-5.5 s at dawn's 10 s per hour): **stretch it to 0.7 h** when the home can have a line (`HomeChoreTurn`, still inside the home's dawn window); the 4th cloth only if the window is longer |
| De-sync | the doer starts 0.2-0.5 s late by key hash; cloth colour order is hashed by the home's key, so two lines differ |
| 3-s read | "someone pegging washing on a line" |

### B28.c States of the line
| State | When | Look |
|---|---|---|
| Empty line | no chore today, or taken in | **nothing drawn**: a bare cord is a line of pixels and a question. Stakes exist only while the chore runs and cloths hang |
| Going up | during B28.b | stakes and cord at the Approach, one cloth at each Contact |
| Hung | after she leaves | all cloths in place, swaying 4 deg at 0.6 Hz. Colour **dries**: 70% brightness wet to 100% dry, linear over the stay, so time passing reads |
| Taken in | the stay's end | B28.d |

### B28.d How long they stay, and who takes them in
| | |
|---|---|
| Stay | **1 view hour after she leaves** (about 16 s real at work hours; 60 game minutes), and never past 17:00. Fewer is a blink: one view minute is 0.27 s real, so "a few game minutes" read literally would look like a glitch. This is the shortest stay that reads as "left behind" |
| Who | the doer if she is home, else a **housemate** idle at the door (`Indoors` or door-sitting) at the stay's end: she walks out (0.6 s), plucks the cloths right to left (arm x 160 -> 60, 0.3 s each; each `Pop`s into the basket), carries them in; the stakes `Pop` out after the last. 3 cloths: about 4 s real |
| Nobody free | nobody in reach by the 2 h mark: **a quiet fade**: the cloths fold up (scale y 1 -> 0.2 over 0.8 s) and vanish with the stakes. Why: a plan never waits on laundry (it is a chore, not a duty), and a fade is kinder to the eye than a pop. The same fade covers a paused, hidden or unwatched box |
| Not seen | the beat runs on the plan's clock; on return the line is in whatever state the clock says (as B27.b) |

### B28.e Night, rain, winter
| | |
|---|---|
| Night | the line is **never** up at night: a dawn chore, in by 17:00. A line left overnight reads as forgotten |
| Rain, storm, snow | no hanging on a wet day (`Weather.KeeperRain`, `Storm`, `Snow`): that day's chore is `SweepStep` (the swap `WithWood` makes for kindling). If rain starts while cloths hang, the doer or a housemate takes them in at their next free moment (B28.d's taking-in, no hurry), else the quiet fade. Fog is dry: the line stays, cloths dimmed |
| Winter | no hanging (cloths freeze stiff): the chore is `SitOut` or `SweepStep` |
| Kids | a kid beside the doer may hand her the next cloth (B7.b) once per chore; never needed |

### B28.f Cheats, the AnimLab clip, tests
| | |
|---|---|
| Cheats | **"Laundry now"** (`View/SimDebugPanel.cs`, `// #705`): the home nearest the camera centre does the chore at once, with the stay shown at 16 s. **"Take in the washing"** (end the stay now) |
| Clip (AnimLab) | three shots, front view, default camera and a close zoom: (1) B28.b for two cloths in real time, ending on the full swaying line; (2) the hung line at 4 x speed: wet to dry, the walk out, the plucking and the carry in; (3) the fade. Plus a **still at the default camera, phone-size render (1179 x 2556)** to judge whether four patches read as washing, and prop renders of the line (front, side, from above) |
| Sign-off | the user sees: cloths on the line during the chore and after it; the pin, the smooth and the taking-in each once; a row of muted patches that reads as washing at the default camera |
| Tests | P1 (a hand within 0.1 u of the cord at each pin), P2 (the cloth `Jolt`s within 0.2 s of the pin), P5, P8 (contacts held >= 0.1 s; offsets >= 0.025 u). Plan tests: the line is never on a path cell; no hanging on rain, storm, snow or in winter; no line at 17:00; the taker is the doer or a housemate; the chore stays inside the dawn window |


### Placed life: hard in code

1. **There is no four-legged doll.** `RabbitDoll` (`View/RabbitDoll.cs`) is the model to copy; P0 is #425's work and
   B13, B14 and B20–B26 wait on it.
2. **`PondLife`'s ducks are view-only**, shown by pond size (`View/PondLife.cs:26`); B12.d and B15 need the sim's
   stock (#421), nests, ducklings and the pen.
3. **Fishing has no stock** (`Life/LifePlanner.Fish.cs`): B16.e gates it on placed fish, and a bite needs a visible
   shadow near the hook.
4. **New props:** `Bow`, `Arrow` (arc), `Pole`, `Egg`, `Antlers`, a torch, a snare, a bone pile, the drying mat, the
   weir's basket. One prop per doll (Hard in code item 8 below): the bow rides on the back while a hunter holds the
   pole.
5. **The tent ring must be a planner rule** for every wolf point (B21–B23, B26), tested like `Life/HugRules.cs`.
6. **Hidden kills** need a line-of-sight check from the default camera to cover between it and the kill (B20).
7. **The rack and the hide frame must stand at the camp's edge** so B22.a reaches them without crossing the tent ring;
   today the hide frame stands where the planner puts it (`View/CampView.cs:246-248`).

---

## Hard in code (for the B workers and the orchestrator)

1. **Bushes and trees are baked into the land's chunk meshes** (`View/BoxView.Eco.cs:69-124`), so one bush can't
   shake on its own. Cheapest: a global shader array of up to 8 shake centres (position, start time, strength); the
   vertex shader sways vertices near a centre. No `MaterialPropertyBlock` (SRP Batcher, research 17 §6.6).
2. **`Woods`, `Pond`, `ForestEdge` are one cell each** (`Life/SettlePlacePositions.cs:354-357`). Woodcutters (B10),
   fishers (B8), woods games (B3) and drawers (B4) need real tree and bank spots from the eco land.
3. **Peg dolls have no legs.** No kicks: the ball is tossed, running reads through bounce and lean.
4. **`FetchWater` is 0.2 h at dawn = 2 s** (`Day.cs:720`; dawn is 10 s per view hour). B4 needs 0.35 h.
5. **The hunt's timing and geometry**: Strike 0.06 h ≈ 1 s, lunge 0.125 u from the rabbit (`Hunt.cs:73, 237`). A throw
   needs a 0.4 u throw point and Strike ≈ 0.2 h. The stalked rabbit's sit-up is the view's today
   (`RabbitView.cs:296`); for the freeze it has to come from the plan.
6. **Pick cadence vs the sim**: the sim takes a berry every 5 s per picker (`BushView.cs:32`); the spec picks every
   2.5 s. Only every 2nd visual pick removes a dot, or the sim's rate changes (a balance call).
7. **Water is 0.08 u below the land** (`Terrain.cs:95`). A kneeling doll's hand reaches about ground level; the
   bucket needs a 0.06 u dip offset on the prop.
8. **One prop per doll** and missing meshes: ball (`PeopleView.cs:805`), bowl, root, fish in the basket, basket berry
   dots. Hand-overs swap props between dolls on one frame.
9. **Style changes only blend for hidden or lying poses** (`PeopleView.cs:441`): one-shot beats inside a style must do
   their own transitions.
10. **Offsets today are below a pixel** (talk bob 0.006, creep 0.01): every style that keeps a small offset needs × 2–3.

## Open questions for the user

1. **Hugs** between peg dolls: on or off? (B7.d, C3.a.) **Decided 2026-11-06: on, used in the game.**
2. **Hit-stop**: a 0.1 s freeze on the key beats (spear lands, fish yanked, tag, dip, found, tree lands), or none?
3. **A kid tumbles** in the rabbit chase (falls over in a puff of grass, gets up laughing): cute, or too much? **Decided 2026-11-06: sometimes (about 1 chase in 4).**
4. **Felling** (B10): build it now (keepers-hands §8.3 says yes) or wait until the town era needs it?
5. **The washing's stay** (B28.d): 1 view hour (about 16 s real) after she leaves, then a housemate takes it in or it fades. Is that "a few game minutes", or longer? And no hanging in rain, snow or winter: right?

---

## Batch 3 (2026-11-15): people and things, the fish, the nest

**Status: specs only. Nothing here is built. Each spec waits for the user's sign-off, then an AnimLab clip signed off, before
it merges** (decisions.md 2026-11-14 "People and things: animation sign-off"; the user again on 2026-11-15: "I will have to
approve the animations and assets"). The checklist to answer is [batch3-approvals.md](batch3-approvals.md).

**What this covers, and where each call was checked:**
- **#692** (camp gaps), **#693** (choice props), **#694** (dogs and wolves), **#695** (village and town): the uses approved in
  decisions.md 2026-11-14 "People and things: the uses (#691)" and listed in [people-and-things.md](people-and-things.md).
  The dropped ones are not specced: trying on the fur, measuring against saplings, knapping stones, throwing pebbles in the
  pond, feeding rabbits, imitating the waddle or the croak, life uses at the building site. Added by the user and specced:
  meat on a spit, feathers tied after good events, more quiet visits to the Keeper's tree.
- **#708**: decisions.md 2026-11-15 "A deeper pond seen through the glass": fish first, inside today's water wall, shadows
  from above. The deeper basin comes later.
- **#709**: decisions.md 2026-11-15 "The bird from the bushes": one nest in a placed thicket, one parent in spring, kids look,
  a gatherer takes an egg now and then, never the last, no hunting.
- The vine: decisions.md 2026-11-14 triage: folded into the forest-edge overgrowth, no separate vine (#685). "Vines" below
  means weeds and creeping seedlings.
- Wolves: decisions.md 2026-11-07: the people never kill a wolf; they drive them off with shouts, torches and fences; wolves
  never come in among the tents. Dogs capped at 1–3.
- Animal counts: `Sim/AnimalBudget.cs` (on the 6 × 6 land: rabbits 8, deer 6, ducks 6, fish 5, frogs 4, wolves 4). Nothing
  below adds a drawn animal except the nest's bird (B71, an open question).
- Stew pot look: signed off in the 2026-11-15 walkthrough (#676 comment). Feather pole and feedback#79: #566 comments.
- **S-ritual** (B72–B77, added after the 2026-11-15 approvals, row 217): the four yearly rituals (#721), the teller's manner
  (#717) and the pond family's boat (#714). Approvals rows 218–229.

Every number is [est] until AnimBench and AnimLab. Calls of mine that are more than a number are marked **[est]**.

### D. Shared rules for every batch 3 spec

| | |
|---|---|
| **The work still gets done** | A life use only takes free time: kids' `Play` blocks, grown-ups' `SitOut`, `Chat`, `Visit` and walks, or the B11 director's borrow (at most 0.5 view h, kids and idle grown-ups first). A work use replaces part of an existing block of the same work; it never adds a duty. Never during a meal, sleep, a hunt, a staged moment, a ritual or a choice. Test per issue: over 10 view days each role's work minutes stay within 5 % of today and no meal or sleep is lost [est] |
| **How often** | **Often**: most view days while the conditions hold (1 or more a day). **Sometimes**: about 1 view day in 3. **Rare**: a few times a game year, or on a trigger. B11 never runs the same use twice in a row, nor within 30 s. One batch 3 life use per thing at a time |
| **When** | Daylight unless the spec says dusk or night. Real seconds per view hour: dawn 10, work 16, midday 25, evening 16.7, night 5 (`Life/LifeClock.cs`) |
| **How it reads on a phone** | The default camera frames the camp: a doll is about 55 px tall on a 1179 × 2556 phone, so 0.01 u ≈ 2.5 px (Placed life intro above). Key offsets ≥ 0.025 u, arm swings ≥ 40°, new props ≥ 0.03 u unless the spec says. Every spec reads with the sound off (P4) |
| **Kids stay safe** | A kid never touches flames or a wolf, and never wades deeper than 0.04 u. A grown-up is within 0.6 u for any fire or water use |
| **Things left behind** | What a use leaves (a stone ring, a twig, a pebble stack, a feather, a gift) is a small prop with a cap per box, given per spec. Never on a path cell (the `Life/RackSites.cs` rule) |
| **Sound** | None unless the spec says. A new sound is CC0 (Kenney) and goes in `docs/assets-log.md` |
| **Cheats** | One block per issue in `View/SliceDebugPanel.cs` (beside "Feather pole: pray now"), tagged `// #692`, `// #693` and so on. A cheat starts the use now with the people nearest the camera's centre, and sets the season or hour when the use needs one |
| **AnimLab** | One AnimBench clip per spec (`Tools/AnimBench/clips/<issue>-<name>.json`), rendered in AnimLab (`Tools/animlab.sh`) and signed off by spec number. P1, P2, P5, P8 wherever there is a contact (§0.9) |
| **Reuse marks** | **R** = existing code as is · **R+** = existing code with a small change · **N** = new |

---

### #692 part 1: the fire, the stew pot, the feather pole

#### B29. The fire

##### B29.a Warm hands in winter
| | |
|---|---|
| Who · where · when | 1–3 people already sitting at the fire (`SitOut`, `Chat`, `ListenStory`), in winter and on snowy days. **Often** in winter: each sitter every 6–10 s by hash, never two within 0.5 s |
| Beats | lean in 10° (0.4 s `Smooth`); both arms out to the flames (x 75, z ∓10), held 1.2 s; **rub**: the arms swap x 75 ↔ 85 at 4 Hz for 0.8 s; a shiver (roll ±4° at 10 Hz, 0.3 s); arms pulled in (x 40), a self-hug 0.8 s. End beat: one `Steam` blob at the head, a breath in the cold |
| Props | none |
| On a phone | two stubs reaching to the flames (an arm is 0.075 u ≈ 19 px), the rub, the white breath against the dusk |
| Sound · cheat | none · **"Winter fire: warm hands"** (sets snow, seats 3 at the fire) |
| Reuse | R `Sit`, R `Steam`, R the fire's sitter list (`MealDirector` pokers). N the rub beat |

##### B29.b Kids poke sticks in the fire
| | |
|---|---|
| Who · where · when | 1–2 kids at the fire with a grown-up sitting within 0.6 u. Evenings, **sometimes**; not during the bowl round |
| Beats | the kid squats 0.2 u from the fire (squash 0.8) with a `Twig`; B9.c's poke at kid scale: raise x 120 (0.3 s), thrust x 60 (0.15 s `Snap`) → `hit`: 3 `Sparks` + a small `Flare` (1.15); the kid hops back 0.04 u, arms up (x 70), delighted. 2–3 pokes. Then the twig's tip glows and the kid draws circles in the air (arm x 100, circle r 0.03 u at 1.5 Hz, 1.5 s). The grown-up holds out a hand: B7.b hand-over of the twig, which goes into the fire (`Log` arc, h 0.05). End beat: the kid sits by them, a hand on the kid's head (arm x 110, 0.8 s) |
| Props | `Twig` (R); an ember dot on its tip, orange, 0.012 u (N) |
| On a phone | sparks and the hop back read as poking. The ember circle is about 15 px across: check it at the default zoom, × 1.5 if it doesn't read |
| Sound · cheat | none · **"Kids poke the fire"** |
| Reuse | R B9.c `PokeFire` at kid scale, R `Twig`, R B7.b. N the ember tip and the circles |

##### B29.c A berry on a stick
| | |
|---|---|
| Who · where · when | a kid (now and then a grown-up) at the fire, late-summer and autumn evenings, while the stash has berries. **Sometimes** |
| Beats | takes a berry from a stash basket (bend, 0.4 s); pushes it onto a `Twig` (both hands together x 70, 0.4 s; the berry `Pop`s onto the tip); holds it over the fire (arm x 95, lean 10) for 2.5 s while it darkens and swells (scale 1 → 1.3); a spark pops off it → `hit`; pulls it back, blows on it (head forward 0.015 u twice, 2 small `Steam` blobs), eats it (hand to mouth x 135, chew bob 0.01 u at 8 Hz, 0.6 s), then fans the mouth (hand x 130, z ±30 at 5 Hz, 0.6 s): hot! End beat: a grin (`Laugh`, 0.6 s) |
| Props | `Twig` (R) with a berry (the `Berry` arc mesh, r 0.022 u, R) |
| On a phone | a stick held into the flames, then a hand fanning the mouth |
| Sound · cheat | none · **"Berry on a stick"** |
| Reuse | R `Twig`, `Berry`, `Steam`, `Laugh`. N the hold-over-the-fire beat |

##### B29.d Meat on a spit after a hunt
| | |
|---|---|
| Who · where · when | the hunter whose catch it was, the cook, the people at the fire. The evening after a rabbit catch (B6) or a deer (B13; the first deer's feast B13.c already names a spit). Rabbit: 1 catch evening in 2, the other goes in the stew (#676) [est]. Deer: every deer evening |
| Beats | **Set-up** (dusk, ~4 s): the hunter pushes two forked sticks in either side of the fire (crouch, push down 0.02 u, `Snap` → 2 `Dust` and a `Jolt`, twice), then lays the bar with the meat across them (arm x 150 → 100, `Back` 0.5 s). **Turning**: a sitter at the bar's end turns it (arm x 80, a small circle z ±20 at 0.5 Hz); the meat turns once every 4 s; every 2 s a drip makes a spark and a `Flare` 1.1; the meat browns from pink-brown to rich brown over the first evening hour (16.7 s). **Done**: the cook lifts the bar off (both arms x 100, `Back`), sets it on a flat stone, tears a piece (arm x 60 → 100, `Snap` 0.15 s; a brown piece `Pop`s into the hand) and goes round the ring as in B9.b with a piece instead of a bowl. Kids crowd in and sniff (head up +15°, lean 10, 0.6 s). End beat: the hunter gets the first piece and holds it up a moment (x 140, 0.6 s); a small `Cheer` |
| Props | the spit: two forked sticks 0.12 u and a bar 0.22 u (N, the same as B13.c's); a rabbit on it (a brown oval 0.05 u with four leg stubs, N) or a deer haunch 0.07 u (N); a piece 0.02 u (N) |
| On a phone | the spit across the fire is a strong shape; a turn every 4 s and a flicker on each drip read as roasting. The piece is ~5 px: the pose carries it |
| Sound · cheat | none · **"Roast on the spit (rabbit / deer)"** |
| Reuse | R B9.b's round, R B7.b, R `Flare`. N the props, the set-up, the turning sitter, the browning |
| Note | on a spit night the pot stays cold (B30.e) [est] |

#### B30. The stew pot (look signed off 2026-11-15, #676; these are its uses)

Today the pot has its look (soot-dark, the stew by kind, steam, bubbles) and the cook stirs and tastes (B9.d). What's missing
(feedback#78 on #676): someone filling it, bowls ladled at supper, and **cold and off the fire when no meal is on**, so an idle pot
doesn't read as a mistake. **The pot's day [est]:** cold beside the fire in the morning → filled mid-afternoon → lifted onto the
fire → simmers and is stirred (B9.d, R) → ladled at supper → lifted off, cold for the night. On a "a basket each" supper (the small
choice) or a spit night it stays cold all day.

##### B30.a Fill the pot (work)
| | |
|---|---|
| Who · where · when | the cook and one helper (a water carrier), mid-afternoon (view 15–16 h), once on each pot day |
| Beats | **Water**: the helper comes with a full `Bucket` (B4's heavy walk), lifts it (both arms x 100, lean −5, `Back` 0.4 s) and tips it in (the bucket rolls 100° over 0.6 s) → `hit`: 4 `Droplets` at the rim, a water-coloured disc rises inside to 60 %. **Food**: the cook brings a `Basket` or `FishBasket` from the stash and tips it in (B5's tip: 3–5 bits arc in, 0.06 s apart); the disc turns to the stew's colour and the bits show (#676's look by kind). **Onto the fire**: cook and helper crouch either side (arms x 60), lift together (`Back` 0.5 s, the pot rises 0.06 u), side-step 0.15 u and set it on the fire stones (`Squash` 0.2 s, `Jolt`, a puff of `Sparks` from under). The simmer starts |
| Props | `Bucket`, `Basket`, `FishBasket` (R); the pot (R) gets a second spot beside the fire |
| On a phone | the pot is ~0.09 u (22 px); a bucket tipped over it and the two-person lift onto the fire read |
| Sound · cheat | none · **"Pot: fill it now"** |
| Reuse | R B4, R B5 `TipBasket`, R+ B27's two-person carry (`Lead` / `Side`). N the pot's two spots and the lift |

##### B30.b Ladle the bowls at supper (work)
| | |
|---|---|
| Who · where · when | B9.b's bowl-giver, every supper at the fire |
| Beats | before each hand-over the giver stands at the pot: the ladle in (arm x 60 → 30, lean 25, 0.3 s) → `hit`: a `Steam` blob, and the bowl in the left hand shows stew (a dot in the stew's colour, `Pop`); then the walk to the eater (B9.b). The ladle stays in the pot between dips |
| Props | a ladle, dark wood, 0.09 u (N); `Bowl` (R) |
| On a phone | a dip into the steaming pot, then a walk with a bowl |
| Sound · cheat | none · "Meal now" (exists) |
| Reuse | R B9.b, R `Bowl`. N the ladle and the dip. One prop per doll (Hard in code 8): the ladle belongs to the pot |

##### B30.c Kids peek in (life)
| | |
|---|---|
| Who · where · when | 1–2 kids while it simmers at dusk. **Sometimes** |
| Beats | the kid comes up behind the cook, stands on tiptoe (offset +0.03 u, squash 1.08, 0.3 s), leans over the rim (lean 20, head pitch −25) for 0.8 s; a `Steam` puff in the face → `Flinch` (back 0.04 u, 0.2 s), wipes the face (arms x 140 in turn, 0.5 s), sniffs (head up +15°, two bobs) and grins. If the cook is there she shoos with the stirring stick (arm z ∓60, two flaps, 0.5 s) and the kid runs off laughing. End beat: from 0.4 u the kid looks back and hops |
| Props | none |
| On a phone | a small doll on tiptoe over the pot, the jump back from the steam |
| Sound · cheat | none · **"Kids peek in the pot"** |
| Reuse | R `Flinch`, `Steam`, B17.b's shoo. N tiptoe-and-peek over a rim |

##### B30.d Someone sniffs and nods (life)
| | |
|---|---|
| Who · where · when | a grown-up passing the simmering pot within 0.3 u on their way (no detour). **Often** at dusk, at most one per 20 s |
| Beats | slows (0.2 s), turns to the pot (0.3 s), leans 15 toward it, sniffs (head up +10° twice, 0.15 s each, bob 0.01 u), a slow nod (lean 4 → 14 → 4, 0.4 s), pats the belly (arm x 40, z ∓20, two pats in 0.4 s), walks on. If the cook is there she nods back |
| Props | none |
| On a phone | the stop and the belly pat: "smells good" |
| Sound · cheat | none · **"Someone sniffs the pot"** |
| Reuse | R B7.a's nod. N the sniff (a short head beat for dolls) |

##### B30.e Cold and off the fire (state)
| | |
|---|---|
| When | no meal on: morning to mid-afternoon, after supper, a "basket each" day, a spit night |
| Look | the pot stands on three stones 0.15 u beside the fire ring, not on it; no steam, no bubbles; inside dark and empty (a flat dark disc 0.01 u under the rim); the ladle leans against it |
| Beats | after supper the cook scrapes it out (arm x 70, three circles in 1.5 s), and with a helper lifts it off the fire (B30.a's lift, backwards); a kid stacks the bowls beside it (B7.b, then `Squash` on the stack) |
| On a phone | no steam and set to one side: "not cooking now" |
| Sound · cheat | none · **"Pot: cold / cooking"** (toggle) |
| Reuse | R `MealDirector.DrawPot` (its `show` flag and the steam), R+ a second position. N the scrape |

#### B31. The feather pole (#566, with #691)

Today: one pole at the camp's edge; one or two prayer visits a day (`Life/LifePlanner.Prayer.cs`, `DollStyle.Pray`, clip
`566-feather-pole`); cheat "Feather pole: pray now". feedback#79 on #566: a player didn't know what the pole is, so the visits
must be seen in a normal visit.

##### B31.a A new feather after a good event
| | |
|---|---|
| Who · where · when | the chief (else the eldest) with the people the event is for: the couple after a marriage, the parent with a newborn, everyone near when the village is founded (the era moment). The next dusk after the event. **Rare** (a few a game year) |
| Beats | they walk to the pole (a couple walks together, B7.c); the chief holds the new feather up (`HoldUp`, x 160, 1.2 s) and it catches the light (a white flash star, 0.1 s); ties it on: both arms at the pole x 95, a fiddle (±10° at 4 Hz, 1.5 s) → `hit`: the feather `Pop`s on, the pole `Jolt`s, the old feathers flutter (`Sway` 8°, 6 Hz, τ 0.3 s). All of them `Pray` for 3 s; the person it is for touches it (arm x 110, 0.8 s). End beat: they step back and look up at it (`LookUp`, 1 s); a kid points at the new one |
| Props | a single feather, 0.06 u (N, the pole's feather shape scaled down); the pole drawn by feather count (R+, the pattern of `ConsequenceProps.Charms(count)`): new feathers spiral down from the top, 0.03 u apart, tilted out 30°. Colour by event [est]: white for a newborn, red for a marriage, ochre for the village. Up to 12 drawn; after that a new one recolours the oldest |
| On a phone | a feather ≈ 22 px at the props' 1.5 scale; over the years the pole fills up (the user: "the pole fills up over the years", #692) |
| Sound · cheat | a soft `Chime` at the tie [est] · **"Feather pole: tie a feather (newborn / marriage / village)"** |
| Reuse | R `Pray`, `HoldUp`, `LookUp`, B7.c, `Sway`. N the tie beat; sim: a saved list of the pole's feathers (kind, year) and the three event hooks (lane: sim) |

##### B31.b Kids dare each other to touch it
| | |
|---|---|
| Who · where · when | 2–4 kids playing within 1.5 u of the pole, by day. **Sometimes**; never during a prayer |
| Beats | the kids bunch up 0.4 u from the pole; one is pushed forward (a shove, arms x 80; the darer stumbles 0.05 u); creeps in (B6's creep at kid scale: a 0.04 u step in 0.4 s, still 0.5 s, 4 steps), looking back twice (yaw 150°, 0.3 s); reaches (arm x 100, lean 20), pulls back once (0.3 s), then touches → `hit`: the feathers flutter (`Sway` 8°); the kid squeals (hop 0.05 u, arms up x 160) and runs back; the others hop and laugh, and the next one creeps in. End beat: a grown-up near the pole turns to look (`Turn`) and the kids scatter |
| Props | none |
| On a phone | one small doll creeping toward the pole, the others bunched behind, then the run back |
| Sound · cheat | none · **"Kids dare at the feather pole"** |
| Reuse | R B6's creep (`HuntStalk`), `Laugh`, `Turn`, `Run`. N the dare group (roles `Darer` / `Egger`) |

##### B31.c Prayer visits you see in a normal visit (rate only)
No new animation. Raise the rate [est]: the midday visit every day (55 % today), the dusk one as now (80 %), so about 1.8 visits a
view day; and B11 may start one as a cheap story when the pole is in view and none has played for 3 real minutes.

---

### #692 part 2: the Keeper's tree visits

Today the Keeper's tree stands in its ring of stones (`View/KeeperTreeKit.cs`), carries the charms (#635), glows at night and is
never cut (decisions.md 2026-11-14 "Wood comes from the forest"). Nobody visits it. **The visit spot** is the stone ring's edge
plus 0.08 u, on the camera's side of the trunk where there is room, so the visit is seen.

##### B32.a Sit under it
| | |
|---|---|
| Who · where · when | 1–2 grown-ups on a midday `SitOut` or `Nap`, once the tree has a crown (stage 3 or more). **Sometimes**, more in summer |
| Beats | walk to the trunk, turn, back to it 0.06 u off, slide down to sit (squash 1 → 0.7, 0.6 s `Smooth`), lean back −10 on the bark. Idle: the head droops twice in 6 s (pitch −20 over 1 s, snaps up in 0.15 s); one leaf falls on them (`Leaves` 1) and they brush it off (arm x 120, z ∓40, 0.3 s). End beat: stand, `Stretch` (1 s), one pat on the trunk (arm x 90 → `hit`, crown `Sway` 3°) |
| On a phone | a doll sitting against the tree, nodding off |
| Sound · cheat | none · **"Keeper's tree: sit under it"** |
| Reuse | R `Sit`, `Nap`, `Stretch`, `Leaves`, the crown's sway. N the back-to-the-trunk sit |

##### B32.b Touch the bark in passing
| | |
|---|---|
| Who · where · when | anyone walking within 0.4 u of the trunk on their way (no detour over 0.3 u). **Often**, at most one per 30 s |
| Beats | a grown-up slows to 0.6×, the near arm out (z 60 → x 80, 0.3 s), the hand trailing along the bark for 0.6 s, a small nod (lean 8, 0.3 s), on. A kid slaps it instead (arm x 100, `Snap` 0.1 s → `hit`, the leaves shiver 2°) and runs on |
| On a phone | a hand out to the tree while walking |
| Sound · cheat | none · **"Keeper's tree: touch the bark"** |
| Reuse | R walk. N the trailing hand over a walk |

##### B32.c Kids circle it
| | |
|---|---|
| Who · where · when | 3–5 kids at play, tree stage 2 or more, by day. **Sometimes** |
| Beats | B2.e's ring round the trunk (r = the stone ring + 0.1 u), hands joined (arms z ∓70), skipping (hop 0.02 u at 2.5 Hz, 0.3 u/s), one round each way; then all run in and touch the trunk together (`hit`, crown `Sway` 3°) and fall back on their bottoms (squash 0.7 → sit, 0.3 s), laughing (1 s) |
| On a phone | a ring of small dolls round the tree |
| Sound · cheat | none · **"Kids circle the Keeper's tree"** |
| Reuse | R+ B2.e `RingGroup` (the centre is the tree), R `Laugh` |

##### B32.d The quiet visit after a death (the user's favourite)
| | |
|---|---|
| Who · where · when | the one closest to the dead (partner, else a grown kid, else a friend), alone. The first dusk after the death; once more at the first dusk of the next spring [est]. **A held moment** [est]: it waits up to two dusks for the Keeper to be watching, else plays unseen. Never two at once |
| Beats (~25 s) | **1 Walk** (4–6 s): from the fire, alone, 0.7× speed, head down (pitch −15), arms limp. The people at the fire turn and watch them go (`Turn`, 1.5 s); nobody follows. **2 Arrive**: at the ring's edge, look up at the crown (`LookUp`, 2 s). **3 Touch**: a step in, a hand flat on the bark (arm x 90, lean 10), held 3 s; the tree's dusk glow comes up a little (+0.1 for 4 s, `KeeperTreeKit`'s glow). **4 Sit**: B32.a's slide down against the trunk, arms in (x 40), head down, held 6–8 s; one leaf drifts down slowly (2 s) and stays on the shoulder. **5** If the box chose the tree to remember its dead (`remember:tree`): they stand, reach up on tiptoe (x 170, +0.02 u) and tie this death's charm (fiddle 1.2 s → `hit`: the charm `Pop`s onto the bough, #635's count + 1). Otherwise they rest the forehead on the bark (lean 25, 2 s). **6 End beat**: a long look up (2 s), then the walk back at normal speed, head level. Someone at the fire stands and walks out 0.3 u to meet them: a hug (B7.d) |
| On a phone | slow and still: one doll walking away from the fire at dusk to the glowing tree and sitting there alone. The leaf on the shoulder (5 px) is a bonus, not the read |
| Sound · cheat | the ambience dips 30 % for the visit [est]; no music · **"Keeper's tree: quiet visit (after a death)"** |
| Reuse | R walk, `LookUp`, `Sit`, `Turn`, B7.d, the charms (#635), the glow. N the visit plan (`LifePlanner.TreeVisits`, a held moment), the hand-on-bark and forehead poses |

##### B32.e The newlyweds walk to the tree
| | |
|---|---|
| Who · where · when | the couple, the dusk of their wedding after C3.a (or the next morning if it ended after dark). Every wedding |
| Beats | walk together hand in hand (B7.c); stand side by side facing the tree (2 s); both `LookUp`; each lays a hand on the bark at the same moment (arms x 90, 1.5 s); they turn to each other and hop together (0.03 u, the wedding's join); walk back as they came. (The marriage feather, B31.a, is the next dusk) |
| On a phone | two dolls hand in hand at the tree |
| Sound · cheat | none · **"Keeper's tree: newlyweds' visit"** |
| Reuse | R B7.c, `LookUp`. N only the plan |

##### B32.f A parent brings the newborn
| | |
|---|---|
| Who · where · when | a parent with the baby (the bundle at least 0.06 u, as C3.b), a morning in the first view days after a birth. At most one a game season [est] |
| Beats | walk with the bundle in the arms (x 60); stop at the ring; lift it toward the crown (both arms x 150, `Back` 1.5 s), a slow sway ±4°; a petal or a leaf drifts down past it (`Leaves` 1, blossom colour at stage 3–4): the tree's answer; cradle again (x 60). End beat: a small `Bow`, walk back |
| On a phone | the bundle held up under the tree; an echo of the newborn at the glass (C3.b) |
| Sound · cheat | none · **"Keeper's tree: newborn visit"** |
| Reuse | R C3.b's bundle and lift, `Leaves`, `Bow` |

##### B32.g The old one's afternoons there
| | |
|---|---|
| Who · where · when | the oldest person in the box once they are old (the age the old-age rule uses [est]), each afternoon (view 14–16 h) while they live. The last days can't be known ahead, so this is "while they are old" |
| Beats | a slow, bent walk (0.6×, lean 12); B32.a's sit against the trunk; dozing on and off. Now and then a kid brings a flower (B33.c) and hands it over (B7.b), sits beside them 4 s; the old one pats the kid's head (arm x 110). End beat: at dusk their grown kid (or another grown-up) takes both hands (arms x 80), pulls them up (`Back` 0.5 s) and walks them home slowly (B7.c). After they die, B32.d plays for their closest one |
| On a phone | the same old doll under the tree every afternoon: the player learns the habit |
| Sound · cheat | none · **"Keeper's tree: the old one sits there"** |
| Reuse | R B32.a, B7.b, B7.c, B33.c's flower |

##### B32.h The eldest tells the kids who planted it
| | |
|---|---|
| Who · where · when | the eldest grown-up and 2–4 kids, an afternoon, tree stage 3 or more. **Sometimes** (1 day in 4) |
| Beats | the kids sit in a half ring facing the eldest under the tree; the eldest pats the trunk (`hit`, crown `Sway` 2°), then **points up at the glass** (`Point`, arm x 150, toward the camera's side); the kids turn their heads up to the glass one after another (0.12 s apart, `LookUp`); one waves at the glass (`WaveSmall`). The eldest mimes the planting: crouches, pokes a finger into the ground (arm x 40, lean 35, 0.5 s), then rises with both arms slowly up (x 20 → 170 over 2 s, `Smooth`): it grew. The kids copy, arms slowly up, and laugh. End beat: one kid pats the trunk the way the eldest did; they scatter |
| On a phone | the point up at the glass and the kids' heads turning: the Keeper planted it. Closed world: the planter is the Keeper, nothing outside the box |
| Sound · cheat | none · **"Keeper's tree: the eldest tells the kids"** |
| Reuse | R `Point`, `LookUp`, `WaveSmall`, `Laugh`. N the planting mime |

---

### #692 part 3: grass, saplings, rocks, fallen branches

#### B33. Grass and tall grass

##### B33.a Roll in the grass
| | |
|---|---|
| Who · where · when | 1–3 kids at play on grass or tall grass, spring and summer. **Sometimes** (on a slope: B49.b) |
| Beats | the kid flops down (squash 0.7, 0.2 s, into `Lie`), rolls sideways three times (roll 0 → 360° in 0.4 s each, moving 0.05 u a roll), 4 `Grass` blades at each turn; stops on the back, arms out (z ∓90), giggles (bob 0.01 u at 9 Hz, 0.8 s); sits up with two green bits on the head (0.008 u dots, gone at the next meal). Tall grass flattens where they rolled for 3 s |
| On a phone | a small doll turning over and over, grass flying |
| Sound · cheat | none · **"Kids roll in the grass"** |
| Reuse | R `Lie`, `Grass` puff, R `GrassHide` flattening. N the roll |

##### B33.b Lie back and look up
| | |
|---|---|
| Who · where · when | 1–2 people (a kid with a parent, or a couple) on grass at midday in summer. **Sometimes** |
| Beats | sit, lie back (`Lie`, 0.6 s), hands behind the head (arms x 170, z ∓40), still 6–10 s; now and then one points straight up (1 s) and the other turns the head to look (0.3 s); a soft laugh together (bob). End beat: one sits up, stretches, holds out a hand; they go. They look up at the light over the box; nothing outside it is named |
| On a phone | two dolls lying side by side, an arm pointing up |
| Sound · cheat | none · **"Lie back in the grass"** |
| Reuse | R `Lie`, `Point`, `Stretch`. N hands-behind-the-head |

##### B33.c Pick flowers
| | |
|---|---|
| Who · where · when | a kid or a grown-up in tall grass with flowers (12 % of tall tufts carry one, `View/BoxView.Eco.cs:207`), spring and summer. **Sometimes** |
| Beats | walks slowly through the tall grass, bends at a flower (lean 35, 0.3 s), picks it (arm x 40 → 60, `Snap` 0.1 s → `hit`, 2 `Grass` blades) and stands holding it up. 2–3 picks make a little bunch. Then gives it away (B7.b): to a parent, a sweetheart, the old one at the tree (B32.g), or lays it on the remembering stone. The one who gets it sniffs it (B30.d's sniff) and tucks it behind the ear (the bunch moves to the head until night) |
| Props | `Flower`: a stem 0.04 u and a head 0.015 u in a `Palette.Flowers` colour; a bunch of 3 (N, unless the flower in `PairBeats` exists as a mesh: then R) |
| On a phone | a bright dot held up; the hand-over is the read. The baked flower dot stays in the grass [est: accepted] |
| Sound · cheat | none · **"Pick flowers"** |
| Reuse | R B7.b, `Mourn` with a flower. N or R the flower prop, N the pick |

##### B33.d Blow a grass whistle
| | |
|---|---|
| Who · where · when | a kid (sometimes two) on grass, not in winter. **Sometimes** |
| Beats | pulls a blade (bend, pull, `Snap`, 1 `Grass` blade); holds it between the hands at the mouth (both arms x 125, z ∓15); blows: lean −5, the head bobs forward 0.015 u, stretch 1.05, 0.4 s → `hit`: 2 short white lines burst at the hands (a `Steam`-like puff, N tint). Two duds (nothing comes out, a shrug), then it works: nearby kids turn (`Turn`) and cover their ears (arms x 150, z ∓60, 0.8 s), then laugh and all try |
| On a phone | hands at the face, the others' hands at the ears |
| Sound · cheat | a reed squeak: no CC0 one found yet; fallback the `Flute` cue pitched up [est] · **"Grass whistle"** |
| Reuse | R `Turn`, `Laugh`. N the blow and the ears |

#### B34. Saplings, seedlings, young trees (wild ones, from the earned seeds #724; never the Keeper's tree)

##### B34.a Water a sapling (work)
| | |
|---|---|
| Who · where · when | the morning water carrier on a dry day (no mist that day), when a wild seedling or young tree stands within 1.5 u of the camp. **Sometimes** |
| Beats | walks over with the `Bucket` after the dawn run (B4), stops 0.08 u from it, crouches, tips the bucket slowly (roll 70° over 1.2 s): 6 `Droplets` in 1 s aimed at the foot → `hit`: a darker wet patch `Pop`s on the ground (r 0.05 u, fades over a view hour) and the little tree perks up (scale y 0.9 → 1.05 → 1, `Back` 0.4 s). End beat: stands, looks at it, nods |
| On a phone | a bucket tipped over a small tree; the dark patch |
| Sound · cheat | none · **"Water a sapling"** |
| Reuse | R B4, `Droplets`. N the pour. View only: no growth change [est], so the balance holds |

##### B34.b Ring a sapling with stones (work, once per sapling)
| | |
|---|---|
| Who · where · when | a grown-up with a kid helping, the first view day a wild sapling stands within 1.5 u of the camp. Once per sapling, at most 4 rings in the box |
| Beats | the grown-up carries 3 stones in the arms (`Arms` slot), the kid one in the hand; each crouches and sets a stone into a ring r 0.06 u round the sapling (lean 30, arm x 40, 0.4 s → `Squash` on the stone + 1 `Dust`); 6 stones in two trips. End beat: both stand back; the kid hops |
| Props | stones in the arms (3 grey icos, N); the ring stays (R+, a smaller `KeeperTreeKit` stone ring) |
| On a phone | the ring echoes the Keeper's tree: "they look after it" |
| Sound · cheat | none · **"Ring a sapling with stones"** |
| Reuse | R+ the Keeper's ring. N the carry of stones and the set-down |

##### B34.c A child plants a twig beside it
| | |
|---|---|
| Who · where · when | a kid who watched B34.a or B34.b, right after it. **Sometimes** |
| Beats | picks up a `Twig` (bend, lift), crouches 0.06 u from the sapling, pushes it into the ground (both arms x 30, lean 35, down 0.02 u, `Snap` 0.15 s → `hit`, 1 `Dust`; the twig stands), pats the soil round it (arms x 40 in turn, 3 pats), "waters" it from the bucket's last drops (2 `Droplets`), and nods at it. The twig stays for a season (at most 4 in the box). No side-by-side height check (measuring against saplings was dropped) |
| On a phone | a kid crouched by a twig beside a little tree |
| Sound · cheat | none · **"A kid plants a twig"** |
| Reuse | R `Twig`. N the plant beat and the standing twig |

#### B35. Rocks and pebbles

There is no rock to sit on in the camp today (the box has the fire ring's stones, the remembering stone and the Keeper's
outcrops). **New [est]:** two low flat boulders at the camp's edge (0.07 u high, 0.12 u wide, `Palette.Rock` icos), placed by
the founders' camp on free ground, never on a path or a lot. A Keeper's outcrop near the camp counts too.

##### B35.a Sit on a rock
| | |
|---|---|
| Who · where · when | a grown-up resting between work, or two chatting (B7.a), by day. **Often** (it is a seat). Kids: **sometimes** |
| Beats | grown-up: walks up, turns, sits on top (offset +0.06 u, `Sit`). Kid: climbs on (hop 0.07 u, 0.4 s), stands on top, arms up wide (`PoseBig`, 1 s: king of the rock), jumps off (hop, `Bounce` landing, `Dust`) |
| On a phone | a doll sitting higher than the ground; a kid on top, arms up |
| Sound · cheat | none · **"Sit on a rock / kid on the rock"** |
| Reuse | R `Sit`, `PoseBig`, B7.a. N the boulders and the climb |

##### B35.b Stack pebbles
| | |
|---|---|
| Who · where · when | 1–2 kids, or an old one, on a rock or at the pond's bank. **Sometimes** |
| Beats | crouch; pick up a pebble (bend, arm x 40 → `hit`, it `Pop`s into the hand); set it on the stack (arm x 60, slow 0.5 s, a 0.3 s hold: balancing; the stack wobbles 3°, 4 Hz, τ 0.3 s). 1 → 5 pebbles, each smaller (0.02 → 0.012 u). On the fifth: a pause, both hands away slowly (arms out z ∓40, 0.6 s), then `Cheer`. 1 time in 3 it topples: the pebbles bounce and roll off (`BounceRoll`, `Dust`) and they laugh. The stack stays for the view day (at most 3 in the box) |
| Props | pebbles 0.012–0.02 u (N); the stack drawn by count (N) |
| On a phone | a 5-high stack is ~17 px; the slow hands-away is the read |
| Sound · cheat | none · **"Stack pebbles"** |
| Reuse | R `Cheer`, `Laugh`, `Arc.BounceRoll`. N the stack |

#### B36. Fallen branches (the deadfall under wild trees, `PropKind.Branch`)

##### B36.a Stick sword fight
| | |
|---|---|
| Who · where · when | 2 kids near the deadfall under a wild tree. **Sometimes** |
| Beats | each picks up a fallen branch (bend, lift) and snaps it to size over the knee (both hands x 70, `Snap` → `hit`, 2 `Chips`; half drops); en garde (arm x 110 pointing at each other, 0.5 s); then B3.b's duel as built (`PlayDuel`, `PlayBlock`). End beat: both sticks up high (x 170), then dropped back on the deadfall |
| On a phone | two small dolls with sticks crossing; the white flash at each cross |
| Sound · cheat | a light wood knock at each cross (`impactWood_light`, already in the project) [est] · **"Kids sword fight with sticks"** |
| Reuse | R B3.b, R `Twig` / `Branch`. N the snap-to-size and the en garde |

##### B36.b A stick horse
| | |
|---|---|
| Who · where · when | a kid with a long branch, by day. **Sometimes** |
| Beats | the branch held between the legs, front end up at the chest (arm x 60, the branch angled 30° down to the back) [est]; gallop: hop 0.03 u at 3 Hz, lean 10 forward, pitch rocking ±6° in step, round a 1–1.5 u loop in the camp; now and then it rears: stop, lean −15, the branch's front up (arm x 110), 0.4 s, and off again. A toddler toddles after (B2.d). End beat: the kid leans the "horse" against a tent (it stays a view hour) and pats it twice (arm x 90) |
| On a phone | the rocking hop with a stick reads as riding |
| Sound · cheat | none · **"Stick horse"** |
| Reuse | R `Branch`, B2.d. N the ride pose (a new hold for the branch) and the gallop |

---

### #692 part 4: animals

##### B37 Frogs: kids catch one and let it go
| | |
|---|---|
| Who · where · when | 1–2 kids at the bank where a frog sits (`View/PondLife.cs`, 2 by day), spring and summer. **Sometimes** |
| Beats | creep to the frog (B6's creep, 3 steps); pounce: lean 40, both arms down x 60, `Snap` 0.15 s → `hit`, 3 muddy `Droplets`; the frog is in the cupped hands (its transform moves there). The kid stands, holds the hands up to the face (x 130), peeks in (head −20, 0.6 s); the frog wriggles (`Wobble` 15°, 8 Hz); the friend leans in to see. Then the kid crouches at the water and opens the hands (arms apart z ∓50, 0.3 s): the frog hops in (`Flee`, R) with a `Ring`; the kid waves (`WaveSmall`). Miss (1 in 2): the frog hops in first, the kid flops (squash 0.6, 6 `Droplets`), sits up dripping and shakes off (B3.a's end beat); the friend laughs. No croak imitating (dropped) |
| On a phone | the pounce, the cupped hands held up, the frog hopping back into the water |
| Sound · cheat | none · **"Kids catch a frog"** |
| Reuse | R the frog and its `Flee`, R B6's creep, B3.a's shake. N the cupped hands and the frog in them |

##### B38.a Deer: watched grazing from the wood's edge
| | |
|---|---|
| Who · where · when | 1–3 people (a grown-up with kids), when the herd grazes within 1.5 u of the camp's edge, by day. **Sometimes**; never during a deer hunt or the dawn at the pond (B14.a) |
| Beats | they stop at the edge, 0.8–1.2 u from the deer (outside their alert radius), stand still side by side; the grown-up crouches beside a kid (squash 0.8) and points (arm x 90, 1.5 s); they watch 6–10 s, heads following the deer. A doe looks up (`Alert`): everyone freezes (B13's freeze) until she grazes. End beat: two slow steps back, then they turn and go |
| On a phone | a little group standing still, facing the deer, a pointing arm |
| Sound · cheat | none · **"Watch the deer"** |
| Reuse | R P0's `Alert` and `Graze`, B13's freeze, `Point` |

##### B38.b Deer: kids creep up and the deer bolt
| | |
|---|---|
| Who · where · when | 2–3 kids, the same conditions. **Rare** (1 day in 4); not in spring while fawns are small [est]; no hunt within 1 u that day |
| Beats | the kids crouch-creep in a line (B6's creep, kid scale) from 1.2 u; they freeze when a deer lifts its head (B13's freeze); at 0.6 u one kid giggles (laugh bob) or steps on a twig (1 `Chips` at the foot) → `hit`: the stag `Alert`s, then the herd `Bolt`s (white rumps); the kids jump up and run after them 0.3 u, arms out, stop, arms drop, look at each other; one shrugs and they all laugh. End beat: they walk back; a grown-up shakes the head at them (yaw ±20° at 4 Hz, 0.5 s) |
| On a phone | a row of crouched kids, then the deer bounding off |
| Sound · cheat | none · **"Kids creep up on the deer"** |
| Reuse | R B6 creep, B13 freeze, P0 `Alert` / `Bolt`, B6's shrug, `Laugh` |

##### B39.a Rabbits: a kid holds a kit
| | |
|---|---|
| Who · where · when | a kid at the meadow's edge in spring, while the rabbits have young. **Rare** (1 spring day in 3); no hunter within 1 u |
| Beats | the kid crouches very still (3 s); a kit hops up (a planned hop toward the kid); the kid scoops it slowly (both arms x 50 → 70, 0.8 s `Smooth`) and holds it at the chest (x 60), its ears twitching (8 Hz); strokes it (one arm x 70 ↔ 90, 3 strokes in 1.5 s); turns and shows it to another kid (arms out x 80). Sets it down (crouch, arms x 40): it hops back to its mother (2 hops). End beat: the kid waves after it |
| Props | a kit: the rabbit doll at 0.6 scale (N look, R doll) |
| On a phone | a kid holding a small rabbit at the chest |
| Sound · cheat | none · **"A kid holds a rabbit kit"** |
| Reuse | R `RabbitDoll`, `RabbitHerd` hops, `WaveSmall`. N the hold. A held kit is never hunted that day [est] |

##### B39.b Ducks: are they seen enough? (rate check, feedback#62 on #692)
No new animation: the duck uses are B15.a (the egg run, as built in #574: "Gather an egg now"), B2's chase and B15.d (crumbs). The
check: in a camp day with ducks, **at least one duck use is seen per view day, two on most days** [est]; B11 lists "feed the
ducks" (B15.d) as a cheap story when ducks are in view. If B15.d is not built yet, it is built as specced. Test: over 10 view days
with ducks placed, the planned duck uses per day are never 0. Cheat: **"Kids feed the ducks"** (if missing).

##### B39.c Fish: kids peer into the water and try to catch one by hand
| | |
|---|---|
| Who · where · when | 1–2 kids at the bank once fish are placed (B16.e), summer. **Sometimes** |
| Beats | the kid kneels at the edge (B4's kneel), leans far (lean 45), head down, watching the fish shadows (B70.b); a shadow drifts close; the hand goes down slowly (arm x 45 → 80, 1.2 s); a hold; the grab (`Snap` 0.1 s) → `hit`: 6 `Droplets` and a `Ring`, the fish darts (B70's dart); the hand comes up empty and dripping (2 drops); the kid looks at it, shakes it (z ±20 at 5 Hz) and laughs. 1 in 6 a splash in the face (B16.b's miss). They never catch one [est]: fishing is the grown-ups' work and the fish keep their floor |
| On a phone | a kid lying over the edge, the splash, the empty hand |
| Sound · cheat | none · **"Kids try to catch a fish by hand"** |
| Reuse | R B4 kneel, B16.b miss, `Laugh`. N the slow reach and grab |

---

### #692 part 5: the rest of the camp

Shorter specs, one table per thing. Columns: **Who · when · how often** · **Beats** (with timings) · **Props · reuse** · **Cheat**.
The read on a phone is in the beats' last words. None has a sound unless it says so.

#### B40. Tents
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B40.a Kids peek out | kids waking at dawn; **often** (at most once per tent a day) | a kid's head pops out of the door (lean out 0.04 u from inside, `Peek`), looks left and right (yaw ±30°, 1 s), ducks back in (0.2 s), pops out again and runs out. Reads: a head in the doorway | R `Peek`, R the indoor/door switch | **"Kids peek out of the tents"** |
| B40.b A toy or a charm on the tent | a kid or a parent; **rare** (once per tent, at most 2 things a tent) | reaches up to the door pole on tiptoe (arms x 160, +0.02 u), ties it (fiddle 1 s) → `hit`: it `Pop`s on and dangles (`Sway` in the wind); the door flap `Jolt`s; the kid flicks it (arm x 140) so it spins, and watches. Reads: a small coloured thing hanging at the door | N a small wooden figure 0.02 u, a string of beads 0.03 u; R `Sway` | **"Hang a toy on a tent"** |
| B40.c Sitting in the doorway at dusk | a grown-up (a kid may join), dusk (view 17–19); **often** (1–2 tents) | sits at the door, half in (0.03 u into the door), leans back −5, watching the fire; a kid comes and sits against them; an arm round the kid (arm z ∓70). At bedtime they go in. Reads: a doll in the lit doorway | R `Sit`, the door spot | **"Sit in the tent doors (dusk)"** |

#### B41. The stash pile
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B41.a Kids count the berries | 2 kids at the stash after a berry tip, autumn; **sometimes** | crouch at the baskets; one pokes each basket in turn (arm x 70, one poke every 0.5 s, a head bob with each, 5 pokes); at the end both throw their arms up (x 150): so many! and `Cheer`. Reads: pointing at each basket, then arms up | R `Cheer`, the stash's baskets | **"Kids count the stash"** |
| B41.b A snack sneaked and caught | a kid (now and then a grown-up), by day; **sometimes** | sidles up looking left and right (yaw ±40°, twice), reaches into a basket (arm x 60, lean 25), a berry or a strip `Pop`s into the hand, up to the mouth → `hit`: a grown-up at the stash turns (0.2 s) and points; the sneak freezes (`Freeze`, 0.5 s), eats it fast and runs; caught after 0.3 u by the arm (both stop); the grown-up laughs. End beat: a rub on the kid's head (arm x 110, 0.5 s). Reads: the freeze with a hand at the mouth | R `Freeze`, `Point`, `Run`, `Laugh` | **"Snack sneak at the stash"** |

#### B42. The racks (with feedback#58 on #692: people tending the racks, meat as well as fish)
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B42.a Tending the racks (work) | the rack keeper (`RackKeeper`, `Life/LifePlanner.FishChain.cs`) mid-morning; the hide scraper at the hide rack; **often** (once a work morning per rack) | fish rack: walks the bar, touches each piece (arm x 150, 0.3 s each; it sways 5°), turns one over (it flips 180° in 0.3 s), fans the smoke (B16.c, 2 s). Hide rack: scrapes (`ScrapeHide`, R), then tugs a corner taut (arm x 90, back 0.04 u; the hide `Jolt`s). **Meat on the rack**: after a hunt, brown meat strips (0.06 × 0.015 u) hang beside the fish [est: the same rack]. Reads: someone checking each hanging thing | R `HangFish`, `ScrapeHide`, B16.c. N meat strips (view) and a meat count on the rack (lane: sim) | **"Tend the racks now"**, **"Hang meat on the rack"** |
| B42.b Kids duck under the hide rack | kids in a tag or chase that passes the rack; **often** when a game is near | the runner ducks (squash 0.7, lean 25, 0.3 s) under the hides; a hide brushes the head (`Sway` 6°); out the other side with a hop. The scraper shoos (B17.b). Reads: a kid bobbing under the hides | R B2.a runners, R+ route through the rack | **"Kids run under the hide rack"** |
| B42.c Kids count the fish | 2 kids at the fish rack; **sometimes** | as B41.a, each poked fish swings. Reads: pointing along the row | R B41.a | **"Kids count the fish"** |
| B42.d A kid sneaks a fish | a kid at the fish rack; **rare** (1 day in 4), at most one fish a day | on tiptoe (+0.03 u) pulls a fish off the bar (it `Pop`s off), runs; the keeper chases 0.4 u, gives up with hands on hips (arms z 70) and laughs; the kid shares it with another kid behind a tent (both `Taste`). The rack's count drops by one [est]. Reads: a kid running with a fish | R `Taste`, `Run`, `Laugh` | **"A kid sneaks a fish"** |

#### B43. The thicket
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B43.a Hide in it | hide and seek (B3.c); **sometimes** | the thicket's bushes count as cover; the hider crouches 0.08 u inside the edge; the bush sways as they go in (6°) and when they peek (3°): the seeker spots the sway. Reads: a bush that moves | R+ B3.c's cover list, R the bush shake | "Kids play: hide and seek" (exists) |
| B43.b Peek at rabbits from it | 1–2 kids, rabbits within 1 u, by day; **sometimes** | lie on the belly at the edge facing the meadow, part a bush with both arms (x 90, z ∓40; the bush leans 4°, held) and watch 4–6 s; a rabbit sits up: the kids freeze; then slide back out. Reads: two heads at the edge of the bushes | R `Lie`, `RabbitHerd` sit-up | **"Peek at rabbits from the thicket"** |
| B43.c Berry-stained faces | kids after picking or tasting; **often** in berry season | B17.a's purple mouth on any kid who ate 3 berries; at the meal a parent wipes it off with a thumb (arm x 110, two rubs, 0.5 s) and it fades. Reads: purple dots on small faces | R B17.a | "Berry kid now" (B17.a's) |

#### B44. Wild trees (never the Keeper's tree)
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B44.a Climb a low branch | a kid at a grown wild tree; **sometimes** | reaches up (x 170), pulls up (0 → +0.09 u over 0.8 s, a wiggle roll ±10° at 6 Hz), sits on the low branch on the camera's side of the trunk (+0.09 u, 0.05 u out), swinging (roll ±5° at 1 Hz) 6–10 s, waves to a kid below; jumps down (hop, `Bounce`, `Dust`). The crown sways 6° at the climb and the jump. Reads: a doll up in the tree | R `Sit`, `Wave`, the crown shake (Hard in code 1). N the climb | **"Kid climbs a tree"** |
| B44.b Nap in the shade | anyone's midday `Nap`, summer; **often** | B32.a's sit against the trunk at a wild tree | R B32.a | **"Nap under a tree"** |
| B44.c Lean on the trunk | a grown-up chatting near a tree; **often** | during a chat (B7.a) one leans a shoulder on the trunk (roll 12°, 0.05 u off it) | R B7.a. N the lean | "Force chat" near a tree |

#### B45. The pond
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B45.a Skip stones | a kid or a grown-up at the bank; **sometimes**. **Ask:** the user dropped "throwing pebbles in the pond" but #692 keeps "skip stones" | picks a flat stone (bend, `Pop`), stands side-on to the water (squash 0.85), arm back low (x −30, z 60, 0.3 s), whips it forward (`Snap` 0.12 s) → `hit`: the stone skims low (h 0.02) and skips 3 times with a `Ring` each (0.25 s apart, 0.15 / 0.12 / 0.08 u), then sinks with a last ring. 3 skips or more: `Cheer`; a kid's goes plop (one ring, h 0.1) and they laugh. Frogs and ducks within reach flee (P3). Reads: a row of rings across the water | R `Ring`, `Arc`, `Cheer`. N the side-arm throw and the skipping arc | **"Skip stones"** |
| B45.b A leaf boat | a kid at the bank, summer; **sometimes** | folds a big leaf (both hands x 90, fiddle 1 s), crouches, sets it on the water (arm x 40, lean 35 → `hit`, `Ring`) and gives it a push; it drifts out 0.3–0.5 u (0.04 u/s), turning 20°/s; the kid walks along the bank after it; it stops against a lily pad or a duck pecks it [est]. It stays until night. Reads: a green fleck drifting, a kid following it | N the leaf boat 0.03 u; R `Ring`, the lily pads | **"Leaf boat"** |
| B45.c Feet in the water | 1–2 grown-ups on the bank, summer afternoons; **sometimes** | sit on the edge facing the water, sunk 0.02 u at the front, lean back −10; a slow bob 0.008 u at 1.5 Hz and a `Ring` at the front every 2 s (dolls have no legs: the rings are the feet [est]). Reads: someone sitting at the water's edge, rings at their feet | R `Sit`, `Ring` | **"Feet in the water"** |

#### B46. Mist puddles
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B46.a Look at their reflection | a kid after a mist; **sometimes** | kneels over a puddle (B4's kneel), head down −40, still 1.5 s; pulls a face (head roll ±15°, twice) and hops back laughing; a second kid joins, both lean over, heads 0.05 u apart. Reads: two kids bent over a puddle | R B4 kneel, `Laugh` | **"Kids look in a puddle"** |
| B46.b Race to the next one | 2–4 kids right after a mist with 3 or more puddles; **often** then | they run puddle to puddle (0.5 u/s); the first there stomps (`Stomp`, `Droplets` and `Ring`, B1.d), the others pile in; three puddles in a row. Reads: kids running and splashing from puddle to puddle | R `Stomp`, B1.d | "Puddles now", then **"Kids race the puddles"** |

#### B47. Reeds
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B47.a Cut reeds to weave (work) | a weaver on a weaving day; **sometimes** | wades in at the reeds (sunk 0.03 u), grabs a bunch (arm x 80), cuts low (arm x 80 → 40, `Snap` → `hit`; the tuft flattens like `GrassHide`, `Grass` blades in reed colour), carries an armful to the tent and weaves (`SitWeave`). Reads: an armful of pale stalks carried from the water | N a reed bundle (3 stalks 0.12 u, `Arms` slot); R `SitWeave`, R+ `GrassHide` | **"Cut reeds"** |
| B47.b Reed whistles | a kid; **rare** | as B33.d, a reed held like a flute (arms x 110 forward, hands 0.03 u apart). Sound as B33.d | R B33.d | **"Reed whistle"** |
| B47.c Hide in the reeds | hide and seek near the pond; **sometimes** | the reeds join the cover list; the hider crouches at the reeds' edge (sunk 0.02 u); the reeds sway as they go in | R+ B3.c | "Kids play: hide and seek" |

#### B48. The stream bed
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B48.a Follow it | 2–3 kids in a line; **sometimes** | walk along the stream bed's cells, the leader balancing (arms z ∓90, roll ±6° at 1 Hz), hopping across it twice (0.05 u). Reads: a line of kids following the hollow | R walk, `Hop` | **"Kids follow the stream"** |
| B48.b A little dam | 2 kids; **rare**; only when the bed holds water [est] | carry stones (B34.b's carry), set 5 across (`Squash`, `Droplets`), pat mud on (arms x 40, brown drops); a small pool shows behind it (r 0.06 u, a view day, view only); then one pushes a stone out: a gush (6 `Droplets`) and they cheer. Reads: kids crouched in the stream, a little pool | R B34.b, `Droplets`, `Cheer`. N the pool | **"Kids build a dam"** |

#### B49. The hill
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B49.a Sit on top at dusk | 1–3 grown-ups (a couple, friends), dusk (view 17–18) in summer; **often** with a hill | walk up, sit on the top facing the camp and the fire; chat quietly (B7.a); stay till the lamps come on. Reads: small dolls sitting on the skyline of the hill | R `Sit`, B7.a | **"Sit on the hill at dusk"** |
| B49.b Roll down it | kids at the top, camp or village; **sometimes** | lie down and roll down (B33.a's roll, faster: 0.25 s a roll, 6–8 rolls down the slope), `Grass` blades; at the bottom stand up dizzy (roll ±12° at 3 Hz, fading over 1.2 s), sit down hard, laugh, run back up. Reads: a doll tumbling down a slope | R B33.a, `Laugh` | **"Kids roll down the hill"** |

#### B50. Paths
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B50.a Hop and race | 2–3 kids; **sometimes** | race from one end of a path to the other (0.55 u/s); the winner jumps with arms up; or hop along it (0.03 u at 2.5 Hz). Reads: kids running along the worn line | R `Run`, `Cheer` | **"Kids race on the path"** |
| B50.b Meet and chat | two walkers whose ways cross on a path; **often**, at most one per 20 s in view; never on an urgent carry [est] | they stop and chat 4–6 s (B7.a), then go on | R B7.a | "Force chat" on a path |

#### B51. The remembering stone: a small gift on feast days
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B51 | 1–2 family members of a name on the stone, the morning of a feast (the festival, the harvest ritual, a wedding) and on the anniversary of the death [est: the box has no birthdays]; **rare** | walk with a gift (a flower, a small `Bowl` of berries, a pebble), kneel at the stone (B4's kneel), set it at the foot (arm x 40, lean 35 → `Pop` on the ground), touch the stone (arm x 90, 1 s), `Bow`, walk back. The gift stays a view day (at most 4). Reads: someone kneeling at the stone, a bright dot left at its foot | R `Mourn`'s flower, B4 kneel, `Bow` | **"A gift at the remembering stone"** |

#### B52. Lanterns at the tents
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B52.a Light each lantern at dusk (work) | the lamplighter (`LightLanterns`, today only at the gathering place); **often** (every dusk) | walks tent to tent with a burning stick (B29.b's ember tip on a `Stick`), reaches up to the lantern on each door pole (arm x 150) → `hit`: it lights (`NightLamps` per lamp) with one spark star; 1.5 s apart plus the walk. Reads: the tents' lights coming on one after another | N a small lantern on each tent's pole (0.03 u, or the `Lantern` piece scaled, R+); R `NightLamps` | **"Light the lanterns now"** |
| B52.b Kids chase moths | 1–2 kids at a lit lantern, summer, the first night hour (view 19–20); **sometimes** | 2–3 pale specks circle the lantern (r 0.04 u at 2 Hz); the kid jumps at them (hop 0.05 u, arms up x 170) and claps (both arms meet, `Snap` → `hit`); the specks scatter and come back. Reads: a kid jumping under a light [est: specks 0.016 u, pale yellow, or they vanish on a phone] | N a moth puff kind | **"Kids chase moths"** |

#### B53. Weeds and the forest edge (the vine folded in, #685)
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B53.a Pull a weed in the camp (work) | a grown-up; **sometimes**; only a seedling that crept onto a tent's door cell, the fire ring or a path in the camp [est]. Round the Keeper's tree and at the forest's edge the weeding stays the Keeper's (the tweezers, keepers-hands §5.5) | bend, grab (arm x 40), tug twice (lean 30 → 20 → 35, 0.2 s each), pull (lean → −10, `Back` 0.4 s → `hit`, 3 `Dust`; the seedling comes up with a dangling root, `Pop`), toss it to the edge (`Arc` h 0.08). The cell clears in the sim, at most one a view day [est]. Reads: a tug-of-war with a plant | N a weed-with-root prop; R+ the sim's pluck (`EcoModel.Overgrowth`) by the people | **"Pull a weed in the camp"** |
| B53.b Kids trip | a running kid crossing creeping seedlings at the forest's edge; **rare** | the tumble from B2.c (decided 2026-11-06: about 1 chase in 4), `Grass` puff, up laughing | R the B2.c tumble | "Kids chase rabbits: a kid tumbles" |
| B53.c A crown | a kid, summer; **sometimes** | twists weeds and flowers into a ring (both hands x 90, fiddle 1.5 s) and puts it on another kid's head (arm x 150); the crowned kid walks proudly (lean −8, chin up +10°). The crown stays until night. Reads: a green ring with bright dots on a small head | N a crown prop (green ring 0.05 u, 3 flower dots, `Head` slot) | **"Kids make a crown"** |
| B53.d Swing | a rope swing on a wild tree (Q3, the user 2026-11-15): its own spec below, **B53.d** | | | |


##### B53.d A rope swing on a wild tree (Q3: the user, 2026-11-15, "a rope swing on a wild tree"; **spec waits for sign-off**)
| | |
|---|---|
| Who · where · when | **The swing**: one per box, hung once the camp has a grown wild tree (stage 4) within 1.5 u of the fire with a low bough on the camera's side; never the Keeper's tree. **Hanging it** (work, once): a grown-up on a free afternoon (a `SitOut` borrowed, D rules), the first view day such a tree stands. **Swinging** (life): 1–2 kids at play, by day, not in rain, snow or winter [est]. **Sometimes** (about 1 view day in 3); B11 never twice within 30 s |
| Beats: hanging it (~8 s) | the grown-up walks to the tree with a coiled rope over the shoulder (`Arms` slot) and a plank under the arm; throws the rope over the bough: arm back x −30, up x 160 (`Snap` 0.15 s) → the rope's end arcs over (`Arc`, h 0.12) and drops on the far side (`Dust` 1); a second throw if the first misses (1 in 2 [est]); ties the two ends to the plank, crouched (both arms x 60, a fiddle ±10° at 4 Hz, 1.5 s); hangs on it once with both hands to test it (lean −15, squash 0.9, the bough dips 3°, 0.5 s) → `hit`: the crown `Sway` 3°. End beat: a step back and a nod at it (lean 8, 0.3 s) |
| Beats: swinging (a kid, 6–10 s) | walks to the plank, sits on it (`Sit`, offset +0.05 u, 0.4 s); both hands on the ropes (arms x 160, z ∓12); pushes off: lean back −20, the swing's arc ±25° at 0.5 Hz [est], the kid's legs-out kick at each front end (squash 1.05, lean −25, 0.15 s); a second kid pushes from behind (both arms x 90 → `Snap` forward at each back end, a step 0.03 u), then they swap. At the top of the last swing the rider jumps off forward (hop 0.06 u, arms up x 150, `Bounce` landing, `Dust` 2) and laughs. A lone kid pumps by leaning (±15° in time with the arc). End beat: the empty swing rocks to a stop (the arc decays τ 1.5 s) |
| Props | the swing (N): two ropes 0.16 u long (pale straw, 0.004 u thick) from the bough's underside to a plank 0.07 × 0.025 u, pivoting at the bough; drawn by the tree's view, it stays for good (a swing per box, never on a path cell). The coiled rope and the plank for the hanging (N, 0.03 u each) |
| On a phone | a doll moving back and forth under a tree, the pale ropes, the second kid's push. The rope at 0.004 u is under 1 px: drawn with a minimum width of 2 px [est], else the swing reads as a floating plank |
| Kids stay safe | a grown-up within 1 u while a kid swings [est]; the arc never over water, a path or the fire; the jump-off lands on open ground |
| Sound · cheat | a soft creak of the bough at each end of the arc (CC0, Kenney `impactWood_light` pitched down [est]) · **"Hang the swing"**, **"Kids on the swing"** (hangs it first if none) |
| Reuse | R `Sit`, `Arc`, `Dust`, `Laugh`, the crown shake (Hard in code 1), D rules borrow. N the swing prop and its pivot, the throw over the bough, the arc that carries the doll, the push |
| Tests | the swing only on a grown wild tree within reach (never the Keeper's tree, never over water or a path); one per box; the hanging plays once; over 10 view days the swinging uses only `Play` time and no kid swings without a grown-up within 1 u |
| AnimLab | `Tools/animlab/clips/692-b53d-hang-swing.json`, `692-b53d-swing.json` |

##### #692 part 5 notes (the user, 2026-11-16; decisions row 157): the nine clips sent back [est] where marked
- **No lantern poles in the camp.** B40.c (the doorway sit) and B52.a (the lantern round) are **houses-only** (`CampGeo5.HousesOnly`): never
  in the tent phase, not even by the cheat (the orchestrator's call for B52.a, flagged to the user). B52.b: the moths circle the fire's light
  (0.24 u out, to the side, 0.15 u up, specks 0.024 u) and a kid claps at them.
- **B40.b:** the toy or beads hang on the tent's canvas facing the camera, 0.2 u up; the kid ties them standing at the canvas.
- **B43.b:** the kids no longer lie on the belly (it read as falling): they creep up, crouch at the bush's two sides (lean 22), part the
  branches with one arm, freeze, creep back and giggle.
- **B44.a:** the seat is at least 0.18 u up (0.23 x the crown scale − 0.04, to 0.26), out along a bark bough 1.3 x the crown radius to the
  side as the camera sees it.
- **B44.c:** the leaner's back on the bark (lean back 12), the body's middle 0.058 u off the trunk's real face at the shoulder.
- **B46.a:** at the drawn puddle's rim (1.6 x its sim radius + 0.05), seen from the side, crouched and bent over, never flat.
- **B51:** the giver kneels outside the stone's drawn footprint (its renderers' bounds + 0.07).
- **B53.d:** a bark branch from the trunk out over the swing, the ropes tied under it; the swing follows the ride under way from the first push.

##### #692 part 5 retakes (2026-11-16): what changed so the clips read at phone scale (all [est], for the user's sign-off)
- **B40, B52, the tents' side:** a tent's uses (the peek, the toy, the doorway sit) and its lantern pole play on the side the home camera
  sees (yaw 28°): the door when it faces the camera within 60°, else the hem on the tent's wall turned just far enough toward the camera
  (`CampGeo5.ShownSide`). A doorway sit there watches the fire when it is in front, else out over the camp. Toys and beads hang on the
  pole's sides under the lantern (they hung inside it), 1.4 x.
- **B44.a:** the seat is 0.1 u under the crown (0.08–0.16 u, `ClimbSeatFor`), in box units after the kid's scale (+0.09 doll units read as
  barely off the ground); a short bark bough (0.14 u, out past the climber's side, a leafy tip) shows under the climber while the climb plays; the friend waves from beside
  the tree, not in front of it.
- **B44.b:** asleep against this tree's bark (the wild trunk's radius): leaning back −18, slumping forward and to the side (roll 14) every
  7 s, a slow breath, the arms folded in the lap (`NapPose`).
- **B44.c:** the leaner stands beside the trunk as the camera sees it and the other out to the side; the shoulder lean is 16°.
- **B45:** people stand at the drawn water's edge (0.035 u short of it), not at the bank cell's middle (up to 0.2 u up the slope).
  Reshoot2: the camp's water rings were drawn 0.06 x their scale under the given point (no ground given), under the raised pond: none ever
  showed. They now sit on the pond's surface at the spec's sizes.
- **B46.b:** each kid keeps a lane 0.065 u apart, side by side across the run.
- **B47.a:** the armful is seven green and straw stalks with brown cattail heads; one reed is green.
- **B53.a:** the weed in the hand is drawn 2 x. **B53.c:** the worn crown sits round the head (1.2 x the head's width, on its upper half).
- **B53.d:** dark rope (not straw on sand), 0.006 u wide (cap 0.01 u), up into the crown's underside; the plank 0.08 x 0.03 x 0.01 u,
  darker wood, lying across the swing; the rider sits on it (the sit no longer sinks through it).
  The swing hangs 0.14–0.2 u out from the trunk under the crown's edge (0.07–0.12 hid it on the bark).
- **B52:** the lamplighter and the moth kid stand beside the pole across the camera's view, not in front of the lantern; the dusk clips
  are shot with the first run's day hold off (real dusk).
- **B40.b, B40.c:** the tier stands beside the pole (not in front of it); the kid sits beside the grown-up across the camera's view.
- **B47.a:** the camera keeps with the weaver's armful to the tent.
- **B42:** the fish rack stays once it has stood (it popped out of view while the last fish went to the pile).

---

### #693: the choice props, a work use and a life use each

#693 asks for the **use pairs first**, for the user to sign off, then the build. The props and their stories are from
`Sim/DilemmaDefs.cs`, `Sim/SimCore.SmallChoices.cs` and `View/ConsequenceProps.cs` (what each looks like today). Each use below is
**sometimes** unless it says so, and plays only at the prop's own spot (`PropAnchor`).

#### B54.0 The pairs, for sign-off

| Prop (the choice that leaves it) | Work use | Life use |
|---|---|---|
| Feather pole (stories, small choices; one pole, #566) | tie a new feather after a good event (B31.a) | pray (built) · kids dare to touch it (B31.b) |
| Stack of berry baskets ("baskets first" in winter; "a basket each" supper) | gatherers take a basket from the stack in the morning and bring it back full | kids stack the baskets into a tower and it topples |
| Woodpile under a roof ("the woodpile" in winter) | logs stacked under the roof after chopping; fetched for the fire on wet days | when rain starts, 2–3 people squeeze under its roof; kids sit on the logs |
| Name stone ("a stone" to remember the dead) | a new name is cut when someone dies | kids trace the names with a finger and polish it with their sleeves (the callback line says so) |
| Charms on the Keeper's tree ("the tree" to remember) | tie the new charm (B32.d step 5) | kids flick the charms so they spin; passers touch one |
| Stew pot on stones ("one big pot" supper; the travellers' stew) | on "one big pot" nights the supper is cooked and ladled there (B30's beats at this pot) | when it's cold, kids drum on it with sticks |
| Axe in a stump (the travellers sent to camp apart) | wood is split on it | kids sit on it (the issue's example) |
| Headman's staff on a flat stone (the headman vote) | the headman takes it up for the walk round and the meeting, and puts it back | kids play headman with it while it lies there |
| Sacks of flour by the mill (young ones to the miller) | sacks carried from the mill to the homes and the bakery | a flour fight (the callback line says there was one) |
| Carved bird on a ridge pole (young ones to the roofers) | a roofer straightens and rubs it after a storm | kids flap their arms under it, flying |
| Long table with benches (a harvest feast) | the shared meal at it (the issue's example) | kids crawl under it during the feast; an old one naps on the bench after |
| Offering basket on a flat stone (an offering for the Keeper) | filled with the best food at feasts (the issue's example) | kids sit by it and watch the glass, waiting to see if the Keeper takes it |
| Little goat pen, gnawed at one corner (the pond family moved in with a goat) | the gnawed rail is mended | kids sit on the rails; one crawls through the gnawed gap |
| Crate of fish on the shore (the pond family sends fish) | the fish are moved from the crate to the rack | kids sit on the crate and wave across the pond to the family's shore |
| Peg with a name tag at a home (naming a baby) | each spring the parent ties the tag on with a fresh cord | the named child taps their own peg every morning on the way out |

No goat is drawn in the pen today. **Ask:** a goat in the pen would be a new animal kind and must take its share of
`AnimalBudget` (its comment: "never add a kind on top"); the pairs above work without one [est].
**Answered (Q7, 2026-11-15; the pen back 2026-11-16, "Bring a pen back"):** one goat in the pen, outside the animal budget, never
hunted, living only in the pen (sim: `SimCore.PenGoat`). The pen stands by the fire beside the pond family's boat.

#### B54.a–o The beats (short, after the pairs are signed off)

| # | Use | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|---|
| B54.a W | Baskets: take and bring back | gatherers at dawn (berry season); **often** | the gatherer lifts the top basket off the stack (arm x 80, the stack `Jolt`s) and leaves with it (`Basket`, R); at the end of the pick brings it back full, sets it on the stack (`Squash`), the berry dots show on top | R `Basket`, B5.a | **"Baskets: take and bring back"** |
| ~~B54.b L~~ | ~~Baskets: the tower~~ (removed 2026-11-16, the user's #693 notes) | 2 kids; **sometimes** | stack the baskets upside down, one on the other (arms x 120, then x 150 on tiptoe for the third); the tower wobbles (3°, 4 Hz) and falls (the baskets `BounceRoll`, `Dust`); the kids laugh and put them back side by side. Reads: a little tower that tips over | R+ the baskets mesh as 3 pieces | **"Kids stack the baskets"** |
| B54.c W | Woodpile: stack and fetch | the woodcutter after chopping; a fire-feeder on wet days; **often** | carries a `Log` and slides it onto the pile under the roof (arm x 60, push 0.04 u, `Jolt`); on a wet day someone takes one off and carries it to the fire | R `Log`, `StackWood` | **"Woodpile: stack / fetch"** |
| B54.d L | Woodpile: shelter from the rain | 2–3 people near it when rain starts; kids at play on dry days | in rain they hurry under the roof (`Run`) and stand pressed together (0.06 u apart), looking out at the rain, shoulders up (squash 0.95); on dry days kids sit on the low logs. Reads: people huddled under a little roof | R `Run`, `Sit` | **"Rain at the woodpile"** |
| B54.e W | Name stone: cut a new name | the toolmaker, the morning after a death in a box that chose the stone; **rare** | kneels, taps with a stone in the hand (`Hammer`'s stroke at small size, x 70 ↔ 100 at 2 Hz) → `hit` each tap: 1 `Chips` (grey); 6 s; a new row of name marks shows. Reads: someone tapping at the stone | R `Hammer`, `Chips`, the stone's rows by count (R+) | **"Name stone: cut a name"** |
| B54.f L | Name stone: trace and polish | 1–2 kids; **sometimes** | a kid runs a finger along a row (arm x 90, the hand moves 0.06 u sideways over 1 s, twice), then rubs the stone with the sleeve (arm x 100, circles at 2 Hz, 1.5 s); a grown-up nods. Reads: a kid's hand moving over the names | R B7.a's nod. N the trace | **"Kids trace the names"** |
| B54.g L | Charms: flick them | a kid passing under the tree; **sometimes** | jumps (hop 0.06 u, arm x 170) and flicks a charm → `hit`: it spins (yaw 720° easing out over 1.5 s) and the others sway; the kid watches it spin. Reads: a jump at the bough, a spinning charm | R the charms (#635). N the spin | **"Kids flick the charms"** |
| B54.h W | Big pot: the together supper | on a "one big pot" night; **rare** | B30.a–e run at this pot instead of the camp pot; everyone sits round it | R B30 | "Meal now" on a big-pot night |
| B54.i L | Big pot: drumming | 1–2 kids when it's cold; **sometimes** | sit at it with two `Twig`s, drum (arms x 90 ↔ 60 at 4 Hz in turn) → `hit` each beat: the pot `Jolt`s; a grown-up claps along or puts a hand over it (arm x 80) to stop them. Reads: sticks going on the pot | R `Twig`, `Jolt` | **"Kids drum on the pot"** |
| B54.j W | Axe stump: split wood | the woodcutter; **often** where the stump stands | sets a short log on the stump (arm x 60), pulls the axe out (lean −10, `Back` 0.3 s), splits (`Chop`, contact → 2 halves fall either side, `Chips`), sets the axe back in (`Jolt`). Reads: wood split on a stump | R `Chop`, `Chips`, `Log` | **"Split wood at the stump"** |
| B54.k L | Axe stump: kids sit on it | a kid at play, while the axe is in it (never touched) | sits on its edge beside the axe handle, swinging (roll ±5° at 1 Hz); a friend sits on the other edge | R `Sit` | **"Kid sits on the stump"** |
| B54.l W | Staff: the headman takes it up | the headman at the start of the walk round (`WalkRound`) and the meeting; **often** | bends, takes it from the stone (arm x 60 → 100), walks with it (`Hand`, planted at each step: arm x 30 ↔ 40), leans on it at the meeting; puts it back after | N the staff as a hand prop (the prop's mesh, R) | **"Headman takes the staff"** |
| B54.m L | Staff: kids play headman | 2–3 kids while it lies on its stone; **sometimes** | one picks it up and struts (lean −10, chin up, steps with the staff planted); the others `Bow` in a wave; the headman comes, stops, hands on hips (arms z 70) and laughs; the kid puts it back fast. Reads: a kid with a stick and others bowing | R `Bow`, `Laugh` | **"Kids play headman"** |
| B54.n W | Flour: carry the sacks | a villager from the mill to the bakery and homes; **often** in the village | a `Sack` on the shoulder (`Shoulder` slot), the heavy walk (B4's), set down at the door (`Squash`, a small white `Dust`) | R `Sack`, B4 heavy walk | **"Carry flour"** |
| B54.o L | Flour: the flour fight | 3–4 kids at the split sack; **rare** (once a game year) | one scoops a handful (arm x 40 → 120) and throws (`Arc` h 0.06, a white `Dust` burst on the one it hits); everyone joins; white blotches stay on them until the evening wash. Reads: white puffs and kids chasing | R `Arc`, `Dust` (white), B2.a runners. N the flour blotches | **"Flour fight"** |
| B54.p W | Roof bird: straighten it | a roofer the morning after a storm; **rare** | leans a short ladder (N, 0.18 u) on the ridge pole, climbs 2 rungs (+0.06 u), sets the bird upright (it tips from 20° to 0°, `Back` 0.3 s) and rubs it with a cloth (arm x 150, circles 1 s). Reads: someone up a ladder at the bird | N ladder; a cloth (B28's colours) | **"Storm: straighten the roof bird"** |
| B54.q L | Roof bird: flying | 1–2 kids under it; **sometimes** | arms out (z ∓90) flapping (z ∓90 ↔ ∓60 at 3 Hz), running a circle under it, looking up at it. Reads: kids flapping round the pole | R `Run` | **"Kids fly under the bird"** |
| B54.r W | Long table: the shared meal | everyone, the harvest feast night (and the harvest ritual each year, decisions.md 2026-11-14 research 21) | tables carried out (`CarryTables`, R), bowls passed down both sides (B9.b along a line instead of a ring), everyone eats (B9.a) | R B9, `CarryTables` | "Festival" / **"Long table meal now"** |
| B54.s L | Long table: under it, and the nap | kids during the meal; an old one after | two kids crawl under the table end to end (squash 0.6, lean 30, 0.15 u/s) and pop out the far end laughing; after the meal an old one lies on a bench (`Lie`, 6 s) | R `Lie`, `Laugh`. N the crawl | **"Kids under the long table"** |
| B54.t W | Offering basket: filled at feasts | the chief or a kid the chief sends, a feast morning; **rare** | carries the best basket up (`Basket`), lifts the cloth (arm x 80), tips the food in (B5's tip), lays the cloth back, `Bow`s to the glass | R `TipBasket`, `Bow` | **"Fill the offering basket"** |
| B54.u L | Offering basket: waiting for the Keeper | 2–3 kids, the afternoon after it was filled; **sometimes** | sit in a row by it, chins in hands (arms x 120 to the face), looking up at the glass; now and then one lifts the cloth to check (it's still full), shrugs; if the Keeper taps the glass now, they all jump and point. Reads: kids sitting by a basket, staring at the glass | R `Sit`, `LookUp`, the knock (`GlassVoice`) | **"Kids wait by the offering"** |
| B54.v W | Goat pen: mend the rail | a grown-up; **sometimes** | kneels at the gnawed corner, binds a new rail on (fiddle 1.5 s, `Hammer` 3 taps → `Chips`); the corner looks whole for a season, then gnawed again [est] | R `Hammer`. N a swap of the rail mesh | **"Mend the goat pen"** |
| B54.w L | Goat pen: rails and the gap | kids; **sometimes** | two sit on the top rail swinging (roll ±5°); one crawls through the gnawed gap (squash 0.6, 0.15 u/s) and out the other side, then waves | R `Sit`. N the crawl (B54.s's) | **"Kids at the goat pen"** |
| B54.x W | Fish crate: to the rack | the rack keeper; **often** when the crate is full | lifts fish one by one into the `FishBasket` (arm x 40 → 100, each fish `Pop`s), carries it to the rack (`HangFish`, R) | R `FishBasket`, `HangFish` | **"Fish crate to the rack"** |
| B54.y L | Fish crate: wave across the pond | 1–2 kids; **sometimes** | sit on the crate facing the pond and the family's shore hut, then stand and wave big (`Wave`, both arms); if someone at the shore hut is out, they wave back (`ApartLife`'s wave, R). Reads: kids waving across the water | R `Wave`, the apart-camp waves | **"Kids wave across the pond"** |
| B54.z W | Name post: re-carve the name (2026-11-16 notes: the peg and tag became a carved name post by the door) | the named child's parent, a spring morning each year; **rare** | stands at the post's face (0.13 u out), a knife in the hand: short downward scrapes (arm x 98 → 80 at 3 Hz, 2.5 s) → a few chips every 0.33 s; the carved mark shows freshly cut (pale) for the day; a step back and a nod. Reads: someone scraping at a post, chips flying, the pale mark | R `Stick` as the knife, `Chips`. N the post (a short square post, notches, a carved ring-and-stroke mark on both faces) | **"Re-carve the name post"** |
| B54.zz L | Name post: the kids' heights (notes) | the named kid and a parent (or an older kid); **sometimes** on spring mornings [est] | the kid stands with the back to the post, straight, a little on tiptoe, facing out (2.2 s); the grown-up beside lays a hand on the kid's head (arm x 150) and scratches a line on the post there (three short strokes → a pale scratch stays; at most 3, each a little higher); the kid steps out, turns, looks at it, cheers; the grown-up laughs. Reads: a kid against a post, a hand on the head, a new line | R `Look`, `Cheer`, `Laugh`. N the height scratches | **"Kids measure their height"** |

---

### #694: dogs and wolves in the people's day

The wolves' and dogs' own sides are B20–B26 (specced, #437–#447). Dogs exist in the sim (`Sim/SimCore.Dogs.cs`, cap 1–3) but
are not drawn yet (#444, #445 open); the P0 quadruped (`View/QuadDoll.cs`, `QuadKind.Dog`, `Pup`) is built. These are the people's
parts. The rules stay: **no wolf is ever killed or touched; at least 0.3 u between a wolf and a person; wolves never come in among
the tents** (decisions.md 2026-11-07).

##### B55.a Driving the wolves off: shouts and torches
| | |
|---|---|
| Who · where · when | the 2–3 grown-ups nearest the fire, when a wolf comes within 1.2 u of the camp's edge at dusk or night (B21.b's eyes, B22.a's thief, a bold wolf on the scraps path). Every time it happens; at most once a night |
| Beats | one stands and points (arm x 90) with two head jabs (a shout); another takes a burning stick from the fire (a torch: B29.b's ember tip, bigger, with a small flame; `Sparks` float off it every 0.3 s); together they walk 0.3 u toward the wolf, **never closer than 0.6 u**, waving both arms and the torch (arm x 150 ↔ 170 at 3 Hz), shouting (head jabs); the wolf `Alert`s, backs off 0.2 u, turns and trots into the dark. End beat: they stand 2 s looking after it; the torch goes back in the fire (`Log` arc) |
| Props | torch: a `Stick` with a flame tip 0.03 u (N) |
| On a phone | at night, the torch's sparks and the waving arms against the dark; the wolf's eye dots leaving |
| Sound · cheat | none (the howl is #447's) · **"Wolves come near: drive them off"** |
| Reuse | R B22.a's wave, P0 `Alert` / `Trot`. N the torch and the walk-out. Planner test: every wolf point stays outside the tent ring and ≥ 0.6 u from the people driving it off |

##### B55.b Holding the kids close
| | |
|---|---|
| Who · where · when | every kid outdoors within 1 u of a grown-up, when wolves are seen: the howl (B21.a), eyes at the firelight's edge, or a wolf in view within 2 u of a kid by day. **Often** while wolves are near |
| Beats | the grown-up walks to the kid and puts an arm round them (arm z ∓70 across the back); the kid presses in (0.04 u apart); both face the wolves; a toddler is picked up (C3.b's carry, arms x 60). Held until the wolves are gone, plus 2 s; then the arm drops and the kid runs off |
| On a phone | grown-ups and kids drawn together into pairs, all facing one way |
| Sound · cheat | none · **"Wolves in view: hold the kids close"** |
| Reuse | R B12.f's hand on the shoulder, C3.b's carry, `Turn`. N the gather-in |

##### B56. Dogs with the people (once dogs are drawn, #444/#445)
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B56.a On the hunt | the hunter and the dog, every hunt with a grown dog (B25.a) | before: the hunter whistles (hand to the mouth, x 130, two head jabs) and the dog runs to heel; after a catch the hunter crouches and pats it (arm x 60, two pats); the dog's tail wags at 4 Hz. Reads: the pat on the dog | R B25.a, P0 tail | (B25's) **"Dog: hunt now"** |
| B56.b On guard | a grown-up at the fire when the dog barks at night (B25.b) | stands, looks into the dark (2 s), then scratches the dog's head (arm x 50, 1 s) and sits again | R B25.b | "Wolves come near (guard)" (B25's) |
| B56.c Fetch | a kid and a grown dog (from stage 2; the pup never fetches, B24.b), by day; **sometimes** | the kid throws a `Twig` (B2.b's throw, `Arc` h 0.12, T 0.5 s); the dog runs, head down 0.2 s, the stick in its mouth, runs back and drops it at the kid's feet, then `PlayBow`; the kid cheers; 3 throws. Reads: the stick flying and the dog running after it | R `Arc`, B2.b's throw, P0 `PlayBow`. N stick-in-mouth | **"Dog: fetch"** |
| B56.d A kid rides a big one | a small kid on a big grown dog (generation 2 or more) [est], by day; **rare** | the kid sits on its back (+0.1 u, hands forward x 60), the dog walks 0.4 u slowly, the kid bobs; an older kid walks beside, a hand out to them (arm x 70); the dog lies down (`LieDown`) and the kid slides off laughing. Reads: a kid on a dog's back [est: a 0.145 u kid on a 0.10 u dog looks silly and sweet; the user judges on the clip] | R P0 `Walk`, `LieDown`. N the ride | **"Dog: a kid rides"** |
| B56.e Sleeping by the fire | the grown dog every night | `SleepCurl` at the fire ring's edge, 0.25 u from the fire (never in it); people step round it (`CrowdAvoid`, R+); now and then (**rare**) a kid lies down with the head on it before bedtime (`Lie`, 4 s) | R P0 `SleepCurl`, R+ `CrowdAvoid` | **"Dog by the fire"** |

---

### #695: the village and the town

The village and town come after the camp (people-and-things.md, order confirmed 2026-11-14). Same rules (D), same short tables.
Building site: work only (the user dropped both life uses), so nothing new there.

**Built, part 1 (2026-11-16, `View/Beats/VillageScenes.cs` + `View/CampLife.Village.cs`; clips `Tools/animlab/clips/695-*.json` wait for sign-off):**
B57.a–j, B58.c, B58.f, B58.g, B58.j, B58.k. **Blocked** (no such thing can stand in a living-land box: the smithy, the kiln, the potter,
the pasture, the beekeeper's meadow and the dug channels that bridges cross all come from the Keeper's shaping gifts, which the living land
does not have): B58.a, B58.b, B58.d at a pasture (built at the goat pen, 2026-11-16), B58.e, B58.h, B58.i. Calls marked [est] in the code:
the harvest dance on calendar month 10 with its own cheat ("Harvest dance": "Start a ritual now" is the Keeper Doll's relic ritual); the
scarecrow stands the growing season; a home shows one thing per 10 years of age; no pebble in the town's fountain; the lumber mill stands
in for the timber yard.

#### B57. Fields, homes, well, windmill, timber
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B57.a Kids run between the rows | 2–3 kids, summer, crops up; **sometimes** | tag (B2.a) along the furrows only (the paths between rows), ducking (squash 0.8) where the crop is tall; the farmer straightens up and waves them out (arm z ∓60, two flaps). Reads: heads bobbing along the rows | R B2.a, B17.b's shoo | **"Kids run in the fields"** |
| B57.b A scarecrow made together | 2 grown-ups and the kids, spring after sowing; **rare** (once a year per field) | one plants a cross of two poles (push in, `Dust`, `Jolt`); a kid hands up a bundle of straw (B7.b); the other ties it on (fiddle 1.5 s) and puts an old hat on top (arm x 170) → `Pop`; everyone steps back and laughs; a bird lands on it (B17.b's songbird) and they laugh harder. It stays all season | N scarecrow (a cross, a straw body, a hat; 0.2 u) | **"Make a scarecrow"** |
| B57.c The harvest dance | everyone at the last harvest day (the harvest ritual, decisions.md 2026-11-14) | B2.e's ring on the cut field round the last sheaf (`Sheaf`), 12–16 s; the end jump all together | R B2.e, `Sheaf`, C3.d's end | "Start a ritual now" |
| B57.d Homes mended and dressed | the household, over the years (with #686: two looks per era) | once a season someone mends: a short ladder against the wall (B54.p), hammer taps (`Hammer`, `Chips`); and adds one thing that stays: a flower box, a painted door, a string of drying herbs (each `Pop`s on, at most 3 per home) | R `Hammer`, B54.p's ladder; N the 3 extras | **"Mend and dress a home"** |
| B57.e Gossip at the well | 2–3 grown-ups who came for water; **often** in the morning | they set their buckets down (`Squash`) and talk (B7.a, the laugh at the end) 6–10 s before carrying them home; one leans on the well's rim (roll 10°) | R B4, B7.a | **"Gossip at the well"** |
| B57.f A pebble down the well | a kid; **sometimes**. (The user dropped pebbles into the pond, but kept this one in #695) | climbs the rim on tiptoe (+0.03 u), drops a pebble (arm x 90, the hand opens), then leans over with a hand at the ear (arm x 120 to the head), waits 1.5 s → `hit`: a tiny ring shows deep down [est] and the kid jumps back delighted. Reads: a kid leaning over the well, hand at the ear | R the pebble (B35.b). N the listen pose | **"Kid drops a pebble down the well"** |
| B57.g Watching the sails | 1–2 kids by the windmill; **sometimes** | sit and watch, the head turning round with the sails (yaw follows the sail tip, a full turn of the head would break: ±30° nods in step) | R `Sit` | **"Kids watch the sails"** |
| B57.h Racing the sails' shadow | 2 kids, sunny days; **sometimes** | run with the sail's shadow as it sweeps the ground, trying to stay inside it; jump when it passes. Reads: kids running in a circle round the mill's foot | R `Run`, the sails' angle (R) | **"Kids race the sails' shadow"** |
| B57.i Sit on the log pile | grown-ups at the timber yard on a break; **often** | sit on the stacked logs (+0.06 u), chat (B7.a), a drink from a cup (B9.a's hand to the mouth) | R `Sit`, B7.a | **"Break on the log pile"** |
| B57.j Balance on a log | a kid; **sometimes** | walks along a lying log (+0.03 u), arms out (z ∓90), wobbling (roll ±8° at 2 Hz), jumps off the end (`Bounce`, `Dust`), arms up | R `Bounce`. N the balance walk | **"Kid balances on a log"** |

#### B58. Workshops, bakery, pasture, streets, bridges, fences
| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B58.a Kids watch the sparks | 1–2 kids at the smithy or kiln door; **sometimes** | stand at 0.3 u, flinch at each strike's `Sparks` (back 0.02 u), then edge closer again; the smith waves them back (arm z ∓60) | R `Hammer`'s sparks, `Flinch` | **"Kids watch the smith"** |
| B58.b Showing off a new pot | the potter, after a firing; **sometimes** | carries the new pot out held high (`HoldUp`, x 140), turns it in the hands (yaw 180° over 1 s); passers stop and nod (B30.d's nod); one taps it (arm x 80 → `hit`, a tiny `Jolt`) | R `HoldUp`. N a held pot prop (R `Pot`) | **"Show off a new pot"** |
| B58.c Waiting for a crust | 2–3 kids at the bakery door, morning; **often** | stand in a row, rocking on the toes (bob 0.015 u at 2 Hz); the baker comes out and hands one a crust (B7.b), the others crowd; they share it (B7.b again) and eat (B9.a) | R B7.b, B9.a | **"Kids wait for a crust"** |
| B58.d Petting the goats | kids at the pasture fence; **sometimes** | reach over the rail (arm x 100, lean 20), pat a goat's head (two pats); the goat butts gently (it steps in 0.03 u) and the kid stumbles back laughing | R the herd (Pasture), N the pat answer | **"Kids pet the goats"** |
| B58.e Running from a bee | a kid near the hives, summer; **sometimes** | a bee (a yellow speck 0.012 u with a wobble path) circles the kid's head; the kid flaps both arms (z ∓90 at 6 Hz) and runs 0.6 u in a zigzag; the beekeeper laughs. Reads: a kid running and flapping by the hives | N a bee speck; R `Run`, `Laugh` | **"Bee chases a kid"** |
| B58.f Hopscotch | 2–3 kids in a street; **sometimes** | a grid scratched on the street (a 6-square mark, N, stays a view day); a kid hops along it (0.03 u at 2.5 Hz), turns at the end, hops back; the others wait and clap | N the grid mark | **"Kids play hopscotch"** |
| B58.g Stopping to talk | two people who meet in a street; **often** (as B50.b) | stop, chat (B7.a), go on | R B7.a | "Force chat" |
| B58.h Leaning on the bridge rail | 1–2 grown-ups at dusk; **sometimes** | lean on the rail (lean 20, arms x 70 on it), looking at the water 6–10 s | R `Bridges` | **"Lean on the bridge"** |
| B58.i Racing sticks under the bridge | 2 kids; **sometimes** | each drops a `Twig` off the upstream rail (`Ring`), both run to the other side and lean over; the sticks drift out (0.05 u/s); the first one out wins: a jump, arms up. Reads: two kids running across the bridge and pointing down | R `Twig`, `Ring`, `Cheer` | **"Race sticks under the bridge"** |
| B58.j Fixing a fence | a grown-up; **sometimes** | kneels at a leaning post, pushes it straight (lean 20, `Back` 0.4 s), taps it in (`Hammer`, 3 taps, `Chips`) | R `Hammer` | **"Fix a fence"** |
| B58.k Leaning on and climbing the fences | grown-ups lean (arms x 70 on the top rail) to watch the animals; kids climb and sit on the top rail; **often** | as B54.w | R B54.w | **"Kids on the fence"** |

#### B59. Jetty, orchard, quarry, dyer, watermill, outbuildings (one job and one life use each)

**Built, part 2 (2026-11-17, `vs/695-village-b`):** B59.k and B59.l at a family's barn (a hay heap drawn beside it [est]); clips `Tools/animlab/clips/695-b59*.json` wait for sign-off. **Blocked:** B59.a–j (the jetty, orchard, quarry, dyer and watermill only answer the Keeper's shaping gifts, which the living land does not have).

| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B59.a Jetty W: tie up the boat | the boat's rower after a trip | kneels at the post, loops the rope (fiddle 1.5 s), pulls it tight (lean −10, `Back`); the boat bumps the jetty (`Jolt`, `Ring`) | R the boat (#404) | **"Tie up the boat"** |
| B59.b Jetty L: sit at the end | 1–2 people, summer evenings; **sometimes** | sit at the end facing the water (as B45.c, the rings at the feet); a kid jumps off into the water (B3.a's big jump) | R B45.c, B3.a | **"Sit at the end of the jetty"** |
| B59.c Orchard W: pick apples | pickers, late summer and autumn; **often** in season | reach up (arm x 160, on tiptoe), twist (roll the hand, 0.3 s) → `hit`: the apple `Pop`s into the hand, into the `Basket` (`Arc` h 0.05); the tree sways 4° | N an apple dot (red, 0.02 u); R B5.a's pick | **"Pick apples"** |
| B59.d Orchard L: an apple on the head | a kid under an apple tree; **rare** | an apple drops (`Arc` from the crown) onto a napping kid's head → `hit`: the kid jumps up (hop 0.05 u), looks up, then eats it | R `Arc`, B9.a | **"Apple drops on a kid"** |
| B59.e Quarry W: cut stone | the stonecutter (`CutStone` exists) | as built, plus the block is levered out and rolled on logs by two (B27's pair carry) | R `CutStone` | (exists) |
| B59.f Quarry L: slide down the gravel | kids; **sometimes** | sit at the top of the spoil heap and slide down (offset down the slope over 1 s, `Dust` trail), laugh, climb back up | R `Dust`, `Laugh`. N the slide | **"Kids slide down the gravel"** |
| B59.g Dyer W: dye cloth | the dyer | dips a cloth in a vat (arms x 60 → 20, lean 30, `Droplets` in the vat's colour), lifts it dripping, hangs it on a line (B28's line, the cloths in strong dye colours) | R B28 (the washing line), N vats' colours | **"Dye cloth"** |
| B59.h Dyer L: coloured hands | a kid who helped; **sometimes** | the kid's hands come up blue (two dots on the arm ends); they press them on a spare cloth: two blue prints `Pop` on it; they show it off (`HoldUp`) | N hand-print marks | **"Kid's blue hands"** |
| B59.i Watermill W: the sacks | the miller | carries grain in (`Sack`, heavy walk) and flour out (B54.n) | R B54.n | **"Watermill: carry sacks"** |
| B59.j Watermill L: watching the wheel | 1–2 kids on the bank; **sometimes** | sit facing the wheel; one drops a leaf (B45.b's boat) above it and they watch it go round and out | R B45.b | **"Kids watch the wheel"** |
| B59.k Outbuildings W: hay and the animals | the household at the barn or byre, mornings | pitches hay from a heap into the byre (arm x 40 → 140 with a `Bundle`, `Grass` bits fly), sets a bucket down for the animals (`Squash`) | R `Bundle`, `Grass` puff | **"Barn chores"** |
| B59.l Outbuildings L: jumping in the hay | 2 kids; **sometimes** | climb onto the hay heap and drop back into it (squash 0.6 on landing, `Grass` bits fly, the heap dents 0.02 u and springs back) | N the heap's dent | **"Kids jump in the hay"** |

---

### #708: the fish, seen from above and through the glass (B70)

**Decided** (decisions.md 2026-11-15, the walkthrough's ★): fish first, inside today's water wall, seen from the side where the
pond meets a pane, and as shadows from above; no new transparent layer (the playtest phones already run at 25–30 fps,
`docs/design/pt1b-proposals.md` #708); the deeper basin waits. Today no fish is drawn after the arrival: B16.a was specced but not
built (`SimCore.EcoFishShown` has no view; only the arrival's bag uses `LifeLooks.FishShape`).

**What is there to draw on:** where pond cells meet a pane, `BoxView.Eco.WaterWall` (`View/BoxView.Eco.cs:291`) draws a flat face
on the glass from the water line down to the soil cut: lighter at the top, `waterDeep` lower down. Its height is the water's depth
at that edge (pond water sits 0.08 u under the land). The pond reaches the glass in the approved opening (memory: "pond to
glass"); the front pane faces the default camera, the side panes show at an angle.

##### B70.a The count (no new animals)
The fish drawn are the sim's: `EcoFishShown()`, capped by `AnimalBudget.Fish` (5 on the 6 × 6 land, via `SimCore.FishShownCap`).
**Each fish is drawn once**: as a shadow from above, or, when its swim takes it within 0.15 u of a wall, as a side fish on the
wall. **At most 2 on the walls at once** [est], so the glass shows a glimpse, not a tank show. No fish before fish are placed (B16.e).

##### B70.b Shadows from above (B16.a, built now)
| | |
|---|---|
| Look | `LifeLooks.FishShape` (R): a flat body 0.045 × 0.014 u with a forked tail, lying on the water at +0.001 u, in the water's colour darkened to 55 % (`waterDeep` × 0.55), **opaque** (the arrival's 0.55-alpha `Shadow` material is not reused) |
| Motion | a loose school drifting at 0.03–0.05 u/s on a wander inside the pond's cells (0.06 u from the bank); the school turns together now and then (all within 0.3 s); each fish's tail wags ±12° at 2.5 Hz (the tail triangle turns about its root) |
| Answer | a `Ring` or `Droplets` within 0.3 u, or a person in the water: dart 0.2 u away in 0.25 s, regroup after 2 s (B16.a, B16.d, B39.c). B16.e: before a bite, one fish darts to the hook in 0.4 s |
| Winter, ice | half the speed; under ice they still show, dimmer (`waterDeep` × 0.7) [est] |
| On a phone | 0.045 u ≈ 11 px: a dark sliver moving on the water. Five of them in a loose group read as "fish" |

##### B70.c A fish seen through the glass
| | |
|---|---|
| Look | a side-view fish (N, `LifeLooks.FishSide`): a lens-shaped body 0.05 × 0.018 u, a forked tail 0.014 u, a dorsal fin tick, a paler belly line (`water` × 1.1) and a light eye dot; the body in `waterDeep` × 0.55. Total ≈ 0.065 u (16 px on the front pane at the default camera). Drawn flat on the wall face, 0.001 u outside it (toward the glass), opaque, kept inside the wall's outline: at least 0.015 u under the water line and 0.01 u above the soil cut |
| Where | only where a wall is at least 0.06 u tall (shallower edges: no side fish there) [est] |
| Hand-off | a shadow that reaches a wall cell shrinks away on the surface (scale 1 → 0, 0.3 s) while the side fish grows in on the wall below it (0 → 1, 0.3 s), at the same x; going back up is the reverse. The eye reads "it went down there" [est] |
| Motion | along the wall at 0.05–0.08 u/s, tail ±15° at 3 Hz, a bob ±0.005 u at 0.5 Hz, at 30–80 % of the wall's depth; every 6–12 s it hangs still for 1 s (a fin flutter: the dorsal tick ±10° at 6 Hz), then darts 0.1 u in 0.25 s. At the wall's end it turns: scale x 1 → 0 → −1 over 0.4 s (a flat flip, cartoon) |
| Answer | a `Ring` above it within 0.3 u (a splash, a fisher's yank, a skipped stone): it darts along the wall and down to 80 % depth |
| Winter, ice | slower (× 0.5) and low (70–90 % depth); under ice the wall's top shows the ice edge (#618) and the fish stay below it |
| On a phone | the front pane's water band sits low in the default frame; a 16 px dark fish sliding along it, then turning, reads as "a fish behind the glass". At the fish's arrival zoom (`LifeArrivalView.WaterZoom`) it is about twice that |
| Sound · cheat | none · **"Fish: show them at the glass"** (camera to the pond's pane, 2 fish sent to the wall), **"Fish: startle"** |
| Reuse · new | R `WaterWall` (where the walls are, their heights), R `FishShape`, R `Ring` hooks. N `FishSide` mesh, a pooled `FishView` (5 transforms, one mesh, one opaque material in the SRP Batcher, no `MaterialPropertyBlock`) |
| Tests | drawn fish ≤ `AnimalBudget.Fish`; ≤ 2 side fish; no side fish outside a wall's outline or on a wall under 0.06 u; none with a fish stock of 0; each fish drawn once (never shadow and side at the same time except during the 0.3 s hand-off) |

**People:** kids peer in and try by hand (B39.c); the fishers' bite waits for a shadow at the hook (B16.e). The people never see
the side view (they are inside); it is the Keeper's.

---

### #709: the nest in the thicket (B71)

**Decided** (decisions.md 2026-11-15, the walkthrough's ★, and the correction comment on #709): nest and eggs, no hunting; one nest
in the placed thicket, one parent in spring, kids look, a gatherer takes an egg now and then, never the last. Today the bird comes
down once at the first bush (`View/BushView.cs`'s sign: a grey box bird, scale 1.8, fly in, sit, fly off) and is never seen again.

**Which thicket [est]:** thickets are becoming a list (#728, landscaping §8.2). The nest goes in **the first thicket**, the disc
founded from the first bush: the bird first came down there, so the story follows through. If that disc is gone or under 6 bushes,
next spring the nest moves to the oldest disc with 6 bushes or more. Inside the disc: the bush on the camera's side nearest the
camp, at least 0.3 u from any path, never one being picked at that moment.

**The count:** the bird is not an `AnimalBudget` kind, and neither is the sign bird today. pt1b-proposals (#709, the ★ the user
chose) said the nest "costs no animal slots". **Ask:** keep the bird outside the budget, or take one slot from the frogs.

**Sim built (lane A, `vs/709-nest-sim`):** `EcoState.nest`, `EcoModel.Nest.cs`, `SimCore.Nest.cs`, `LifePlanner.Nest.cs`. Q4, Q5 and Q6 take their defaults, each one value: `AnimalBudget.NestBirdSlots` (0 = outside the budget), `EcoTuning.nestChicks` (2; 0 = the eggs just go), `EcoTuning.nestWhichThicket` (0 = oldest thicket with 6+ bushes; 1 = the biggest). The nest stays through the winter as an empty nest (`nestWinterGone` false). Eggs are laid in one go the second month of spring (2 to 3); the view shows them one a day if it wants (B71.a).

##### B71.a The nest and the eggs (the asset)
| | |
|---|---|
| Nest | built in code: a shallow bowl of 10 twigs (thin boxes in bark and straw colours) in a ring, outer r 0.03 u, 0.012 u high, × 1.5 like the props (r 0.045 u ≈ 11 px); sits in the bush's crown, front side, 0.06 u up; open on top, so the default camera's 34° pitch sees in |
| Eggs | 0–4 small icos 0.012 u (× 1.5 = 0.018 u ≈ 5 px), pale blue-green with a darker speck; they sit in the nest's hollow |
| Through the year | **spring**: built (B71.b), eggs come one a view day from day 3 to 4 eggs (fewer if taken); **late spring** [est, an option]: two chicks hatch (fluff dots 0.015 u, beaks up, a peep bob at 3 Hz), stay 3 view days and fly off with the parent; **summer, autumn**: the empty nest stays; **winter**: it's gone (blown down) and a new one is built next spring |
| On a phone | an 11 px brown ring on the bush with pale dots in it; the bird on it is the real read |

##### B71.b The bird's spring visit
| | |
|---|---|
| When | the first spring dawn each year, while the thicket stands. Then the bird stays through spring |
| Beats | flies in as the sign bird does (`BushView`'s fly-in, R) with a twig in its beak (a 0.02 u line), lands on the bush, drops the twig in (`Pop`); three trips that first morning (the nest grows 0.6 → 0.8 → 1). From the eggs on it **sits**: body 0.01 u lower, fluffed (scale 1.1), head turning (yaw ±40° every 2 s). Every 20–40 s it gets up, flies a small circle (r 0.3 u, as B17.b's songbird) and comes back 6–10 s later. End of spring: it leaves for good (the fly-off, R) |
| On a phone | the same grey bird as at the first bush, now sitting on a nest: "it stayed" |
| Sound · cheat | a few chirps from `BirdSong` (R) when it lands [est] · **"Nest: spring now (bird and eggs)"**, **"Nest: next stage"** |
| Reuse | R the sign bird's meshes and flight (`BushView`), R B17.b's circle. N the nest, eggs, sitting pose, the yearly stages (sim: a nest record in `EcoState`, saved; lane: sim) |

##### B71.c Kids look in
| | |
|---|---|
| Who · where · when | 1–3 kids, spring days while there are eggs. **Sometimes** (1 day in 2); never while a gatherer is there |
| Beats | they creep up quietly (B6's creep, 3 steps), stand on tiptoe (+0.03 u) and peer in (lean 25, head −30), still 2 s; one counts the eggs (pokes the air three times, arm x 70, not touching); if the bird is on the nest it `Alert`s (head up, wings half out): the kids step back two steps, hands behind their backs (arms x −20), and watch from 0.3 u. A grown-up beckons them away (arm x 60 → 100, twice). End beat: they tiptoe off, then run |
| On a phone | small dolls on tiptoe at a bush, very still |
| Sound · cheat | none · **"Kids look at the nest"** |
| Reuse | R B6 creep, B30.c's tiptoe peek, B41.a's count poke. Rule: the kids never touch the nest or the eggs |

##### B71.d A gatherer takes an egg
| | |
|---|---|
| Who · where · when | a gatherer picking at that thicket in spring (B5.a), when the nest has **2 eggs or more** and the bird is off it. At most 2 eggs a spring [est]; **never the last**; never while kids are looking |
| Beats | the gatherer notices (head turn, 0.3 s), sets the basket down (`Squash`), reaches in carefully (arm x 100, slow 0.6 s, lean 25) → `hit`: one egg `Pop`s from the nest into the hand; holds it up to the light (x 140, 0.8 s); lays it gently on top of the berries (arm x 60). The bird comes back and scolds: lands on the bush, hops toward the gatherer with wings out flapping (6 Hz, 1 s), never touching (≥ 0.05 u); the gatherer backs away with the basket and a small apologetic `Bow`. End beat: at the stash the egg rolls out with the berries (B5's tip, R); food + 1 |
| On a phone | the reach into the bush and the little bird flapping at the gatherer (a tiny echo of the duck's charge, B15.a) |
| Sound · cheat | two sharp chirps on the scold (`BirdSong`, R) [est] · **"Take a nest egg"** |
| Reuse | R B5.a, B5's tip, B15.a's charge (smaller), `Egg` (B15.a's prop), R+ the duck egg's sim path (`SimCore.TakeEgg`, `LifePlanner.Eggs.cs`) as `TakeNestEgg`. No hunting of the bird: it is never a hunt target |

---

### Rituals and the teller (S-ritual, B72–)

**Status: built on `vs/ritual-b72` (S-ritual Editor pass, 2026-11-17), waiting for the clips' sign-off.** Plans: `Life/MomentStagePlan.Rituals.cs`
(`RitualPlanTests`); poses: `View/Beats/RitualBeats.cs`; targets and what they leave: `View/CampLife.Rituals.cs`; B75.b and B77:
`View/Beats/RitualScenes.cs`; B76: `View/CampLife.Teller.cs`, `View/Beats/TellerBeats.cs`. Calls made in the build are marked [est] in place.
The user asked for these on 2026-11-15 (approvals row 217: "Rituals and the teller's manner need
staging specs"). Each waits for its OK on the approvals page (rows 218–229), then its AnimLab clip is signed off before it
merges. **Every number in this section is [est]** until AnimBench and AnimLab.

**What is built today:**
- **The rituals' sim** (`Sim/SimCore.Rituals.cs`, #721). `RitualKind` has four: the berrying walk (autumn, calendar month 8),
  the harvest feast (9), the offering (9) and stacking the woodpile (10). A box keeps at most one of berrying and woodpile
  (`camp_winter`) and one of feast and offering (`village_harvest`). The lead is `RitualLead`: the town's priest for the
  offering, else the era's leader, else the eldest adult. `RitualKid` names one kid a year. The lines grow by era: in a
  village that keeps the feast, the berrying walk ends at the long table; in the town the tavern sets the feast table and
  the priest carries the offering. The offering goes to `OfferingPlace`: the Keeper's tree, else "the tallest tree" (W721.c).
  In spring the line `ritual_offering_empty` says the basket was found empty.
- **The rituals' view.** Each plays as a held moment (`MomentKind.Ritual`, `Life/LifePlanner.HeldMoments.cs`, 50 s budget).
  Today `MomentStagePlan.Build` stages all four as the festival (`plan.Festival(cast)`, `Life/MomentStagePlan.cs:207`), led by
  the ritual's lead. The caption is `RitualKind.Caption` (`View/Moments/StagedMoments.cs`). The people's lines are
  `PeopleVoice.MomentLines` (`View/ShowMe/Core/PeopleVoice.Moments.cs`); two lines there wait for these beats: "Not that
  bush, that bush." and "Don't look back, it's rude."
- **The teller** (#717, W717). `Life/LifePlanner.Day.cs` makes the Keeper's pick tonight's teller (`SimCore.StoryTellerOn`),
  and the fire keeper is never the teller. The telling runs from `TellFrom` 18.3 to `TellSlowUntil` 19.7 (slow) or
  `TellShortUntil` 18.85 plus `LaughHours` 0.15 (short). The teller plays `StoryTell` (`DollStyle.Speech`); everyone else
  plays `ListenStory` with the kids at the front (`ListenToTheTeller`), then `LaughPoint` after a short one. The four lines
  are `PeopleVoice.TellerLines`. The comment at `TellFrom` says the manners' gestures wait for this spec.
- **Cheats that exist:** SimDebugPanel "Rituals (#721)": "Play berrying / woodpile / feast / offering now"
  (`StagedMoments.DebugPlayRitualNow`). SliceDebugPanel: "Story tonight: slow" and "Story tonight: short".

**How each ritual is built (B72–B75).** Each ritual gets its own plan in `MomentStagePlan` (`Berrying`, `Woodpile`, `Feast`,
`Offering`) in place of `Festival` (R+ `Build`'s switch). The plan keeps the pure plan's rules (R): everyone starts from
where they stand; the gathering takes at most `MaxGather` 16 s; `GoAt` spreads the set-off; no sim dice (`Hash01`); every
member is released by `Duration`; the planner, the view and AnimBench run the same plan. **N:** `MomentGeometry` gets each
ritual's targets (the bushes, the woodpile and the chopping spot, the long table's pose, the tree's visit spot), filled by
`StageHeldMoment`. New `StageBeat`s are named per spec; each gets its pose in `View/Beats/MomentBeats.cs` and its symbol in
`RitualBubbles.ForHeldMoment` (R+). `HeldMomentSeconds(MomentKind.Ritual)` becomes one value per ritual (R+): berrying 55 s,
woodpile 45 s, feast 50 s, offering 45 s.

**Shared rules.** D holds (kids safe, cheats, AnimLab, reuse marks). On top of D: `RitualLead` leads every beat that has a
leader; the year's `RitualKid` gets the kid's beat when there is one; B11's director never starts anything in a ritual's cast.
Where a ritual has no target (no bushes in reach, no woodpile, no tree), today's festival stand-in plays.

#### B72. The berrying walk (`camp_winter` "the baskets"; every autumn)

*Everyone walks out to the bushes together, picks and eats, walks home in a line with full baskets and holds up purple fingers.*

| | |
|---|---|
| Who · where · when | the lead, then every free grown-up and kid up to `MaxFestival` 16; the year's berrying moment (month 8). The target: berry bushes with berries (the thicket's bushes or `BushCell`s), up to 6, nearest the camp first. A bush more than 2.5 u from the fire is skipped [est] |

| Beat | What | Time |
|---|---|---|
| 1 Baskets | in the gathering the lead goes to the basket stack (`ConsequenceKind.Baskets`, B54.a's take), lifts baskets off and hands out up to 4 (B7.b, 0.8 s each); the others bring their own `Basket`. The lead holds one up (x 140, 0.6 s): a Sun over the lead | 6–10 s |
| 2 Walk out | a loose line behind the lead, 0.14 u apart, ±0.04 u sideways by hash; empty baskets swing (arm z ±10 in step with the stride); 1–2 kids run 0.3 u ahead and back once | by distance, ≤ 8 s |
| 3 Pick | at the bushes they spread out, 2–3 to a bush, starting 0.8 s apart by slot; B5.a's pick every 2.5 s (reach, bush `Sway` 11°, a berry arcs into the basket), 4 picks each. Kids eat every other berry (B5.c's `TasteBerry`), so their baskets fill slower. Once, the lead points a picker on to the next bush (`Point`, arm x 90, 0.5 s): "Not that bush, that bush." | 12 s |
| 4 Purple fingers | from each picker's second pick their hand dots turn purple (a 0.012 u dot at each arm's end); a kid who ate 3 berries gets B17.a's purple mouth | (with 3) |
| 5 Walk home | the line forms again with the lead at the back; full baskets held in front in both hands (arms x 70), B4's heavy walk at × 0.85; Notes over 2–3 walkers | by distance, ≤ 8 s |
| 6 Tip | at the stash they tip one after another down the line, 0.4 s apart (`TipBasket` as built: 3–5 berries arc onto the pile, pile `Squash`). **Village that keeps the feast:** they set the full baskets on the long table instead (`Squash`, berry dots on top), and each eats one (B9.a's hand to the mouth, once) | 6 s |
| 7 End beat | everyone holds both hands up to the glass, palms out (arms x 160, z ∓30) for 1 s; two kids put their hands side by side (0.04 u apart), look, and laugh (`Laugh` 0.6 s) | 2 s |

| | |
|---|---|
| A child's first walk | a kid in their first autumn walking with the rest (age 3–4) holds a parent's hand all the way (B7.c) and carries a small basket (`Basket` × 0.7). At the bush it eats every berry it picks; at the stash the parent drops two berries from their basket into the kid's, and the kid tips those (research 21 §7.1's later hook) |
| Props | `Basket` (R), the basket stack (R, B54.a), the small basket (R+ × 0.7), purple hand dots (N; the same dot as B59.h's blue hands, with a colour), B17.a's purple mouth (R). The purple fades at supper (B43.c's wipe, R) |
| How it reads on a phone | a line of 6–12 dolls going out to the bushes; several bushes shaking at once reads as "everyone picking"; the hands-up is a row of raised arms (an arm ≈ 19 px). The purple dots are about 3 px: the pose carries the end beat, the colour is a bonus |
| Kids safe | the walk crosses no water and passes no fire; kids stay within 0.6 u of a grown-up |
| Sound · cheat | none · R "Play berrying now"; N **"Berrying: end at the long table"** (keeps the feast too and sets the village), **"Berrying: a kid's first walk"** |
| Reuse | R the plan's gather and `Travel`, B5.a `PickBerry`, B5.c `TasteBerry`, `TipBasket` (`View/Beats/BerryBeats.cs`), B7.b (`View/Beats/PairStage.cs` `HandOver`), B7.c, B4's heavy walk, B17.a, B43.c, `Point`, `Laugh`, `RitualBubbles` (Sun, Notes). N `MomentStagePlan.Berrying`, beats `HandOut`, `WalkLine`, `ShowHands`; the purple dots |
| AnimLab | `Tools/AnimBench/clips/721-berrying.json` (8 grown-ups, 4 kids, 3 bushes, the stash) |

#### B73. The log line (`camp_winter` "the woodpile"; every autumn, before the snow)

*A line passes logs hand to hand onto the woodpile while someone mends its roof; the lead sets the last log on top.*

| | |
|---|---|
| Who · where · when | the lead, up to 8 free grown-ups and up to 4 kids; the year's woodpile moment (month 10). From the woodcutter's chopping spot (where `Chop` and `StackWood` work) to the woodpile under its roof (`ConsequenceKind.WoodpileRoof`). Month 10 comes before the first snow, so it never plays in snow |

| Beat | What | Time |
|---|---|---|
| 1 The heap | in the gathering each grown-up brings one `Log` from the chopping spot (C3.d's bring, R: `Travel` carrying `Prop.Log`) and drops it on a heap 0.5 u from the woodpile (lean 30, `Squash`, `Dust`): 6–8 logs | 8–14 s |
| 2 The line | the grown-ups stand in a line from the heap to the pile, 0.15 u apart, facing across it, every other one 0.03 u to the side (a zigzag reads better than a straight row); the lead stands last, at the pile | 1 s |
| 3 Pass it along | the first lifts a log (lean 30 → 0, `Back` 0.3 s). Each passer turns to the giver (yaw −70°, arms x 80, 0.25 s), takes it (`Pop`), swings to the next (yaw +70°, 0.3 s `Snap`) → `hit`: the log moves on (`Pop`). 0.65 s a hop; a new log starts every 1.2 s, so 2–3 logs are in the line at once and the arms go in a wave. "Pass it along!" | 8 logs ≈ 14 s |
| 4 Onto the pile | the lead takes each log and slides it onto the pile under the roof (B54.c: arm x 60, push 0.04 u, `Jolt`); the pile `Pop`s 1.03 with each | (with 3) |
| 5 The roof | at the same time one grown-up outside the line leans the short ladder (B54.p's) on the roof, climbs 2 rungs (+0.06 u), lays a thatch bundle on it (`Bundle`, arm x 150 → `Pop`) and taps it down (`Hammer`, 3 taps → `Chips` in straw colour); the roof's sag goes (tilt 4° → 0°, `Back` 0.3 s) | 6 s |
| 6 The kids' line | the kids pass `Twig`s in a short line of their own beside the big one (0.1 u apart) into a basket at the pile's foot. One kid hugs the biggest twig and won't pass it (arms x 60 round it, turns away yaw 90°, 1 s): "That one's mine. I'm keeping it."; the next kid laughs and they go on | (with 3) |
| 7 A dropped log | in 1 ritual in 2, by hash: a passer fumbles (arms x 80 → 40, 0.15 s), the log drops (`Bounce`, `Dust`), the next one hops back 0.04 u with arms up (x 70): "Mind your toes."; both laugh (`Laugh` 0.6 s) and it goes on | 2 s |
| 8 End beat | the lead sets the last log on top (both arms x 120, slides it on, the pile `Squash`es) and pats it twice (arm x 90); everyone claps (B2.e's end clap, `Clap` 1 s); the kids sit on the low logs (B54.d's dry-day sit) | 3 s |

| | |
|---|---|
| Props | `Log` (R), the woodpile under its roof (R, `Pieces.Woodpile` in the prop), the short ladder (N in B54.p, shared), a thatch bundle (R `PropKind.Bundle`), `Twig` (R `PropKind.Twig`), a basket (R) |
| How it reads on a phone | a zigzag row of 6–8 dolls with a log moving down it and the arms swinging in a wave: "passing logs along". The doll on the ladder is a second, slower read. A log is 0.1 u (25 px) |
| Kids safe | kids pass twigs only, never logs, 0.15 u off the big line; nobody stands under the ladder |
| Not staged here | the village hook "every new roof gets its first log from it" belongs to a building's raising, later |
| Sound · cheat | none · R "Play woodpile now"; N **"Log line: drop a log"** |
| Reuse | R C3.d's bring, B7.b's hand-off shortened to a swing, B54.c, B54.p, B54.d, B2.e's clap, `Hammer`, `Chips`, `Bounce`, `Dust`, `Laugh`. N `MomentStagePlan.Woodpile`, beats `PassLog`, `Fumble`, `MendRoof`; the zigzag line |
| AnimLab | `721-log-line.json` (7 grown-ups, 3 kids, the heap, the woodpile) |

#### B74. The long table: the harvest feast (`village_harvest` "a feast"; every autumn; extends B54.r)

*Everyone brings something to the long table, it gets one board longer, the first bowl is served, bowls go down both sides,
they eat, raise their bowls together and sing.*

B54.r (the shared meal) and B54.s (under it, and the nap) are signed off. This spec stages them as the feast's held moment;
beats 5, 7 and 10 below **are** B54.r and B54.s, built once. No second long-table meal.

| | |
|---|---|
| Who · where · when | everyone up to `MaxFestival` 16; the year's feast moment (month 9). At the long table by the fire (`ConsequenceKind.LongTable`, `PropAnchor.Fire`), its long side across the screen so the far bench faces the camera [est]. Town with a tavern, on the line's tavern years: the table stands in front of the tavern's door and the tavern keeper carries the bowls (`Serve`, R) |

| Beat | What | Time |
|---|---|---|
| 1 Gather | everyone walks to the table with a `Bowl` or a `Basket` (C3.d's bring); baskets go on the table (`Squash`, berry dots on top) | 8–14 s |
| 2 One more board | every year after the first: two grown-ups carry a new board (B27's pair carry, `Lead` / `Side`) to the table's foot, lay it on (arms x 80 → 60, `Squash`) and one taps it on (`Hammer`, 3 taps, `Chips`); the table is one board longer: "One more seat this year." | 4 s |
| 3 Sit | everyone sits on the benches from the head down, 0.07 u apart, both sides; the lead at the head; kids beside their parents. More people than seats: the last kids sit on the ground at the foot | 2 s |
| 4 The first bowl | the lead ladles at the pot (B30.b's dip) and gives the first bowl to the eldest (B7.b); the eldest lifts it (x 120, 0.5 s) | 3 s |
| 5 Down the sides | bowls pass hand to hand down both benches (B9.b along a line: turn, hand over, 0.5 s a hop, both sides at once) | 4–5 s |
| 6 Budge up | the last one in (the latest `GoAt`) comes to the bench; its sitters shuffle along 0.04 u in a wave from the head, 0.1 s apart (a sideways hop 0.015 u each), and the late one sits: "Budge up, there's room." | 2 s |
| 7 Eat | B9.a, out of step; 2 kids crawl under the table end to end (B54.s); "Who made this? It's good." | 10 s |
| 8 Bowls up | everyone raises their bowl at once (x 140, `Back` 0.4 s, held 0.6 s), in step on purpose (P6 exempt, as C3.d's jump); a Sun over the lead | 1.5 s |
| 9 The song | `Sing` along the benches: a sway (roll ±6° at 0.5 Hz) that runs down each bench, each 0.15 s after the one before; Notes over 3–5 | 6 s |
| 10 End beat | they stand from the foot end; the bowls are stacked at the head (B30.e's stack); the oldest stays and lies down on the bench (B54.s's nap, `Lie`) | 3 s |

| | |
|---|---|
| The table grows | the long table (R+ its mesh) is 0.44 u plus 0.04 u a board, one board a year since the first feast (the count the `harvest_feast_seats` callback already reads), at most 12 boards (0.92 u). After 12 the year's board replaces the oldest: a pale new board among grey ones |
| Props | the long table (R+), a board 0.04 × 0.1 u in the table's wood (N), `Bowl`, `Basket` (R), the pot's ladle (B30.b) |
| How it reads on a phone | a long table seen across with two rows of dolls; the bowls going up together; the sway running down a bench. One board is 10 px: a single year doesn't show, a few years do |
| Kids safe | the crawl stays under the table; a kid seated on the ground at the foot keeps D's fire rule (a grown-up within 0.6 u, never at the flames) |
| Sound · cheat | none (the song is `Sing`'s own, R) · R "Play feast now", R "Long table meal now" (B54.r); N **"Feast: add a year's board"** |
| Reuse | R B54.r, B54.s, C3.d's bring, B27's pair carry, B30.b, B30.e, B7.b, B9.a, B9.b (`View/Beats/MealBeats.cs`), `Hammer`, `Sing`, `Lie`, `Serve`; `CarryTables` (R) for an extra table when a town's cast outgrows it. R+ the table by boards. N `MomentStagePlan.Feast`, beats `AddBoard`, `Budge`, `BowlsUp`, `SongSway` |
| AnimLab | `721-feast.json` (12 people with 4 kids, the table at 6 boards) |

#### B75. Carrying the offering to the tree (`village_harvest` "an offering")

##### B75.a The carry (every autumn)

*The lead carries the best basket in both arms to the Keeper's tree, the kids carry a small one behind, and everyone bows to
the glass.*

| | |
|---|---|
| Who · where · when | the lead carries (`RitualLead`: the priest in the town); the year's `RitualKid` and one more kid carry the small basket; the people behind as in the Ask below. The year's offering moment (month 9). **Following the 2026-11-16 note** (the offering basket's home is the foot of the Keeper's tree, `ConsequencePropsView.OfferingAtTree`; redrawn in the #693 notes): the lead lifts the best basket at the camp's stash (else in front of the fire) and carries it to the offering basket at the tree's foot, kneeling at it on the side they come from (0.12 u out). There is no stone to start from |
| Before the tree stands | (`KeeperTreeGrown` false, W721.c) the tallest wild tree: the grown tree with the biggest crown, the nearest the camp on a tie, its trunk plus 0.12 u on the camera's side; the best basket is set down there (no offering basket to tip it into). No grown tree at all: they set it down in front of the camp and look up (beats 1, 5 and 6 only) |

| Beat | What | Time |
|---|---|---|
| 1 Lift | at the stash the lead squats (squash 0.8, 0.3 s) and lifts the best basket in both arms against the chest (arms x 90, lean −5, `Back` 0.5 s): "Gently. It's for the Keeper."; the two kids lift the small one between them | 1.2 s |
| 3 The walk | the careful walk: speed × 0.75, stride × 0.7, almost no bounce (0.002 u), the basket never swings. The two kids follow 0.2 u behind, the small basket between them, one hand each on it (arms x 70, B7.c's pair). The rest walk behind in pairs (B7.c), 0.2 u apart. The Doll symbol (R) over the lead | by distance, ≤ 14 s |
| 4 Fill at the tree | the lead kneels at the offering basket (B4's kneel), lifts its cloth (arm x 80), tips the best basket in (B54.t, `TipBasket`) and lays the cloth back; the kids set the small basket 0.08-0.12 u beside it and each drops one berry in (arm x 60). Before the tree stands: the best basket is set down (arm x 40, lean 35) | 3 s |
| 5 To the Keeper | everyone looks up at the glass (`LookUp`, 1 s), then bows in a wave, nearest the basket first, 0.12 s apart (C3.c's bow wave); the lead bows last; a Sun over the lead | 3 s |
| 6 Still | they stand quiet for 2 s; one leaf falls from the crown (`Leaves` 1) | 2 s |
| 7 Walk back | they turn and walk back. One kid turns to look back (yaw 150°, 0.3 s); a grown-up puts a hand on its head (arm x 110) and turns it forward again (0.5 s): "Don't look back, it's rude." Released to their day 2 s into the walk | 3 s |

| | |
|---|---|
| A far tree | a walk longer than 14 s starts part way: the cast is placed along the route, already walking (the plan's `Travel` from a point on the route) |
| The basket's stay | the offering basket stands at the tree all year (its home since the 2026-11-16 note): full under its cloth from the offering until the spring line, the kids' small basket beside it (× 0.6); empty after the spring line (B75.b's look) until the next offering. Read from the chronicle, so a reload keeps it. B54.u's kids wait by it there |
| Props | the offering basket as a carried prop (R+ `ConsequenceMeshes` "offering_basket"), the small basket (R+ `Basket` × 0.6), the cloth (R, the prop's top) |
| How it reads on a phone | one doll walking slowly with a basket held in both arms, two small dolls and a small basket behind, then the bow wave at the tree |
| Kids safe | no water or fire on the way; the kids walk behind the lead, never ahead |
| Sound · cheat | none · R "Play offering now"; N **"Offering: before the tree (tallest tree)"** |
| Reuse | R B54.t, `TipBasket`, B4's kneel, B7.c, C3.c's bow wave, `LookUp`, `Leaves`, B32's visit spot (`View/KeeperTreeKit.cs`), `OfferingPlace`, the `PeopleVoice` lines. R+ the basket's second spot. N `MomentStagePlan.Offering`, beats `CarryCareful`, `LookBack`; the careful walk |
| AnimLab | `721-offering.json` (the lead, 2 kids, 4 behind, the Keeper's tree) |

**Ask:** who walks to the tree: everyone behind the lead every year, or only the lead and the kids while the rest watch from
the fire? My pick: the lead, the kids and up to 4 grown-ups in the camp and the village; the whole town behind the priest (the
town's line says so).

##### B75.b The empty basket in spring (once a year)

| | |
|---|---|
| Who · where · when | the morning after the `ritual_offering_empty` line (month 3): the first kid who passes the tree (B32.b's passers), else the lead at midday. Over the winter, a kid passing now and then lifts the cloth to check (B54.u): "Is it empty yet?" |
| Look | it empties in the dark with nobody within 1 u and the camera elsewhere, never in view: the line says nobody saw who emptied it. Empty: the cloth lies flat in the basket (0.02 u lower), no food at the rim. The kids' small basket empties too |
| Beats | the kid lifts the cloth (arm x 80, lean 20), freezes (`Freeze` 0.5 s), lifts the empty basket high (`HoldUp`, x 150, 1 s) and runs to the fire with it held up; 2–3 grown-ups there look up at the glass (`LookUp`, 1 s) and one nods slowly (B7.a's nod). End beat (following the 2026-11-16 note): the first of them takes it from the kid and carries it back to the foot of the Keeper's tree with B75.a's careful walk and sets it down there, empty [est: in the same scene, not "later that day"] |
| How it reads on a phone | a kid running with an empty basket held high, the grown-ups at the fire looking up |
| Sound · cheat | none · N **"Offering: empty it now (spring)"** |
| Reuse | R B54.u, `Freeze`, `HoldUp`, `Run`, `LookUp`, B7.a's nod, B75.a's walk. N the empty look |
| AnimLab | `721-offering-empty.json` |

#### B76. The teller's manner at the fire (W717)

*The Keeper's pick tells tonight's story: slow, with all the voices, or short, for a laugh.*

| | |
|---|---|
| Who · where · when | the Keeper's pick (`tellerPicked`), the evening the story choice is answered; everyone at the fire listens (`ListenToTheTeller`). The fire keeper (`dayFireKeeper`) is never the teller and keeps poking the fire through it (B9.c) |
| Where | the teller stands behind the fire as the camera sees it, 0.22 u from its centre (the festival lead's spot, `TossReach`), facing the camera and lit by the fire. The listeners sit in a half ring on the camera's side: kids in front at 0.4 u from the fire, grown-ups behind at 0.55 u. The standing teller shows over the seated heads |
| Kids safe | no kid comes closer than 0.33 u to the fire, even on "come closer"; grown-ups sit within 0.6 u (D) |
| Today | `StoryTell` plays `DollStyle.Speech`. A new `DollStyle.Tell` (N) replaces it for the picked teller, by manner |
| Lines | each of `PeopleVoice.TellerLines` shows once, as a bubble at the beat named below; the listeners keep the Eye symbol (R, `ListenStory`) |

##### B76.a Slow, with all the voices (18.3 → 19.7 view h ≈ 23 s at 16.7 s a view hour)

| Beat | What | Time |
|---|---|---|
| 1 Hush | the teller lifts both hands, palms out (arms x 120, z ∓30, 0.6 s), and waits; the kids shuffle in 0.03 u | 2 s |
| 2 The voices | two voices in turn, 3 s each. **The big one:** tall (squash 1.08), chin up +10°, arms wide (z ∓70), a slow heavy bob 0.015 u at 1 Hz, turned yaw +25°. **The small one:** low (squash 0.85), lean 15, hands together at the chest (x 90), head tilted (roll 12°), a quick bob 0.008 u at 3 Hz, turned yaw −25°. Each switch is a 0.3 s `Snap` turn | 6 s |
| 3 The wind | "And then the wind said... no, wait. Let me do the wind." The teller stops (`Freeze` 0.4 s), wags a finger (arm x 110, z ±15 at 4 Hz, 0.6 s), then sweeps both arms over the head (z ∓150 ↔ ∓110 at 0.8 Hz) with a body sway (roll ±8°) for 2.5 s. The kids copy with their arms up, swaying (roll ±5°) a beat late | 4 s |
| 4 The voices again | one more round of the two voices | 3 s |
| 5 The scary part | "This next part is the scary part. Come closer." The teller crouches (squash 0.8, lean 25) and beckons (arm x 80 → 110, twice, 0.5 s each); the kids scoot in 0.04 u and press together (0.04 u apart); a grown-up puts an arm round the nearest kid (B55.b's arm). Still for 1.5 s. Then the jump: arms flung up (z ∓160), hop 0.04 u, 0.12 s `Snap` → `hit`: the kids `Flinch` back 0.03 u, then laugh (`Laugh` 0.6 s) | 5 s |
| 6 End beat | the hands come down slowly, palms down (arms x 60 → 30, 1 s), then a `Bow` (0.6 s); the grown-ups nod (B7.a's nod, twice); one kid throws an arm up (x 170) and hops: again! | 3 s |

| | |
|---|---|
| How it reads on a phone | one standing doll over a row of seated heads: tall, then small, arms over the head for the wind, the crouch and the jump. The kids' flinch runs through the front row. The voices' height change is 0.023 u (6 px) plus the arms: check it at the default zoom, and make it squash 1.12 / 0.8 if it doesn't read |
| Cheat | R "Story tonight: slow" |
| Reuse | R `TellFrom`, `TellSlowUntil`, `ListenToTheTeller`, `PeopleVoice.TellerLines`, `Freeze`, `Flinch`, `Laugh`, `Bow`, B7.a's nod, B55.b's arm, B9.c. N `View/Beats/TellerBeats.cs` (the two voices, the wind, the crouch and jump, the kids' copy and scoot), `DollStyle.Tell` |
| AnimLab | `717-teller-slow.json` (the teller, the fire keeper, 3 kids, 3 grown-ups) |

##### B76.b Short, and gets a laugh (18.3 → 18.85 view h ≈ 9 s, then the laugh, 0.15 h ≈ 2.5 s)

| Beat | What | Time |
|---|---|---|
| 1 Listen | one arm up, a finger raised (x 140, 0.4 s); a quick look round (yaw ±30°, 0.6 s) | 1 s |
| 2 Quick telling | B7.a's talker beat at speed: a beat every 0.8 s, an open hand to the left, then the right, bob 0.015 u | 4 s |
| 3 The woodpile | only where the woodpile stands: the teller mimes sitting down (squash 0.75, 0.3 s) and jumping up (hop 0.06 u, arms up x 160, 0.12 s `Snap`), then points at the woodpile (`Point`, arm x 90); every listener's head turns to it (0.3 s, 0.05 s apart). "So nobody sat on the woodpile again. The end." No woodpile: two more talker beats | 2 s |
| 4 The end | both arms out wide, palms up (z ∓80, x 60), a quick bow (0.4 s) → `hit`: the listeners laugh (`LaughPoint` → `Laugh`, as built); the kids rock back (lean −20, 0.4 s) | 2 s |
| 5 End beat | a hand on the chest (arm x 100, z 20) and two slow nods, mock-serious: "That's the whole story. It's true, too."; a second, bigger laugh (bob 0.02 u); the teller laughs too | 2.5 s |

| | |
|---|---|
| How it reads on a phone | a short burst of big gestures, then the whole fire laughing at once; the heads turning to the woodpile point the eye at it |
| Cheat | R "Story tonight: short" |
| Reuse | R `TellShortUntil`, `LaughHours`, `LaughPoint`, B7.a's talker, `Point`, `Bow`, `PeopleVoice.TellerLines` (the woodpile line gated as built). N the mime and the head-turn wave (`TellerBeats`) |
| AnimLab | `717-teller-short.json` (the same cast, the woodpile in view) |

**Ask:** on evenings nobody was picked, the eldest still tells the winter story (`StoryTell`, `DollStyle.Speech`). Give that
teller the slow manner's gestures too, or keep `Speech` so only the Keeper's pick tells it this way? My pick: keep `Speech`.

#### B77. The pond family's boat (W714.i; approvals row 216)

The boat lies upside down by the fire, flat, apart from the stew pot (decisions.md 2026-11-15, the boat row; the model is
Kenney's rowing boat, `ConsequenceMeshes.KenneyBoat`). The callback says the children use it as a house. Nobody uses it today,
so it needs a work use and a life use (people use everything).

| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B77.a W Sort the catch on it | a fisher back from fishing (B8), the pond family's first; **often** on fishing days | lays the `Rod` along the hull (arm x 120 → 90, `Pop`; the rod rests on the keel), sits on the hull's end (+0.04 u), sorts the catch from the `FishBasket` (`SortBasket`, 4 s), takes the rod and goes on to the rack. Reads: a doll sitting on the boat with a basket, a rod across it | R `Rod`, `FishBasket`, `SortBasket` (`View/Beats/ChoreBeats.cs`), `Sit` | **"Boat: sort the catch on it"** |
| B77.b L The kids' house | 2–3 kids, by day; **sometimes** | one kid crouches behind the far side (squash 0.7); another walks up and knocks on the hull (`Knock`, arm x 90, two taps → `hit`, the boat `Jolt`s 1°); the hidden one pops up over the keel (`Peek`, +0.04 u) and waves them round; they sit on it side by side (+0.04 u), swinging (roll ±5° at 1 Hz), 6–10 s. In rain they crouch in its lee (as B54.d's shelter). Reads: a kid knocking on the boat, a head popping up behind it | R `Knock`, `Peek`, `Sit`, `Wave`, B54.d | **"Boat: kids play house"** |

AnimLab: `714-boat-sort.json`, `714-boat-house.json`. Kids safe: the boat lies on clear ground off the mud
(`SimCore.ConsequenceProps`' boat spot); the kids stay on land beside it, and D's water and fire rules hold.

---

### Batch 3: assets needed

Every new thing is **built in code** in the house style (`MeshBuilder`, the box's `Palette`, low-poly, no textures), like the
props, the dolls and the quadruped today; no pack is needed. The CC0 packs below are fall-backs to look at for shape, only if the
user wants a model instead (free only, KayKit / Kenney, logged in `docs/assets-log.md`; "check" = confirm the pack has it before
any import).

| Asset | Spec | Size (u) | Source | Licence |
|---|---|---|---|---|
| Ember tip / torch flame on a stick | B29.b, B52.a, B55.a | 0.012 / 0.03 | built in code | — |
| Spit (2 forked sticks, a bar), rabbit on it, deer haunch, meat piece | B29.d (and B13.c) | 0.12–0.22; 0.05; 0.07; 0.02 | built in code (fall-back: Kenney Food Kit, check) | CC0 |
| Ladle | B30.b | 0.09 | built in code | — |
| The pot's second spot (cold, beside the fire) | B30.e | — | built in code (the pot is R) | — |
| A single feather; the pole drawn by feather count | B31.a | 0.06 | built in code (the pole's feather, R+) | — |
| Flower, bunch of 3, flower crown | B33.c, B53.c | 0.04 / 0.05 | built in code (R if `PairBeats`' flower is a mesh) | — |
| Grass bits on a head | B33.a | 0.008 | built in code | — |
| Stones in the arms; a sapling's stone ring | B34.b | 0.02 each | built in code (R+ the Keeper's ring) | — |
| A planted twig | B34.c | 0.05 | built in code (`Twig`, R) | — |
| Two sitting boulders at the camp's edge | B35.a | 0.07 h × 0.12 w | built in code (fall-back: Kenney Nature Kit rocks, check) | CC0 |
| Pebbles; a pebble stack by count | B35.b, B57.f | 0.012–0.02 | built in code | — |
| A branch held as a horse (a new hold) | B36.b | — | `Branch` (R), a new prop pose | — |
| A frog in cupped hands | B37 | — | the pond's frog (R) | — |
| A rabbit kit | B39.a | 0.6 × the rabbit | `RabbitDoll` at a smaller scale (R+) | — |
| Meat strips on the rack | B42.a | 0.06 × 0.015 | built in code | — |
| A toy figure, a bead string at a tent | B40.b | 0.02–0.03 | built in code | — |
| A lantern on each tent's pole | B52.a | 0.03 | built in code or the `Lantern` piece scaled (R+) | — |
| Moth specks (a puff kind) | B52.b | 0.016 | built in code | — |
| A leaf boat | B45.b, B59.j | 0.03 | built in code | — |
| A reed bundle | B47.a | 0.12 | built in code | — |
| A little pool behind the dam | B48.b | r 0.06 | built in code | — |
| A gift at the stone (flower, bowl, pebble) | B51 | — | R (`Bowl`, the flower, the pebble) | — |
| A weed with its root | B53.a | 0.04 | built in code | — |
| Choice props pulled apart into pieces (baskets, staff as a hand prop) | B54.b, B54.l | — | R+ `ConsequenceProps` meshes | — |
| A short ladder | B54.p, B57.d | 0.18 | built in code | — |
| A goat in the pen (only if the user wants one) | B54 note | P0 size | the P0 quadruped (`QuadDoll`, a new look) | — |
| Flour blotches, blue hand prints | B54.o, B59.h | dots | built in code | — |
| Scarecrow | B57.b | 0.2 | built in code | — |
| Home extras (flower box, painted door, herbs) | B57.d | 0.04–0.08 | built in code (fall-back: KayKit Medieval Hexagon details, already in the project) | CC0 |
| Hopscotch grid mark | B58.f | 0.15 | built in code | — |
| Bee speck | B58.e | 0.012 | built in code | — |
| Apple | B59.c | 0.02 | built in code (fall-back: Kenney Food Kit, check) | CC0 |
| Side-view fish (`FishSide`) | B70.c | 0.065 | built in code (fall-back for the silhouette: Kenney Fish Pack, 2D, check) | CC0 |
| Fish shadow (`FishShape`) | B70.b | 0.045 | R (`LifeLooks.FishShape`) | — |
| Nest, eggs, twig in the beak, chicks (option) | B71 | r 0.045; 0.018; 0.02; 0.015 | built in code | — |
| Sound: a grass/reed squeak | B33.d, B47.b | — | none found on Kenney yet; fall-back: the `Flute` cue pitched up | CC0 if found |
| Sound: a wood knock on the sticks | B36.a | — | Kenney Impact Sounds (`impactWood_light`, already in the project) | CC0 |
| Sound: bird chirps | B71 | — | `BirdSong` (R, in code) | — |
| Purple hand dots (a coloured hand dot, shared with B59.h's blue) | B72 | 0.012 | built in code | — |
| A small basket (the kids') | B72, B75.a | × 0.6–0.7 of `Basket` | R+ `Basket` scaled | — |
| A thatch bundle on the woodpile's roof | B73 | 0.06 | R `PropKind.Bundle` in straw colour | — |
| The long table by boards; a new board | B74 | 0.44 + 0.04 a board (max 0.92); board 0.04 × 0.1 | R+ the long table mesh; board built in code | — |
| The offering basket as a carried prop; its spot at the tree; the empty look (cloth flat) | B75 | the prop's size | R+ `ConsequenceMeshes` "offering_basket" | — |

---

### Batch 3: open questions for the user (also at the end of the approvals page)

1. **Skip stones** (B45.a): you dropped "throwing pebbles in the pond", but #692 still lists "skip stones". Keep it?
2. **Pebble down the well** (B57.f): #695 keeps it. Keep it?
3. **The swing** (B53.d): there is no hanging vine now (folded into the overgrowth). Drop it, or a rope swing on a wild tree?
4. **The nest's bird and the budget** (B71): outside `AnimalBudget` (as the ★ proposal said), or one slot from the frogs?
5. **Chicks** (B71.a): two chicks for the last days of spring, or the eggs simply gone when the bird leaves?
6. **Which thicket** holds the nest (B71): the first one (my pick), or the biggest?
7. **A goat in the goat pen** (B54): no goat (my pick), or a goat that takes a budget slot?
8. **People pull weeds in the camp** (B53.a): only at the tent doors, the fire and the camp's paths, so the Keeper's tweezers keep
   the rest. Right?
9. **Sitting rocks** (B35): two new boulders at the camp's edge. OK, or only the Keeper's outcrops?
10. **The quiet visit as a held moment** (B32.d): it waits for you up to two dusks. Or let it play whenever it falls?
11. **Feather colours** (B31.a): white newborn, red marriage, ochre village. OK?
12. **A kid on a dog's back** (B56.d): fine, or too silly?
13. **How often** (D): "often / sometimes / rare" as defined. Any thing you want seen more?

Answered 2026-11-15 (decisions.md "Batch 3 approvals"). Added with S-ritual (approvals rows 228–229):

14. **Who walks the offering to the tree** (B75.a): everyone, or the lead and the kids? My pick: the lead, the kids and up to
    4 grown-ups in the camp and village; the whole town behind the priest.
15. **The unpicked evening teller** (B76): give the eldest's winter story the slow manner's gestures too, or keep `Speech`?
    My pick: keep `Speech`.

## B78. New events (#722): the first three, for sign-off (2026-11-24)

Draft specs for the three events suggested first in `docs/design/new-events.md` (§1.1–1.3). Each one is staged by
CampLife (a `CampScene`, like the rituals' uses). Each gets a cheat and an AnimLab clip, and is built only after the user
picks it. Every number is [est] until the clip.

| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B78.a The tree's first fruit | the nearest free kid, a summer day once the Keeper's tree is grown; **once** a box | the kid stops under the tree and looks up (`LookUp`, 1 s); goes up on their toes (+0.02 u, arms x 150) and picks the one red fruit (`Pick`, `hit` → the fruit leaves the branch); runs to the fire holding it up (both arms x 150, `KidRun`); at the fire it goes round: each grown-up there bends and takes a bite (`Nod`, 0.6 s each), the last bite to the youngest. Reads: a kid holding up something red, people leaning in to it | `Fruit` (new: a red sphere 0.018 u, a leaf); R `LookUp`, `Pick`, B75.b's held-high run | **"Tree: first fruit"** |
| B78.b Two marks on the tree | the newly-weds, the dusk after a wedding while the tree is grown; **once** per couple, up to 8 pairs | the couple walk to the tree side by side; one cuts a short mark at hand height (arm x 70, the `Knife`, three small strokes → `hit` each, a `Chip` puff); the other cuts one beside it; they step back together and look at the two marks (`Stand`, 1.5 s); kids watch from a step back (`Peek`). Reads: two people at the trunk with a small knife, then two pale notches | `TreeMark` (new: a pale notch 0.012 × 0.004 u on the bark, a pair per couple, stacked); R the knife and the strokes from the name cut (#693) | **"Tree: a couple's marks"** |
| B78.c The second stone | two grown-ups and the family of the one lost, when the name stone is full (nine names) and a tenth is due; **once** | two grown-ups roll a rough stone in from the camp's edge (both push, lean 25°, the stone turns a quarter each 0.8 s, `Push`); they stand it upright beside the first (a heave, `Lift`, the stone `Jolt`s up); the cutter cuts the new name (the runestone's cut, #693 `NameCut`); the family stands round with heads down (`Mourn`, as B32.a). Reads: a stone rolled in, set up, a name cut | `Runestone` (the #693 runestone, a second one, a different seed); R `NameCut`, `Push`, B32.a's mourners | **"Stone: fill it"** |

**Assets needed:**
- `Fruit` and `TreeMark`: built in code in the house style.
- The second stone: the existing runestone mesh with another seed.

**Kids safe:**
- the fruit is picked from the ground, never by climbing;
- the knife stays with the grown-ups;
- the stone is rolled only by grown-ups, with the kids kept back 0.3 u.

## B80. New events (#722), batch 2

The one new scene of the second batch (`docs/design/new-events.md` §5.1). It is staged by CampLife (a `CampScene`, as B78) as the
held Story moment of its line. The other batch-2 events reuse what exists: the four asks (4.1–4.4) are the choice scene; the
Keeper's child's wedding (5.2) is the wedding moment with its line on the caption; the singer's song (2.2) is a card until its own
spec. Every number is [est] until the clip.

| # | Who · when · how often | Beats | Props · reuse | Cheat |
|---|---|---|---|---|
| B80.a The Keeper's child comes of age | the child the Keeper named, the year they turn grown-up (with work), the eldest free grown-up as the elder, up to 3 free people at the fire watching; **once** per person | the two come to the fire's camera side and stand face to face across the camera, 0.22 u apart (the elder carries the tool); the elder nods (0.8 s); B7.b's hand-over (`Giver` / `Taker`: held out, the hands meet, the tool changes hands at the contact → `hit`, the taker lifts it to look); the child holds it up (right arm x 150, a small hop 0.015 u, 1.4 s) while the elder and the seated watchers nod; the child walks off with it toward work, the elder sits down at the fire (1.5 s). Reads: an old one gives a young one a tool at the fire, and the young one holds it up | the job's tool, already in the prop kit: `Rod` (fisher), `Axe` (woodcutter, hunter, toolmaker, mason, smith), `Basket` (every other job); R B7.b `PairStage.HandOver`, `NodPose`, `Sit` | **"People: the Keeper's child grows up"** |

**Assets needed:** none (the tools are the existing props).

**Kids safe:** the tool goes from a grown-up to a grown-up (the child has just come of age); the kids watching sit at the fire.
