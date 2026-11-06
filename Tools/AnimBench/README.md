# AnimBench: animation timing and staging without the Editor (#352)

AnimBench steps the game's own pose code (`View/DollMotion.cs`, the beat kit in `View/Beats/**`, the `Life` and `Sim` types)
outside Unity, frame by frame, the way `PeopleView.Drive` drives it. It draws what happened as a contact sheet, curves
and short videos, and checks research 17 §4's principles. It never opens the Editor, so any number of agents can use it
at once, in any number of worktrees.

It shows timing and staging, not the final look: peg dolls in flat palette colours through an orthographic camera at
`BoxCamera`'s default yaw 28 and pitch 34. The Editor (T2 AnimLab) has the final say on the look.

## Use it

```bash
bash Tools/AnimBench/run.sh hunt-strike            # a clip in Tools/AnimBench/clips/, or a path to any clip.json
bash Tools/AnimBench/run.sh my/clip.json --no-video # sheet + curves only (~2.5 s)
bash Tools/AnimBench/run.sh hop-circle --strict     # exit 3 if a principle fails that the clip doesn't list in knownGaps
dotnet test Tools/AnimBench.Tests                   # the harness, the principles over every clip, the beat kit's tests
```

Output goes to `Tools/AnimBench/out/<clip file name>/` (gitignored):

| file | what |
|---|---|
| `sheet.png` | **Look at this first** (Read it). Up to 24 frames (4×6, ≤ 1560 px wide). Each cell: the default camera on top and a level side view across the action below. Red frame + red ring at the tool tip = `DollPose.hit`. Blue frame = the target answers. The grid is 0.1 u cells. The header lists every principle's result. |
| `curves.png` | Key channels over time, per doll (armR.x, armL.x, lean, offset.y, yaw, squash, speed). Around each hit it shades **wind-up / top hold / strike / contact / hold / follow-through**. It flags a **wind-up POP** (the arm jumps to the top in one frame) and **no hold**. Below: each object's speed and act changes (rabbit Graze → SitUp → Flee). |
| `principles.txt` | The principle checks in words (also in `track.json`). |
| `track.json` | Every channel per frame, as columns: per doll `style, seg, x, z, yaw, bodyYaw, offset, lean, roll, squash, armL, armR, extend, hit, walking, speed, progress, since, step, hunt, prop, handR, handL, tip, head`. Per object: `pos, yaw, act, visible, phase` plus state (e.g. `berries`). Also `hits` and `principles`. |
| `render.json` | The parts' world frames, for `render.py` only. |
| `clip.mp4`, `clip.gif`, `clip-0.25x.mp4`, `clip-0.25x.gif` | Default view and side view side by side, at 1× and at quarter speed. For the user and the review page. |

Timing, measured 2026-11-06 on this machine: about 4 s for a full run (simulate in about 0.1 s, sheet and curves in about 0.5 s,
video in 2 to 3 s). About 2.5 s with `--no-video`. A rebuild after a source change adds about 1.5 s. The first build ever
takes about 10 s.

## Make a clip

A clip is plain JSON with no comments: T2 AnimLab reads the same files in the Editor. Positions are box units, world
x/z (the site grid, the same as the planner's). Times are clip seconds. `hours` converts at `secondsPerViewHour`.

```jsonc
{
  "name": "hunt-strike",               // label (the out dir uses the FILE name)
  "notes": "what this clip stages and why",
  "duration": 11.5, "fps": 30,
  "warmup": 1,                         // seconds simulated before frame 0 (speeds, blends and rabbits settle); default 1
  "clock": 0,                          // the pose clock at frame 0 (PeopleView's poseClock); default 0
  "secondsPerViewHour": 16,            // for "hours": the morning is 16 (LifeClock: 80 s for 5 h)
  "camera": { "view": "default", "focus": [0.15, 0.05], "span": 1.5, "yaw": 28, "pitch": 34, "sideYaw": -15 },
                                       // focus + span (world width shown) or auto-fit; sideYaw default: across the first hit
  "sheet": { "from": 5, "to": 10, "frames": 24, "columns": 6 },  // every hit's first frame is pulled onto the sheet
  "expect": { "reactSeconds": 0.2, "neighbourRadius": 0.6,
              "arcs": [ { "kind": "spear", "by": "hunter", "target": "rabbit" } ] },
  "knownGaps": ["react", "arcs"],      // principles this clip fails TODAY (tests report them as Skipped)
  "objects": [ ... ],
  "dolls": [ ... ]
}
```

**Dolls.** Put fields at the doll level for a single stretch of time. Use `timeline` for a sequence. A segment takes the
doll-level fields as defaults and can override any of them.

| field | meaning |
|---|---|
| `id`, `role` | names (role is free text) |
| `kid` | kid scale 0.68 (`PegDoll.KidScale`) |
| `seed` | look and phase; `phase` defaults to `Hash01(seed, 5) * 37` s, as in PeopleView |
| `phase` | set the doll's own clock offset directly |
| `camp` | camp look (hide tones, headband); default true |
| `activity` | an `ActivityKind`: style from `DollMotion.StyleOf`, loop steps from `ActivityInfo.LoopSteps` |
| `style` | a `DollStyle` directly (overrides `activity`) |
| `hunt` | a `HuntPhase`: `DollMotion.Hunt` lays the strike or the bolt over the pose, as PeopleView does |
| `pos` / `from`, `to` | where it stands, or a straight walk from → to over the segment. Speed decides walking exactly as in PeopleView: above 0.05 u/s a non-Hop move becomes `Walk` (`Run` for kids at Play) |
| `ring` | `{center, r, slot, count, h}`: a slot on a ring as `PeopleView.SlotSpot` places it at the fire, green or market (angle `h + slot·2π/max(count,8)`, facing the middle) |
| `face` / `facing` | `[x, z]`, a doll or object id, or `facing`: a yaw in degrees. When it walks it faces the way it goes, except a hunter facing its prey |
| `prop`, `slot` | a `PropKind` and a `PropSlot` (default Hand). Hand tools tilt and extend as in `PegDollView` |
| `pace`, `count`, `step`, `steps`, `stepSeconds` | `Evaluate`'s inputs. `step` defaults to `(since / stepSeconds) % steps` (planner `StepHours` 0.25 h) |
| `partner`, `copiesPartner` | the chat turn offset (+0.65 s for the higher seed) and the apprentice's −0.35 s |
| `target` | the object or doll this segment's hits are aimed at (principles 1 and 2) |
| `endBeat` | mark this segment as the end beat of the loop before it |
| `hidden` | not drawn |
| `t` / `dur` / `hours` / `until` | timing. Without `t` a segment starts where the last ended. Without an end it runs to the next `t` or forever |
| `label` | shown on the curves |

**Objects.** `id`, `kind`, `pos` (`[x, z]` or `[x, y, z]`), `radius` (has a per-kind default), `yaw`, `size` (water `[w, d]`)
and `state` (free numbers). These kinds are drawn: `rabbit`, `bush`, `tree`, `fire`, `water`/`pond`/`puddle`, `spear`,
`ball`, `berry`, `fish`, `basket`, `tent`, `stash`. Any other kind is drawn as a grey dot.

- `rabbit` runs the real `Sim.Eco.RabbitHerd.Scare` against every visible doll. Its schedule is a fixed grazing spot,
  all green. `"hunted": {"by": "hunter", "until": t}` is RabbitView's rule: it ignores everyone until the hunt's resolve
  time, and only sits up with the hunter within 0.8 u. `"scared": false` makes a prop rabbit.
- `bush`: `"berries": N`. Add `"pickedBy": id, "dropEvery": 5` to drop a berry every 5 s while that doll picks (BushView's
  rule today).
- `"arc": {"from": "kid.hand" | "hunter.tip" | id | [x,y,z], "to": id | [x,y,z], "t0", "t1", "height", "heldBefore"}`
  is a parametric arc (research 17 §6.4). The start and end are fixed at `t0`. Before `t0` the object rides on its
  `from` anchor. Used for a thrown ball, spear, berry or fish.

## The principles (research 17 §4, without pixels)

| id | passes when |
|---|---|
| `hitNear` | on every `hit`, the tool's working end (or the hand) is within one doll-width (0.11 × scale) of the target's surface; a hit with no `target` fails |
| `react` | the target visibly changes (moves, act, turn, state, appears) within `reactSeconds` of the hit, and was **not** already changing in the 0.2 s before |
| `endBeat` | every work or play loop (Chop, Pick, Hop, Talk, Crouch…) is followed by an end beat (Strike/Bolt, HoldUp, Cheer, Bow, Flinch…, or `endBeat: true`), not by the clip's end |
| `neighbours` | at no moment do 3+ dolls within `neighbourRadius` (linked: a ring counts) share one move. Rest and walking don't count |
| `arcs` | every expected arc exists, starts within a doll-width of the thrower's hand and lands within one of its target |

A clip lists what it fails today in `knownGaps`. Then `dotnet test` reports those as **Skipped, "KNOWN GAP"** and keeps the
suite green. When a gap closes, the test fails and asks you to remove the gap from the list, so the list stays true.
Everything else must pass.

## How faithful is it

Exactly the game's code: the poses (`DollMotion.Evaluate`, `Blend`, `Hunt`, `StyleOf`, `Cadence`), the beat kit, the
rabbits' `Scare`, `ActivityInfo.LoopSteps`. Copied from `PeopleView`/`PegDollView`/`PegDoll`, because those are
MonoBehaviours or build meshes: the walk thresholds, turning, the 0.16 s blend, phase offsets, the doll's proportions,
slot poses, tool tilt and grip, prop tips. If you change those in the game, change `src/Bench.cs` too.
Not modelled: crowd easing and catch-up glides (the doll stands on the clip's point), slot search, terrain height
(flat ground at y = 0), the rabbit's wandering day, lighting.

## Extending

- New pure animation code in `View/Beats/**` or `View/Anim/Core/**` compiles in automatically. Keep it free of
  `UnityEngine` beyond `Shims/UnityMath.cs`: no MonoBehaviour, Transform or Mesh. If a compile error asks for a math
  member, add it to the shim, matching Unity's documented behaviour.
- A new object behaviour goes in `Simulator.StepObject` (`src/Bench.cs`). Its drawing goes in `Scene.object_prims`
  (`render.py`).
- A new principle goes in `src/Principles.cs` (add it to `All`). Test it in `Tools/AnimBench.Tests/PrincipleTests.cs`
  with one clip that passes and one that fails.

## Gotchas

- Run it from Bash. It uses `/c/Python311/python` (override with `ANIMBENCH_PYTHON`). It finds ffmpeg on PATH, then
  under winget `Gyan.FFmpeg`, or uses `ANIMBENCH_FFMPEG`.
- Everything it writes stays inside its own worktree (`bin/`, `obj/`, `out/`), so two worktrees can run at once. In one
  worktree, two runs of the **same** clip write the same out dir. A build in one worktree takes a lock in that
  worktree's `obj/` only.
- The 0.25× gif runs at 7.5 fps. Frames are rendered in parallel (`ANIMBENCH_JOBS`, default cores − 1, at most 8).
