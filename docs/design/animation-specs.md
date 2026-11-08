# Animation spec sheets: B1–B26, the staged moments and placed life (A3, #356; P9, #416)

Status: **draft, waiting for the user** (2026-11-06). One beat sheet per animation of the Visit loop. Design:
[visit-loop.md](visit-loop.md) §2.6 and §4; research [17](../research/17-readable-life-animations.md) §4 (the eight
principles, P1–P8 below) and §5–6. Every number is [est] until it has been looked at in AnimBench and in an AnimLab clip.
**P9 (2026-11-08):** B12–B26 and the shared quadruped (P0) cover the Placed life milestone (#424–#446), in the part
"Placed life" below.
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
