# Prompt: the fifth playtest-feedback batch (#569–#578)

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`. The Unity Editor can be open
or closed. It's written to run unattended and to survive context compaction. You (the session) are the **orchestrator**:
you plan, brief, review, merge, verify and close. **Sonnet workers** do the issues, and you keep sending work back to
them until every issue's *Done when* is truly done. At the end you build a walkthrough the user answers in the Editor.

---

You're working through the fifth batch of playtest-feedback polish for Aquarium Civ: the notes from build 25
(2026-11-10/11, notes #544–#567), triaged into issues #569–#578. Don't stop to ask: make the sensible call, write it
down, and keep going. Stop only for something that would break `dev`/`main`, publish anything, or that CLAUDE.md says to
report. **The issues are binding.** Each one quotes the tester's words and records the user's calls of 2026-11-11.
Don't reopen a decision an issue records. **This is the polish phase: no new features beyond what the issues ask.**

## 0. Resume first
Read `docs/overnight/FEEDBACK-5-STATUS.md`. If it exists, continue from the first unchecked step and don't redo
finished work. If it doesn't, create it from the checklist in §7. After **every** finished step, add one line: the wall
clock, what was done, the commit, and anything odd. It's your memory across compaction. Also keep a **round log** per
issue there (round 1, 2, … : what the worker claimed, what you found, what you sent back).

## 1. Read before any code
- `CLAUDE.md` (every rule applies), `docs/workflow.md`, `docs/tooling.md` (all of it; the dated log from 2026-11-04 on
  closely), `docs/game-design-practices.md`.
- The previous batch, for how it ran and what went wrong: `docs/prompts/feedback-batch-4-run.md`,
  `docs/overnight/FEEDBACK-4-STATUS.md`, `docs/overnight/feedback4-pass-results.md`, PR #568.
- The sign-off walkthrough you'll extend in §6: `AquariumCiv/Assets/Game/View/PlayTests/OpenQuestionsSignOff.cs`, its
  menu `AquariumCiv/Assets/Editor/Agent/OpenQuestionsSignOffMenu.cs`, `docs/open-questions.md`, and the last run's
  verdicts `docs/overnight/open-questions-signoff.md`.
- The issues: `gh issue view <n> -R TommasoRibaudo/aquarium-civ --comments` for #569–#578, and the playtest note each
  one names. **Look at the note's screenshot.** It's base64 in a fenced block; decode it to your scratchpad
  (`awk '/^```$/{f=!f;next} f' | grep -E '^[A-Za-z0-9+/=]{60,}$' | tr -d '\n' | base64 -d > n.jpg`) and Read it.
- The code each issue names under Touches.

## 2. Guardrails
- **Git:**
  - Create the integration branch `p9/feedback-5` from `origin/dev`, in its own worktree `F:\GameDev-wt\feedback5`. Its
    first commit is this prompt (`docs/prompts/feedback-batch-5-run.md`; copy it from `F:\GameDev` if it isn't on
    `origin/dev` yet).
  - Give each worker its own branch `vs/<n>-<slug>`, from `p9/feedback-5`, in its own worktree `F:\GameDev-wt\<n>`.
  - At the end, open a PR from `p9/feedback-5` to `dev`, let CI pass, then merge it.
  - **Never `git commit -a`.** Stage files by name. Never commit `AquariumCiv.slnx`, `Glass.mat` (unless #577 changes it
    on purpose; say so in the PR), or `AquariumCiv/.utmp/`. Never push to `main`/`dev`, and never force-push. Before the
    PR, check `git diff --stat origin/dev` for those files and for any `.unity`/`.prefab`.
- **Lanes (the pattern from PRs #326, #529, #568):**
  - Lane A workers never touch the Editor. They compile-check offline with csc (tooling.md, 2026-10-29) and run
    `dotnet test Tools/SimTests`.
  - Lane B (look, touch, UI) runs as **one Editor worker at a time**, after the lane A work it depends on is merged.
    Before each one: `bash Tools/editor-health.sh` (exit 1 → `bash Tools/restart-editor.sh`; exit 2 → tell the user),
    then the Editor lock, then `bash Tools/watch-editor-stall.sh` in the **same message** (have it wait for the lock
    first). Restart the Editor at least every 3 tickets.
  - **Editor workers compile offline before every `unity recompile`** (batch 3: a worker's own compile error put up
    "Enter Safe Mode?" and the watcher missed it).
- **Unity:** `--timeout 60` on short calls. Long ones go in the background, with a check after a minute. When anything
  stalls, run the dialog check first. Never run a bare `unity command test run`. Use `Tools/run-tests.sh`, one assembly
  or class per run. Leave **Run In Background ON** at the end.
- **Known traps:**
  - **#569 moves the land's numbers.** Seed-pinned tests across Eco, Life and placed life will shift. Merge #569 **first**,
    re-seed the fixtures it moves (say why in each), and have every later worker branch from the merge after it. New
    randomness gets its own seeded stream (or `Terrain.Hash3`), never the main stream.
  - Care and EcoMistView budget tests are flaky (pass on rerun); MistDusk 20/21 h already failed on `dev`. Don't chase
    those; check against the base before calling anything a regression.
  - The debug panel is two files: `View/SimDebugPanel.cs` and `View/SliceDebugPanel.cs`. Each worker keeps its cheats in
    one block. **Every new testable state gets a cheat in the same change** (CLAUDE.md).
  - Glass panes leak if a view rebuilds without destroying the old ones (an 81-pane leak was hidden once). #577 adds a
    pane: count the panes before and after a reload.
- **Playable, not green:** every player-facing piece works through the real HUD and real touches. Cheats only reach a
  state.
- **Closed-world writing:** people live in the tank. No outside world in any new line of text (#570's need lines,
  #578's travellers' lines, #576's season word).
- **Free assets only.** Record every source and licence in `docs/assets-log.md`.
- **No publishing:** don't run `ios-testflight.yml`.
- **GitHub:** if GraphQL hits its rate limit, use `gh api` REST; don't wait. If `gh issue comment`/`edit` is blocked by
  the permission classifier, collect the commands and give them to the user at the end.

## 3. Agents: Sonnet, iterated until done
- **Every worker is Sonnet** (`model: "sonnet"` on every Agent call), one issue each, or a chain of issues on the same
  files. No Opus workers in this batch. You (the orchestrator) review.
- Brief each worker from a shared rules file in your scratchpad: the lane, offline csc, tests, cheats in one block,
  closed-world writing, commits without push. Add per issue: the issue number, the worktree and branch, Touches as its
  fence, what *other* workers are touching right now, and the issue's *Done when* copied in as its checklist.
- A worker's final reply is under 400 words and contains:
  - the branch and commit, and the files it changed;
  - its cheats;
  - its test results, with counts;
  - each *Done when* box, ticked or not, with the evidence (test name, screenshot path);
  - **exactly what to verify in Unity**: classes to run, and what each screenshot should show;
  - risks and open questions.
- **The review loop (the core of this run).** After every worker reply:
  1. Treat its claims as unverified. Read the diff. Run its tests yourself (lane A: `dotnet test Tools/SimTests`; lane B:
     the classes it named). Look at every screenshot it names. Run the base before accepting "it was already failing".
  2. Check each *Done when* box against the evidence, and the work against the issue's words and the user's calls.
  3. If anything is short, **send it back** to the same worker with `SendMessage` (it keeps its context): a numbered
     list of what's missing or wrong, each with the evidence. If that worker is gone, start a fresh Sonnet worker on the
     same branch with the round log as its brief.
  4. Repeat until every box is ticked. Log each round in the status file.
  5. **After 4 rounds on one issue without convergence**, stop sending it back: write what's still short in the issue,
     merge what's solid behind a cheat or leave it unmerged, and list it for the user at the end. Don't loop forever.
- **Every tick:** merge accepted branches into `p9/feedback-5` and run `dotnet test Tools/SimTests` on the merge.
  `ListAgents`, and `TaskStop` anything that finished but still looks active.
- Near the 5 h usage limit: keep to low-stakes tasks, and let running workers finish.

## 4. The work
| # | Issue | What | Lane | Notes |
|---|---|---|---|---|
| 1 | #569 | Pacing: village at year 25 (~25 h) for an active player. The **mister's water** speeds tree growth and spread where it falls; more seeds, less thicket choking | A (headless lab + `EcoTuning`) | **First, alone.** Not "near the pond": misted cells. Before/after table for the three scripted players into `docs/design/placed-life.md`; the target into `docs/decisions.md`. Extend #434's lab if it helps; don't close #434 |
| 2 | #573 | Woodcutters never chop beside the Keeper's tree (clear radius, grown wild trees only), and chopping shows chips and logs | A (spot rule + test) → B (FX) | The cause is in the issue: young trees count as forest, seeds fall near the Keeper's tree. Keep it compatible with #366 felling (still undecided) |
| 3 | #574 | No rabbit hunts in a camp full of rabbits; no one ever takes eggs | A (diagnose headless, write the cause in the issue, fix) → B (a hunt seen) | **Eggs: your call** is a minimal egg gather (a gatherer at the reeds with a basket, `TakeEgg()`), built so #428 can grow it later. Record the call. Report the job mix |
| 4 | #578 | The travellers: words match the land (woods / meadow / pond), the wood reward seen (they carry logs to the stash), they join at the village | A (wording, join rule, tests) → B (log carry, joining moment) | **After #573** (shares the log carry). Cheats: "Travellers bring wood now", "Travellers join now" |
| 5 | #575 | Rename a notable from their person card | A (save field, name used everywhere after) → B (card UI) | Old chronicle lines keep the old name |
| 6 | #570 | Locked life seeds shown with what they need; a line saying what the village waits for | A (readouts in words from the jars and `EcoModel.Gates`) → B (chooser, the line) | **After #569** (the gate may change). People's words, no "unlock" |
| 7 | #571 + #572 | Population on the HUD; tapping the chief's office selects the chief | B (UI) | One worker, same HUD files. Coordinate the title line with #576's season word |
| 8 | #576 | Seasons you can see: tint, autumn leaves, winter snow, season word | B (look) | **Spec first:** four season shots, then wire it. One material parameter, cheap on an A13. Cheat "Skip to next season". `needs:user` |
| 9 | #577 | Glass: no outline from hidden panes over dry or lit land; a glass lid | B (look, taps) | Taps, mister and tweezers must pass through the lid. Pane count stable after reload. `needs:user` |

- Start lane A with #569 alone. When it's merged, run lane A of 2, 3, 5 in parallel; then 4 (after 2) and 6.
- Lane B, one Editor worker at a time, in this order: 7, 2, 3, 4, 5, 6, 9, 8.
- Issues that need the user's look (`needs:user`: #576, #577) and anything you'd like them to judge: build your best
  version, merge it, and **keep the issue open**. Comment "Built in `<commit>` (PR #…), waiting for the user's sign-off
  in the walkthrough." **Close only the ones that need no sign-off.**
- **Feedback notes:** they're already triaged (`fb:triaged`). When you close a work issue, close the notes it answers
  with a comment naming the issue and the commit: #569 → #567, #547; #570 → #549; #571 → #554; #572 → #555;
  #573 → #545, #557; #574 → #551, #544, #550; #575 → #548; #576 → #553; #577 → #546; #578 → #556. Notes whose issue
  stays open stay open.
- Close work issues with `bash Tools/close-issue.sh <n> "<note>"`.

## 5. Verification in Unity (one batched pass, then the play-through)
1. Bring the Editor up on `p9/feedback-5`: health gate, the lock, the watcher, `unity recompile`, and the console
   (0 errors). If it shows "Enter Safe Mode?", fix the compile error and answer Ignore with
   `dismiss-dialogs.ps1 -Click`.
2. Run EditMode, one assembly at a time: Sim, Life, View, Audio, ShowMe, Cards, Moments, Shaping, TimeLapseScreen,
   Game.UI. The counts must be above 0.
3. Run PlayMode: every class the workers named, plus `CarePlayTests`, `WorldTapPlayTests`, the life-slot / placing play
   tests, the fishing and pond-life play tests, the apart-groups / settlers play tests, `Feedback3Playthrough`,
   `Tester2Fix`, and `SmokePlay` (category).
4. Fix the failures (send them back to the worker who owns them, same review loop). For each one, decide whether it
   belongs to the ticket or was already failing: run the class on the base. Re-run only what failed, plus the smoke test.
5. **The play-through.** Add the beats below to a `Feedback5Playthrough` (explicit, its own category), then play it by
   touch at 1179×2556. Use cheats only to reach a state:
   - **pacing (#569):** a lab run of the active player reaches the village at year 25 ±3; in the Editor, mist one patch
     for a few seasons (cheat time) and see it green and seed faster than an unmisted one;
   - Kit → Life: locked seeds show with their needs; the village line names what's missing and counts down;
   - the HUD shows the population; tap the chief's office → the chief's card;
   - a y28+ camp with a wild tree beside the Keeper's tree: no one chops there; chopping elsewhere shows chips and logs;
   - a camp with rabbits: a hunt is seen; with ducks and eggs: someone takes an egg at the reeds;
   - the travellers' event with no woods: the words fit the land; logs carried to the stash; the village made → they
     walk in and join, their camp cleared, no leftover roots;
   - rename a notable on the card; the new name shows on the label and in a new chronicle line, and after a reload;
   - skip through the four seasons: each reads at a glance;
   - the glass: dry land and the lit camp at night with hidden panes, no outline; the lid seen from the default and
     top-down cameras; mist, tweezers and a Kit placement through the lid.

   Screenshot every beat and look at every shot. Any beat you can't reach by touch is a blocker: fix it before the end.
6. **Tester:** one `tester` run with a narrow brief:
   - what changed;
   - what to play;
   - a pass list taken from the issues' *Done when*;
   - the fresh-box path;
   - scene sanity: leftover roots, the mesh count and the glass pane count before and after.

   Ask for a budget of about 45 minutes. Fix every **[real]** finding (review loop) and list the **[polish]** ones.

## 6. The walkthrough the user answers (like the 2026-11-10 sign-off)
The user judges in the Editor, one stop at a time, on the `OpenQuestionsSignOff` panel (menu **Aquarium Civ/Open
questions sign-off**). It reads its stops from `docs/open-questions.md` and stages the look stops through the
`Staged()` table in `OpenQuestionsSignOff.cs`. Extend it; don't build a second tool.
1. **`docs/open-questions.md`:** add a section **"Feedback 5 (build 25 notes), 2026-11-1x"** at the top of the waiting
   items:
   - **Sign-offs (look):** one bullet per thing to judge, each naming its issue: the misted patch greening (#569), the
     locked seeds and the village line (#570), the population line (#571), the chopping spot and the wood (#573), a
     rabbit hunt and the egg gather (#574), the travellers' wood and their joining (#578), the rename on the card
     (#575), each season (#576), the glass outline and the lid (#577).
   - **Decisions:** anything you couldn't settle, with ★ your recommendation and an Alternative (e.g. the pacing
     numbers if b + d alone didn't reach year 25 and you changed the gate; the season word's place; the lid's look).
   - **Calls I made:** one bullet per call you made without the user (the egg gather, the job-mix answer, the
     population line's place, any re-seeded fixtures, anything left short after 4 rounds).
   - Move everything older in "Waiting on you" below it, unchanged.
2. **`Staged()`:** add an entry per issue with stops that set the state up with the cheats and frame the camera, each
   with a one-line question for the user (the pattern of the `#543` and `#539` entries). The pacing stop shows the
   before/after table as text and a misted vs unmisted patch side by side. Live stops (the hunt, the joining, the
   chopping) give a "Set up again" so the user can watch twice.
3. **Check it in auto mode first:** create `Temp/agent/oqsignoff/AUTO` (or `OQSIGNOFF_AUTO=1`), run the test, read
   `auto-results.md` and **look at every shot** in `Temp/agent/oqsignoff/`. Every new stop must stage (no
   "not staged") and frame what its question asks about. Fix and re-run until it does. Delete the `AUTO` file after.
4. Leave the Editor **ready** (not in play mode), Run In Background ON, the lock released, so the user can start it from
   the menu.
5. Tell the user, in the final report: open the menu **Aquarium Civ/Open questions sign-off**, the Feedback 5 section
   comes first ("Go to..." jumps there), about N stops, about M minutes; the verdicts land in
   `docs/overnight/open-questions-signoff.md`.
6. **After the user answers** (if they do so in this session): act on the verdicts. Move answered items into
   `docs/decisions.md`, close the sign-off issues they approve (and their notes), and open follow-up issues for the
   changes they asked for.

## 7. Checklist (copy into FEEDBACK-5-STATUS.md)
- [ ] `p9/feedback-5` from `origin/dev`; this prompt committed
- [ ] 1 · #569 merged (pacing table; target in decisions.md; fixtures re-seeded with reasons)
- [ ] 2 · #573 merged (no chopping by the Keeper's tree; chips and logs)
- [ ] 3 · #574 cause written in the issue; hunts and the egg gather merged
- [ ] 4 · #578 merged (words, wood, joining)
- [ ] 5 · #575 merged (rename)
- [ ] 6 · #570 merged (locked seeds, village line)
- [ ] 7 · #571 + #572 merged (population, chief's office)
- [ ] 8 · #576 merged (open for look sign-off)
- [ ] 9 · #577 merged (open for look sign-off)
- [ ] Every issue's *Done when* ticked with evidence, or listed as short after 4 rounds
- [ ] Batched Editor pass green (EditMode 10 assemblies, PlayMode classes, smoke); failures fixed or noted as baseline
- [ ] `Feedback5Playthrough` + the touch play-through done, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Walkthrough: open-questions.md section, `Staged()` stops, auto run clean, every shot looked at
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released; Editor ready
- [ ] PR → `dev`, CI green, merged
- [ ] Final report to the user:
  - what's in;
  - how to start the walkthrough, and what's in it;
  - the [polish] list;
  - the calls you made without them;
  - anything left short after 4 rounds;
  - the wall-clock time per phase
