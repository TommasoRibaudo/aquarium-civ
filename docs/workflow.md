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
