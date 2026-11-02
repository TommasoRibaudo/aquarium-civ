# Prompt: build, finish and test all of P7 (the Keeper's dilemmas)

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`, with the Unity Editor
open on `AquariumCiv`. It's written to run unattended and to survive context compaction.

---

You're building, finishing and agent-testing **prototype P7, the Keeper's dilemmas**, end to end: issues **#220–#226**
(milestone "P7 Keeper's dilemmas"). Don't stop to ask: make the sensible call, write it down, and keep going. Stop
only for something that would break `dev`/`main`, publish anything, or that CLAUDE.md says to report.

## 0. Resume first
Read `docs/overnight/P7-STATUS.md`. If it exists, continue from the first unchecked step and don't redo finished work.
If it doesn't, create it from the checklist in §5. Update it after **every** finished step (one line: what, commit,
anything odd). It's your memory across compaction. Check the wall clock at every update.

## 1. Read before any code
- `CLAUDE.md` (every rule applies), `docs/workflow.md` (lanes, Editor lock, the loop, baseline first),
  `docs/tooling.md` (all of it, including the dated log), `docs/game-design-practices.md`.
- **The spec:** `docs/design/results/P7.md` (rules, the one variable, scope, pacing, measures, pass criteria) and
  `docs/research/14-dilemmas-and-living-land.md` §2 (design, kinds, consequences, research).
- The issues themselves: `gh issue view 220` … `226`. Each one's What / Touches / Done when is binding.
- Code to build on: `Sim/SimCore.Wishes.cs` (the exact shape: open → answered or lapsed, no timer), `SimCore.Cards.cs`
  (card kinds, ranking, teaser threads), `SimCore.Story.cs` (voice), `SimCore.Nudges.cs` (leans), `SimCore.Roles.cs`,
  `Life/EchoScenes.cs` + `LifePlanner.Echo.cs` (staging people in a scene), `View/Cards/**`, `View/SimDebugPanel.cs`,
  `Runtime/Telemetry/**`.

## 2. Guardrails
- **Git:**
  - Create the integration branch `p7/dilemmas` from `origin/dev`.
  - **First commit on it: the P7 docs from the working tree** (`docs/design/results/P7.md`, `docs/research/14-…`,
    `docs/research/15-…`, `docs/README.md`, `docs/decisions.md`, `docs/prompts/P7-dilemmas-run.md`), so every worker
    sees the spec.
  - One branch per issue (`vs/<n>-<slug>`) from `p7/dilemmas`; lane A work in its own worktree. Merge finished issues
    into `p7/dilemmas`.
  - Never commit `AquariumCiv/AquariumCiv.slnx`, `Glass.mat` or `AquariumCiv/.utmp/`. Never push to `main`/`dev`;
    never force-push.
- **Off by default:** `SimConfig.dilemmas = false`. With it off, every existing test and fingerprint must stay green,
  and the slice must play exactly as before.
- **Unity:** CLAUDE.md in full.
  - Health gate (`bash Tools/editor-health.sh`) before taking `.agent-editor.lock`; restart at least every 3 tickets.
  - Start `Tools/watch-editor-stall.sh` with every Editor agent.
  - `--timeout` on short calls; long ones in the background with 1-minute check-ins.
  - Run the dialog check first when anything stalls. Never a bare `unity command test run`.
  - No YAML hand-edits while the Editor is reachable.
- **Tests:** batch them. Don't rerun what already passed. **Baseline first:** a failure that also fails on the base
  commit isn't yours: file it, move on, 90-minute time box.
- **Debug panel stays current** (#225 covers it, but every Editor ticket keeps it working).
- **No publishing:** don't run `ios-testflight.yml`. Prepare the build so the user can start it with one command.
- **Free assets only.** A stake and an offering prop in the style kit are enough.

## 3. Order, lanes and models
| Step | Issue | Lane | Worker model | Notes |
|---|---|---|---|---|
| 1 | #220 sim core | A | Sonnet | Starts now |
| 2 | #221 callbacks | A | Sonnet | After #220 merges |
| 2 | #223 card | B (Editor) | Sonnet | After #220 merges, in parallel with #221 |
| 3 | #222 the 8 dilemmas | A | **Opus** | After #221. Writing is the main cost: voice, both options good, a hint per option |
| 3 | #224 choice scene | B | **Opus** | After #223. The scene must read as a moment, not a menu |
| 4 | #225 debug, telemetry, survey | B | Sonnet | After #221 and #224 |
| 5 | #226 tester run + results | B | the `tester` agent | After everything else |

- At most 3 lane A agents and 1 Editor agent at a time, and their `Touches` must not overlap.
- Give each worker its issue as its brief, plus CLAUDE.md. You (the orchestrator) review each one: read the diff,
  rerun the tests, run Unity tests for anything under `Assets/`, merge, then
  `Tools/close-issue.sh <n> "<note>"`.
- Run the full EditMode suite after each merge of sim work that touches `Assets/`.
- **#222's text and #224's look need the user's sign-off:** don't wait for it. Finish with your best version, list
  them for review at the end, and keep the issue open with a comment saying so.
- Issue #228 (Ecology Lab spike) is **not** part of this run.

## 4. Testing P7
- Per ticket: the "Done when" of the issue, plus `SlicePlaythroughSmoke` for anything in View, UI or Runtime.
  PlayMode UI tests call `GameUiPanel.AcceptInputUnfocused(true)`.
- **Self-play before the tester:** with dilemmas on and the lab preset (×60), play a fresh box from the first run to the
  village. Screenshot at phone portrait sizes and look at every shot. Check:
  - none appears in the first run;
  - a wish and a dilemma never show together;
  - one answered from the card, one in the box;
  - one left to lapse (the people decide, the line is neutral);
  - a callback card with "Because the Keeper chose…" and the grey question line.
  Then turn dilemmas off and confirm the slice is unchanged.
- **The `tester` agent (#226):** brief it with
  - what changed;
  - what to play (variant B, lab preset, a fresh box to the village);
  - what to watch for (it reads as a quiz or a menu, a "wrong answer" feeling, text too long, targets under 44 pt,
    the two choices hard to tell apart or tap at default zoom, pacing);
  - a pass list (`P7.md` "Pass", the parts an agent can judge);
  - the fresh-box path;
  - scene sanity (leftover roots, mesh count).
  Don't touch the Editor while it runs. Fix every **[real]** finding (an issue each, the same loop); list the
  **[polish]** ones for the user.
- **Results:** write the agent-test results into `docs/design/results/P7.md` ("Agent playtest", what passed, the
  numbers from `TelemetryAnalyzer`), and the exact steps for the human lab + 3-day diary.
- **Build prep:** confirm the variant switch is reachable in a dev build (the debug panel). Write the one command the
  user runs (`gh workflow run ios-testflight.yml -f notes="P7 dilemmas lab"`) in `P7.md`. Don't run it.

## 5. Checklist (copy into P7-STATUS.md)
- [ ] Branch `p7/dilemmas` from `origin/dev`; the P7 docs committed
- [ ] #220 merged, closed
- [ ] #221 merged, closed
- [ ] #223 merged, closed
- [ ] #222 merged (open for the user's tone review)
- [ ] #224 merged (open for the user's look sign-off)
- [ ] #225 merged, closed
- [ ] Full EditMode suite + `SlicePlaythroughSmoke` green on `p7/dilemmas`; dilemmas off = the slice is unchanged
- [ ] Self-play done, screenshots looked at
- [ ] Tester run; [real] findings fixed; [polish] listed
- [ ] `P7.md` results + the human test steps + the build command
- [ ] PR `p7/dilemmas` → `dev`, CI green, merged (the feature is off by default)
- [ ] Final report

## 6. The final report (to the user, at the end)
Keep it short and plain:
- What was built, and the PR link.
- What the agent test found: pass criteria an agent can judge, path split, seconds to answer, [polish] items.
- **What the user does next**, as a list:
  - review #222's text;
  - sign off #224's look;
  - run the TestFlight command;
  - run the lab + diary (`P7.md`);
  - go / reshape / drop.
- Put every open question in `docs/open-questions.md`, and publish them as an open-questions artifact with a copy
  button, as in earlier autonomous runs.
