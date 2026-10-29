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

- `Tools/SimTests` compiles the same `Assets/Game/Sim` files plus the pure sim test classes against .NET 8 and NUnit,
  with a tiny `JsonUtility` shim. Unity's own test run (and CI) stays the authority before a merge.
- New sim test classes that don't use `UnityEngine` (other than `JsonUtility`) go into `Tools/SimTests/SimTests.csproj`
  too.
- **Editor lock:** whoever drives the Editor writes `F:\GameDev\.agent-editor.lock` (owner, issue, start time) and
  deletes it when done; others wait. The orchestrator checks it before starting any lane B item.
- A lane B agent works in the main checkout `F:\GameDev` (the folder the Editor has open).

## 5. The loop
1. **Orchestrator** (the main session) keeps the board: picks Ready items, at most 3–4 lane A items and 1 lane B item
   at a time, whose `Touches` don't overlap.
2. Sends each to a **worker agent** with the issue as its brief (plus CLAUDE.md, which binds every agent).
3. Worker: branch + worktree, implement, tests, push the branch, comment on the issue with the result, move it to Review.
4. Orchestrator **reviews**: diff read, tests re-run, Unity tests for anything touching `Assets/`, tester agent for
   visual/UI/gameplay work. Then merges into `vs/slice`, closes the issue, moves the card to Done.
5. Anything only the user can decide → `needs:user`, and it's surfaced to the user, never decided by an agent.
- Large fan-outs (several items through implement → review → verify) can use the Workflow tool, only when the user
  says so ("use a workflow"): it's token-heavy.

## 6. Pilot (first run)
Two lane A items in parallel (M3b: notable jobs and roles; trait-coloured time in office) while lane B is busy
(Growth Lab holds the Editor). Compare wall-clock and review findings against single-agent work, then adjust this doc.
