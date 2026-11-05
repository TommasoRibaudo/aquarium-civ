# How the slice work is defined, tracked and run (2026-10-29)

Decided by the user 2026-10-29 (decisions.md). Replaces "issue tracker deferred until production" (2026-10-28).
The design docs stay the **why** (game-definition, decisions, vertical-slice-plan, research); issues are the **what**.

## 1. A work item
One work item = **one GitHub issue**, small enough for one agent in one sitting (≤ ~1 agent day). Template:
[.github/ISSUE_TEMPLATE/work-item.md](../.github/ISSUE_TEMPLATE/work-item.md).

| Field | Meaning |
|---|---|
| Why | Link to the game-definition section, decision or research doc |
| What | 3–6 lines; what's in, what's out |
| Touches | Folders/assemblies the agent may change. Two parallel items must not touch the same files |
| Unity | none / Editor / play mode. Decides the lane |
| Done when | Testable checks; tester agent for anything visual/UI/gameplay; user sign-off only for look/feel calls |
| Depends on | Other issues |
| Needs the user | A decision only the user makes (also the `needs:user` label) |

Slice milestones (M2 … M8, [vertical-slice-plan.md §7](design/vertical-slice-plan.md)) are **GitHub milestones**. A
milestone is done when its issues are closed and its own "done when" is met.

## 2. Where it's tracked
- **Board:** [Aquarium Civ Slice](https://github.com/users/TommasoRibaudo/projects/18) (Backlog → Ready → In progress →
  Review → Done). Tester feedback keeps its own board (Playtest Feedback).
- **Labels:** `type:feature|bug|spike|research|tooling`, `lane:sim|view|ui|docs`, `needs:editor`, `needs:user`.
  Filter `label:needs:user` to see everything waiting on the user.
- **Ready** means: briefed with the template, dependencies done, no open `needs:user`.

## 3. Branches
- `vs/slice` = the **integration branch** for the slice (worktree `F:\GameDev-slice`). The orchestrator merges finished
  work here (user approval 2026-10-29). `main`/`dev` stay PR-only (docs/ci.md).
- One branch per issue: `vs/<issue#>-<slug>`, from `vs/slice`, in its own git worktree for lane A work.
- Never commit `AquariumCiv/AquariumCiv.slnx` or `AquariumCiv/Assets/Art/Materials/Glass.mat`.

## 4. Lanes: what can run in parallel
**The bottleneck is the single Unity Editor**, not agents: one Editor per project folder, and two agents driving it
fight over compiles, domain reloads and the open scene.

| Lane | Work | Parallel? | Test with |
|---|---|---|---|
| **A: no Unity** (`lane:sim`, `lane:docs`) | Sim logic (`Assets/Game/Sim`, pure C#), research, design docs | **Yes**, 3–4 agents, one worktree each | `dotnet test Tools/SimTests` (seconds, no Editor) |
| **B: Editor** (`needs:editor`: `lane:view`, `lane:ui`) | Scenes, art, shaders, UI, play mode | **One at a time** (Editor lock) | Unity tests, screenshots, tester agent |

- `Tools/SimTests` compiles the same `Assets/Game/Sim` files plus the pure sim test classes against .NET 8 and NUnit (floats can differ slightly from Unity's Mono: never pin a float-derived hash in both, see tooling.md),
  with a tiny `JsonUtility` shim. Unity's own test run (and CI) stays the authority before a merge.
- New sim test classes that don't use `UnityEngine` (other than `JsonUtility`) go into `Tools/SimTests/SimTests.csproj`
  too.
- **Editor lock:** whoever drives the Editor writes `F:\GameDev\.agent-editor.lock` (owner, issue, start time) and
  deletes it when done; others wait. The orchestrator checks it before starting any lane B item.
- A lane B agent works in the main checkout `F:\GameDev` (the folder the Editor has open).
- **Editor health gate:** before taking the lock run `bash Tools/editor-health.sh`; exit 1 → `bash Tools/restart-editor.sh`
  (holding the lock, out of play mode), exit 2 → C: is nearly full, tell the user. Restart at least every 3 tickets. Measured
  2026-10-30: 28 h without a restart took reloads from 4 s to 95 s+ (3.3 h lost) and let 81 leaked glass sets pile up.
- **Phase 1 / phase 2:** when the Editor is busy, start a `needs:editor` ticket's pure logic as lane A (phase 1, dotnet
  tests), then wire it in the Editor (phase 2). The Editor is the bottleneck: it was busy 97 % of a 19 h run.

## 5. The loop
1. **Orchestrator** (the main session) keeps the board: picks Ready items, at most 3–4 lane A items and 1 lane B item
   at a time, whose `Touches` don't overlap.
2. Sends each to a **worker agent** with the issue as its brief (plus CLAUDE.md, which binds every agent).
3. Worker: branch + worktree, implement, tests, push the branch, comment on the issue with the result, move it to Review.
4. Orchestrator **reviews**: diff read, tests re-run, Unity tests for anything touching `Assets/`, tester agent on its
   cadence (CLAUDE.md, loop step 5). Then merges into `vs/slice` and closes with `Tools/close-issue.sh <n> "<note>"`,
   which also moves the card to Done. New issues go on the board when filed.
5. Anything only the user can decide → `needs:user`, and it's surfaced to the user, never decided by an agent.
6. **Every loop tick:** (a) fewer than 3 lane A agents running → `gh issue list --state open`, start the next Ready or
   Backlog items whose `Touches` don't overlap, and phase 1 of any `needs:editor` ticket; the user should never have to
   ask what can run in parallel. (b) `ListAgents`, and `TaskStop` anything finished that still looks active.
   (c) at the start of a run, merge `dev` into `vs/slice` first.
7. **Baseline first:** when a ticket's failures also fail on its base commit, they aren't the ticket's. Merge it, file
   one issue per failing class, and hunt them as their own ticket with a **90-minute time box** (report at 60). A 4 h 09 m
   hunt on 2026-10-30 held the Editor and delayed everything behind it.
8. Run the full Unity EditMode suite after every merge of sim work that touches `Assets/`, or at least every 4 hours:
   floats differ under Mono, and an 11 h 47 m gap hid failures.
- Large fan-outs (several items through implement → review → verify) can use the Workflow tool, only when the user
  says so ("use a workflow"): it's token-heavy.

## 6. Pilot (first run)
Two lane A items in parallel (M3b: notable jobs and roles; trait-coloured time in office) while lane B is busy
(Growth Lab holds the Editor). Compare wall-clock and review findings against single-agent work, then adjust this doc.

## 7. Two ways to run a batch: pick one per batch (2026-11-04)
Both follow §4–§5. They differ in **when the Editor sees the work**. Choose before you brief anyone, and write the
choice and the reason in the run's status file.

### Mode 1: the Editor queue (one Editor worker per ticket)
Each ticket's worker takes the Editor lock, builds, looks at its screenshots, runs its own tests and the smoke test,
and hands back verified. Lane A (sim) work runs beside it. The tester runs on cadence. A touch play-through comes at
the end.
- **Used for:** the P8 polish run (PR #302): 18 work items (~25 issues) in ~14 h, plus ~2.5 h of CI fixes. About
  35–40 min per issue. The Editor was the bottleneck: one ticket at a time, restarts every 3 tickets.
- **Why it works:** every view change is seen, played and fixed while its author still has the context. Look and feel
  problems surface per ticket, not all at the end. Mistakes don't pile up.

### Mode 2: parallel offline workers, then one batched Editor pass
Every worker runs at once in its own worktree and **never touches the Editor**. Each one compile-checks offline (csc
against the Unity DLLs, §4 / tooling.md 2026-10-29), runs `dotnet test Tools/SimTests`, commits and reports exactly what
to verify. The orchestrator merges each branch as it lands and runs the sim suite on the merge. Then it does **one**
Editor pass:
1. Recompile and read the console.
2. EditMode, one assembly at a time.
3. The affected PlayMode classes, plus the smoke test.
4. Fix the failures and re-run only those.
5. Screenshots, the tester, the touch play-through.
- **Used for:** the playtest-feedback batch (PR #326): 10 issues in **2 h 45 m** from issue creation to merge.
  - 7 Sonnet workers finished in ~45 min, in parallel.
  - The Editor pass took ~1 h, fixes ~30 min, two tester passes ~35 min, CI 33 min.
  - About 17 min per issue.
- **Why it works:** Editor time is the scarce resource (§4: busy 97 % of a 19 h run, mostly waiting on reloads), and
  this mode spends it once: one domain reload and one test batch instead of one per ticket.
  - Offline csc catches compile errors in seconds, and the sim and planner logic is fully tested by `dotnet test`. So
    what's left for Unity is integration, and integration is cheapest checked all together.
  - It also matches the user's rule to bunch tests and avoid reruns.
- **What it costs:**
  - Nobody sees a view change until the end. PR #326's pass found 3 bugs no offline check could see: a side pane faded
    at the default view, a fresh tap swallowed by a drag throttle, and a weed's tap area hiding a sapling.
  - The young tree stages were invisible at the default zoom, and only the second tester pass caught it.
  - Unity-only APIs slip through: Unity's NUnit has no `Is.AnyOf`, and that put the Editor in Safe Mode.
  - Seed-pinned tests move when merged branches shift the random stream.
  - Worker claims are unchecked until the pass. One worker reported a failure as already there when it wasn't; another
    said it had saved screenshots without running Unity.

### Choosing
| Use **Mode 2** (parallel, one Editor pass) when… | Use **Mode 1** (Editor queue) when… |
|---|---|
| The issues are small to medium, and logic-heavy or rules-heavy (sim, planner, audio scheduling, ordering, input rules, text) | The issues are judged by eye, ear or feel: a new asset or model, a moment, a camera feel, a UI look, a mix |
| The issues have separate `Touches`, so their merges don't fight | Several issues touch the same view files, or one feature is built across many tickets |
| You can say what "right" means in a test or a number | "Right" is a screenshot the user signs off on, and it will take a few iterations |
| Feedback batches, bug batches, cleanups, removals | New systems that need playing to tune (a hunt, a pond, the opening) |
| You want wall-clock speed, and the user is waiting | Correctness per step matters more than speed, or the Editor is already free and nobody else waits |

**Mixed batches (the usual case): use a hybrid.**
- Run the logic parts as Mode 2 workers, in parallel.
- Give each look item its own short Editor worker that iterates on screenshots, one at a time.
- Finish with the one batched pass over everything.

`docs/prompts/feedback-batch-2-run.md` is written this way.

### Mode 2 rules (learned in PR #326)
- **Briefs:**
  - Brief every worker from one shared rules file: no Editor, offline csc, tests, cheats in one block in
    `SimDebugPanel.cs` / `SliceDebugPanel.cs`, commit without push, and a final report under 400 words.
  - The report must name the exact Unity checks: classes to run, and what each screenshot should show.
  - Tell each worker what the others are touching right now.
- **Order of the pass:** merge as branches land, and run `dotnet test` on every merge. When one fails, run the class on
  each branch to find which one moved it. Don't take "it was already there" on trust.
- **Chains:** sequence workers that share files instead of running them together. For example, removing trash came
  before the tweezers, and the tweezers worker started from the merged branch.
- **The pass itself** is one test runner loop: start with `--async_tests true`, poll `test status` until `completed`,
  read the JSON. One assembly per EditMode run.
- **Never skip the touch play-through** at the end (memory: "playable, not green"). PR #326 skipped it, which was its
  biggest gap.
- **Sign-offs:** issues waiting for the user's look or listen stay open after the merge. The feedback notes they answer
  close only when their issue closes.
- **Testers:** give them narrow briefs with a time budget (about 45 min). A broad brief covered half the batch.
