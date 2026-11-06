# Prompt: the Visit loop milestone (moments you see, choices you own, life you can watch)

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`, on Opus. The Unity Editor can
be open or closed. You (the session) are the **orchestrator**: you build the tools, plan, brief, review, merge, verify
and close. Workers do the issues. The run is long and must survive context compaction.

---

You're building the Visit loop milestone for Aquarium Civ. The design is signed off: `docs/design/visit-loop.md`
(read all of it), with research `docs/research/16-visit-loop-events-and-choices.md` and
`docs/research/17-readable-life-animations.md`, and the decisions row of 2026-11-06 in `docs/decisions.md`.
**Those are binding.** Don't reopen a decision they record.

The user's two priorities, in their words:
- **"The animation needs to be very well done."** Cute, not realistic, but every animation must show what is
  happening: the hunter really hunts and the rabbits really run; water is really drawn; berries are really picked;
  kids really play (in the meadow, the woods and the water), not "all the same circle".
- **"Use the Editor in a smart way… build the tools to see assets and animation for concurrency. It makes the
  difference."** The Editor is the bottleneck (workflow.md §4: busy 97 % of a 19 h run). This run is designed so that
  most animation work happens **without** the Editor, and the Editor's time goes to batched, high-value work.

Don't stop to ask, except where §6 says a look waits for the user. Make the sensible call, write it down and keep
going. Stop only for something that would break `dev`/`main`, publish anything, or that CLAUDE.md says to report.

## 0. Resume first
Read `docs/overnight/VISIT-LOOP-STATUS.md`. If it exists, continue from the first unchecked step and don't redo
finished work. If it doesn't, create it from the checklist in §9. After **every** finished step, add one line: the wall
clock, what was done, the commit, Editor minutes used, and anything odd. It's your memory across compaction.

## 1. Read before any code
- `CLAUDE.md` (every rule applies), `docs/workflow.md` (all of it, especially §4 lanes and §7 modes), `docs/tooling.md`
  (all of it; read closely the entries of 2026-10-29 on offline csc and edit-mode renders, and 2026-11-01 to 2026-11-05),
  `docs/game-design-practices.md`, `.claude/agents/tester.md`.
- The last run's prompt `docs/prompts/feedback-batch-2-run.md` and what it learned (tooling.md 2026-11-04 entries).
- The design and research above. Research 17 §2 is the inventory of every animation today with file:line refs;
  §4 the eight principles; §5 the upgrades; §6 the technical approach. Research 16 §4 the model and its numbers.
- The code: `Assets/Game/View/DollMotion.cs` (pure pose math: `Mathf`/`Vector3` and `AquariumCiv.Life` types, no scene
  access), `View/PeopleView.cs`, `Life/LifePlanner*.cs`, `View/ChatterView.cs` (one-mesh pattern), the rabbit and
  hunt views, `Sim/SimCore.Cards.cs` (`CardCap`), `Sim/SimCore.Dilemmas.cs`, `Assets/Editor/Agent/GrowthLabBuilder.cs`
  (edit-mode renders), `Tools/SimTests/SimTests.csproj` and `Tools/SimTests/Shims/`.

## 2. Guardrails
- **Git:**
  - Integration branch `vl/visit-loop` from `origin/dev`, in worktree `F:\GameDev-wt\visitloop`. Each worker:
    branch `vs/<n>-<slug>` from it, worktree `F:\GameDev-wt\<n>`. At the end, PR `vl/visit-loop` → **`dev`** (PRs into
    `main` only come from `dev`; CI's "Source is dev" check enforces it). Let CI pass, then merge.
  - **Never `git commit -a`**; stage by name. Never commit `AquariumCiv.slnx`, `Glass.mat`, `AquariumCiv/.utmp/`.
    Never push to `main`/`dev`, never force-push. Before the PR, check `git diff --stat origin/dev` for those files
    and for any `.unity`/`.prefab`.
- **Unity:** `--timeout 60` on short calls; long ones in the background with a check after a minute; dialog check
  first when anything stalls; never a bare `test run`. Use `Tools/run-tests.sh` (one filter per run, bounded waits).
  Editor lock + `bash Tools/editor-health.sh` + `bash Tools/watch-editor-stall.sh` started in the same message the
  lock is taken. Restart the Editor every 3 tickets or when the health check says so.
- **Known traps:** Unity's NUnit has no `Is.AnyOf`. Seed-pinned tests move when a change shifts the random stream:
  new randomness gets its own seeded stream. `DontSave` meshes need `Resources.FindObjectsOfTypeAll`. Don't kill
  runners from Bash with a `Get-CimInstance` filter that matches your own shell.
- **Playable, not green:** every player-facing piece works through the real HUD and real touches. Cheats only reach a
  state. **Debug panel stays current:** every new animation, game, moment and choice gets a cheat in the same change,
  in one block per worker in `View/SimDebugPanel.cs` / `View/SliceDebugPanel.cs`.
- **Closed-world writing:** the people live in the tank; no outside world in any line; every premise must be true in
  the box (playtest #346: "celebrating dead ancestors" when nobody had died).
- **Free assets only**, logged in `docs/assets-log.md`. **No publishing** (`ios-testflight.yml`). GitHub writes
  (`gh issue create/comment/edit`) may be blocked by the permission classifier: if so, collect the commands for the
  user and carry on; don't work around a block.

## 3. Phase T: build the tools first (they decide the whole run's speed)
Build these before any animation work, on `vl/visit-loop`, and record each in `docs/tooling.md` the moment it works.

### T1. AnimBench: animation without the Editor (lane A, any number of workers at once)
`DollMotion` is pure math, and `Tools/SimTests` already compiles `Assets/Game/Life/**`. So:
- `Tools/AnimBench/`: a .NET 8 console project that compiles `View/DollMotion.cs`, the new beat kit (A1) and the
  `Life` types against **small shims** for `Mathf`, `Vector3`, `Quaternion` (only what's used; put them next to the
  existing `Tools/SimTests/Shims/`, never in `Assets/`). Keep the shared code free of other `UnityEngine` calls so it
  stays compilable here; if a file must touch the scene, split the pure part out.
- Input: a scene file (JSON) per clip: dolls (style, start pose, target, role), objects (bush with N berries, rabbit,
  water edge, ball, spear, basket), a duration and fps. Output, under `Tools/AnimBench/out/<clip>/`:
  - `track.json`: every channel per frame (offset, yaw, lean, arms, prop, `hit`, object states);
  - `sheet.png`: a **contact sheet** (e.g. 4×6 frames, frame numbers, the `hit` frames outlined) from the default
    camera angle and a side view. This is what an agent looks at with Read;
  - `curves.png`: the key channels plotted over time, with wind-up, contact, hold and follow-through marked;
  - `clip.mp4` / `clip.gif` at 1× and 0.25× (ffmpeg is installed: winget Gyan.FFmpeg) for the user.
- The renderer: Python 3.11 at `/c/Python311/python` with Pillow (installed). Draw peg dolls the way the style kit
  builds them (body, head, stub arms, prop), flat palette colours, a fake 3/4 camera matching `BoxCamera`'s default
  pitch. It's an approximation for timing and staging; the Editor is the authority for the look.
- **Principle checks as tests** (`dotnet test Tools/AnimBench.Tests`): from research 17 §4, everything checkable
  without pixels: the `hit` frame falls when the tool is within one doll-width of its target; the target reacts within
  0.2 s of `hit`; every loop has an end beat; no 3+ neighbours share the same move in step; arcs (ball, spear, berry,
  fish) start at the hand and land on their target.
- Done when: one command renders today's `Hop` circle and today's hunt strike (as baselines to beat) in under 10 s,
  and a second worktree can run it at the same time.

### T2. AnimLab: the real look, in batches, in edit mode (the Editor holder only)
`Assets/Editor/Agent/AnimLabBuilder.cs`:
- Builds a **lab stage** with the real doll meshes, props and object views (bush, rabbit, pond edge, tent, tree) as a
  **grid of cells**, one clip per cell, so a single render shows many clips side by side (variants A/B/C next to each
  other, or every kids' game at once).
- Poses dolls from `DollMotion` / the beat kit at time t **in edit mode** (no play mode, no domain reload per clip) and
  renders with `cam.Render()` through URP post, as `GrowthLabBuilder.Render` does. Frames → ffmpeg → mp4/gif plus a
  contact sheet, at the default camera and close up, at 1× and slow.
- **A request queue, so workers never wait on the Editor:** workers drop the same clip JSON as T1 into
  `F:\GameDev\AquariumCiv\Temp\agent\animlab\requests\` (with their branch and commit); the Editor holder merges what's
  ready, recompiles **once**, and renders the whole queue in one go, chunked into `script run` calls under 60 s.
  Output goes to `Temp/agent/animlab/out/<clip>/`, and the holder tells each worker where.
- For what needs the live planner (a play group of real kids, a hunt with the real herd), a play-mode capture: a cheat
  starts the scene, `PeopleView.HandDriven` steps at a fixed 1/30 s, and frames are grabbed per step (see tooling.md
  2026-11-01). One play-mode session captures every such clip in the queue.
- Done when: the queue renders three clips side by side in one call, and the result matches T1's timing.

### T3. The review page (for the user's sign-off)
One private Artifact, "Animation review", updated in place (same file path) after each batch: per clip, the spec's
one-line read ("a hunter throws; the rabbits scatter"), the Editor clip, the AnimBench clip, the 0.25× clip, and the
status (draft / waiting for the user / signed off). Load the `artifact-design` skill before writing it. Give the user
the link and ask for sign-off there or in chat. Clips must be readable on a phone.

## 4. How the run uses the Editor (the plan, not a suggestion)
- **Lane A is the default for animation:** with T1, timing, staging and the beat kit are iterated by many workers in
  parallel worktrees, each looking at its own contact sheets and curves. They never touch the Editor.
- **The Editor runs in batches, not per ticket:** one holder, one recompile per batch. A batch = merge what's ready →
  recompile → console → render the AnimLab queue → run the affected test classes → screenshots. Run a batch when 3+
  requests are queued or every ~45 min, whichever comes first. Between batches the Editor holder works on the
  Editor-only looks (new meshes: the ball, spear, puff shapes, basket fill) one at a time, Mode 1 style.
- **Phase 1 / phase 2 for every `needs:editor` ticket:** the pure part (logic, poses, rules) is lane A with dotnet
  tests and offline csc; the wiring is a short Editor step inside a batch.
- Log Editor minutes per batch in the status file. If the Editor sits idle while requests wait, or a worker waits on
  the Editor for more than 20 min, change the plan and say so in the status file.
- **A second Editor is not part of this plan.** Don't open the project twice.

## 5. Models and agents
- Workers default to **Sonnet**, one issue each (or a chain on the same files), briefed from one shared rules file in
  your scratchpad (no Editor; offline csc; AnimBench; tests; cheats in one block; closed-world writing; commit without
  push; final report under 400 words) plus: the issue, worktree and branch, `Touches` as its fence, and what the
  others are touching right now.
- **Opus** for: T1/T2 design review, A1 (the beat kit), A3 (the spec sheets), B2 (play groups), C2 (the director),
  C3 (staged moments). Animation craft needs judgment; don't save money there.
- A worker's report names: branch and commit, files, cheats, test counts, **the AnimBench sheets it looked at and what
  they show**, the AnimLab requests it queued, risks. Treat claims as unverified until you've looked at the sheets and
  clips yourself. **Look at every contact sheet you pass on.**
- Every tick: merge finished branches into `vl/visit-loop`, `dotnet test Tools/SimTests` and `Tools/AnimBench.Tests`
  on the merge, `ListAgents`, `TaskStop` what has finished.

## 6. The quality bar for animation (every B issue, no exceptions)
1. **Spec first** (A3): a beat sheet per animation: actor, verb, real target, wind-up, contact, how the target
   answers, hold, follow-through, end beat, duration, and the 3-second read at the default camera with the sound off.
   Exaggerate 1.5–2×; cute, not realistic.
2. **AnimBench draft** of the spec, principle tests green.
3. **Spec + draft clip on the review page** → ask the user. While waiting, the worker or another goes on with lane A
   items; don't sit idle. If the user hasn't answered by the next batch, build to the spec and mark it "waiting".
4. **Build**, then the **AnimLab clip** at the default camera and close, in the real box.
5. **Tester** with research 17 §4's eight principles as its pass list, plus naming 5 random dolls from a screenshot
   and a 3 s look.
6. **The user signs off the clip.** Until then the issue stays open (`needs:user`), even when merged.
Still undecided, show the user on clips rather than deciding: hugs between peg dolls; a short hit-stop on key beats.

## 7. The work (file it first)
Create the milestone **"Visit loop"**, then file these issues with the work-item template (Why / What / Touches /
Unity / Done when / Depends on / Needs the user), linking `docs/design/visit-loop.md` and the research sections. Put
them on the Slice board.

| # | Issue | Lane | Size | Depends on | Notes |
|---|---|---|---|---|---|
| T1 | AnimBench (offline animation harness) | A, tooling | M | — | §3 |
| T2 | AnimLab (batched edit-mode renders + request queue) | B, tooling | M | T1's clip format | §3 |
| T3 | Animation review page | docs/UI | S | T1 | §3 |
| A1 | Beat kit: one-shot beats with easing, a `WorkHit` event from `DollPose.hit`, pooled arc objects, group and pair controllers with a shared clock | A → B | M | T1 | research 17 §6 |
| A3 | Spec sheets for B1–B11 | docs | S | — | `needs:user` |
| B1 | Contact puffs and target shakes | A → B | S | A1 | build first: B3–B8 reuse it |
| B2 | Kids' play 1: play groups; tag, a ball you can see, chasing the rabbits | A → B | M | A1, B1 | today: one `Hop` circle, the ball is never drawn (`PeopleView.cs:804`) |
| B3 | Kids' play 2: splashing at the pond and puddles, games in the woods, hide and seek | A → B | M | B2 | the user: "play in the woods and the water" |
| B4 | Drawing water: kneel at the real edge, dip, ripple, lift, walk home under the weight | A → B | S | B1 | |
| B5 | Berry picking you can see | A → B | S | B1 | fold into #288 |
| B6 | The hunt made watchable, **spear thrown** and sticking, nearby rabbits bolt, catch held up | A → B | M | B1 | fold into #283 |
| B7 | Talk and pairs: nods, open hands, handing over, walking together (hugs: show the user) | A → B | M | A1 | #327 or new |
| B8 | Fishing pays off | A → B | S | B1 | #286 or new |
| B9 | Meals and the fire | A → B | S | B1 | |
| B10 | Felling real trees | A → B | M–L | keepers-hands §8.3 | may wait |
| B11 | Background-life director: a story always in progress on screen, **scaled by population** | A → B | M | B2, B4 | the user: "depends on pop" |
| C1 | The choice fix: raised only while watching or on open; never lapses unseen; closes after shown on 3 visits; "Let them decide" credited to the Keeper; **paced by time, not opens** (≥ ~3 real hours between raises, one open at a time); cards 1/2/3 | A → B | S–M | — | start at once; touches #224 |
| C2 | Moment queue and director: rates by population, rest gaps, max hold 8 game years → card, a welcome moment on open; budget per watched minute | A → B | M | C1 | research 16 §4.2 |
| C3 | Staged moments: the wedding the people organize **because the Keeper showed up**, a newborn held up to the glass, the new chief, a festival | B | M–L | A1, C2 | #276 |
| C4 | Callbacks as live moments; every option leaves a thing in the box; "chosen by the Keeper" on the person card | A → B | M | C2 | #222 |
| C5 | Small choices: naming a notable's newborn first | A → B | M | C1 | |
| C6 | History: "The Keeper's choices" page, hand pins on the time-lapse | B | S–M | C1 | |
| C7 | Telemetry: moments seen per visit, gaps, recall of seen vs read | A | S | C2 | |
| D1 | Bug #347: tweezers fail through the glass | A → B | S | — | |
| D2 | Bug #337: pinch-zoom knocks the glass (a second finger cancels the pending tap; knocks now startle rabbits) | A → B | S | — | |
| D3 | Bug #336: zoom lurches up/down | B | S | — | link #273 |
| D4 | Bug #342: the Keeper's tree planted on the bed's edge | A → B | S | — | |
| D5 | Bug #350: misting brightens the whole land at dusk | B | S | — | measure as in tooling.md 2026-11-04 (#300) |
| D6 | Bug #346: the ancestors line with nobody dead; find the source, gate it on a real death | A | S | — | trinkets at the tree → #276/#317 |

Fold #334 and #349 into #332 with a comment. Answer the playtest notes #334–#350 with a comment naming their issue and
add `fb:triaged`; #341, #345, #348 point to this milestone. Notes close only when their issue closes.

**Order:** T1 + C1 + D1/D2/D4/D6 + A3 start at once in parallel (lane A). Then A1 → B1 → (B2, B4, B5, B6 in
parallel on AnimBench) while T2 is built in the Editor. Then the batches. C2 → C3/C4 once A1 and C1 are merged.
B7–B11, C5–C7 after. B10 may wait for the felling decision; say so.

## 8. Verification
1. Every batch: recompile, console (0 errors), the affected classes via `Tools/run-tests.sh`, the AnimLab queue.
2. At the end, one full pass: EditMode one assembly at a time (Sim, Life, View, Audio, ShowMe, Cards, Moments,
   Shaping, TimeLapseScreen, Game.UI; counts > 0), the PlayMode classes the workers named, `SmokePlay`, and
   `SlicePlaythroughSmoke`.
3. **The touch play-through** on a fresh living-land box at 1179×2556, only time-advance cheats: open after an away
   (a welcome moment plays; one choice is waiting and staged in the box; at most 1–3 cards); answer by touch; "Let
   them decide"; reopen 5 minutes later (no new choice); a hunt with a thrown spear; kids at play in meadow and water;
   water drawn; berries picked. Screenshot every beat and look at every shot.
4. **Tester** on its cadence (6 view tickets or 4 h of merged Editor work) and at the end, narrow briefs with a
   ~45-minute budget, research 17 §4 as the pass list, scene sanity (roots, mesh count). Fix every **[real]**.
5. Review page up to date; list what waits for the user's sign-off.
6. PR `vl/visit-loop` → `dev`, forbidden-file check, CI watched in the background with a timeout, merge when green.

## 9. Checklist (copy into VISIT-LOOP-STATUS.md)
- [ ] `vl/visit-loop` from `origin/dev`; milestone and issues filed; notes triaged
- [ ] T1 AnimBench: baselines rendered (Hop circle, hunt strike), runs in two worktrees at once; tooling.md entry
- [ ] T2 AnimLab: three clips side by side in one call; queue works; tooling.md entry
- [ ] T3 review page published; link given to the user
- [ ] A3 specs on the review page
- [ ] C1 merged · D1–D6 merged
- [ ] A1 · B1 merged
- [ ] B2 · B4 · B5 · B6 merged, clips on the review page
- [ ] C2 · C3 · C4 merged
- [ ] B7 · B8 · B9 · B11 · C5 · C6 · C7 merged (B10: built or deferred, with the reason)
- [ ] Final pass green; play-through done; tester [real] findings fixed
- [ ] PR to `dev` merged; sign-offs listed for the user
