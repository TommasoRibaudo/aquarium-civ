# Prompt: playtest 1, second batch (the 2026-11-14 notes → #696–#710, plus #669 #671 #676 #687 #658)

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`. The Unity Editor can be open
or closed. It's written to run unattended and to survive context compaction. You (the session) are the **orchestrator**:
you plan, brief, review, merge, verify and close. **Sonnet workers** do the issues, and you keep sending work back to
them until every issue's *Done when* is truly done. At the end you build a walkthrough the user answers in the Editor.

---

You're working through the second batch from the first outside playtest of Aquarium Civ: the notes of 2026-11-14
(private repo `aquarium-civ-feedback`, notes #44–#71), triaged into public issues #696–#710 and comments on #669, #671,
#676, #687 and #658. Don't stop to ask: make the sensible call, write it down, and keep going. Stop only for something
that would break `dev`/`main`, publish anything, or that CLAUDE.md says to report. **The issues are binding**, with the
comments added on 2026-11-14 (read them all). Don't reopen a decision an issue or `docs/decisions.md` records. **This is
the polish phase: no new features beyond what the issues ask.**

## 0. Resume first
Read `docs/overnight/PLAYTEST1-B-STATUS.md`. If it exists, continue from the first unchecked step and don't redo
finished work. If it doesn't, create it from the checklist in §7. After **every** finished step, add one line: the wall
clock, what was done, the commit, and anything odd. It's your memory across compaction. Also keep a **round log** per
issue there (round 1, 2, … : what the worker claimed, what you found, what you sent back).

## 1. Read before any code
- `CLAUDE.md` (every rule applies), `docs/workflow.md`, `docs/tooling.md` (all of it; the dated log from 2026-11-10 on
  closely), `docs/game-design-practices.md` (§15: people use everything), `docs/design/people-and-things.md`,
  `docs/design/animation-specs.md` (how a spec sheet looks).
- How the last runs went: `docs/prompts/feedback-batch-5-run.md`, `docs/overnight/FEEDBACK-7-STATUS.md`, and the
  overnight run of 2026-11-14 (PRs #681, #682; `docs/overnight/open-questions-signoff.md` for the user's verdicts).
- The sign-off walkthrough you'll extend in §6: `AquariumCiv/Assets/Game/View/PlayTests/OpenQuestionsSignOff*.cs`, its
  menu `AquariumCiv/Assets/Editor/Agent/OpenQuestionsSignOffMenu.cs`, `docs/open-questions.md`.
- The issues: `gh issue view <n> -R TommasoRibaudo/aquarium-civ --comments` for every issue in §4. Each links its
  private note: read it with `gh issue view <m> -R TommasoRibaudo/aquarium-civ-feedback` and **look at its screenshot**.
  It's base64 in a fenced block; decode it to your scratchpad
  (`awk '/^```$/{f=!f;next} f' | grep -E '^[A-Za-z0-9+/=]{60,}$' | tr -d '\n' | base64 -d > n.jpg`) and Read it.
- The code each issue names.

## 2. Guardrails
- **Privacy (#637, user rule):** the code repo is public. Nothing from a private note goes into a public issue, commit,
  PR, doc or test name: no tester quotes, screenshots, device, tester id or nickname, or their box's names. Paraphrase,
  and link the private note. Screenshots you decode stay in your scratchpad.
- **Git:**
  - `F:\GameDev` itself has another session's uncommitted work on `p9/signoff`. **Never work, stage, stash or switch
    branches there.** Use worktrees only.
  - Create the integration branch `p9/playtest1-b` from `origin/dev` (it already holds the new start, #682), in its own
    worktree `F:\GameDev-wt\pt1b`. Its first commit is this prompt (`docs/prompts/playtest-1-batch-2-run.md`; copy it
    from `origin/dev` or `F:\GameDev` if it isn't there).
  - Give each worker its own branch `vs/<n>-<slug>`, from `p9/playtest1-b`, in its own worktree `F:\GameDev-wt\<n>`.
  - At the end, open a PR from `p9/playtest1-b` to `dev`, let CI pass, then merge it. CI on PRs into `dev` skips the
    Unity suite, so **your local batched Editor pass (§5) is the Unity check**: never skip it.
  - **Never `git commit -a`.** Stage files by name. Never commit `AquariumCiv.slnx`, `Glass.mat`, `AquariumCiv/.utmp/`,
    `_Recovery/`, or a stray save scene. Never push to `main`/`dev`, and never force-push. Before the PR, check
    `git diff --stat origin/dev` for those files and for any `.unity`/`.prefab`.
- **Animation sign-off (user rule, 2026-11-14):** every new people-and-things animation needs a detailed spec sheet in
  `docs/design/animation-specs.md` that **the user signs off**, then an AnimLab clip the user signs off, **before it is
  merged into `dev`**. In this batch that's #705 (hanging the laundry) and #700's take-down and rebuild of the rack.
  Their workers write the spec, build it, and record the AnimLab clip on their own branch. Those branches go into the
  walkthrough branch (§6) but **not** into `p9/playtest1-b` or the PR to `dev` until the user approves them.
- **Lanes:**
  - Lane A workers never touch the Editor. They compile-check offline with csc (tooling.md, 2026-10-29) and run
    `dotnet test Tools/SimTests`.
  - Lane B (look, touch, UI, animation) runs as **one Editor worker at a time**. Before each one: `bash
    Tools/editor-health.sh` (exit 1 → `bash Tools/restart-editor.sh`; exit 2 → tell the user), then the Editor lock,
    then `bash Tools/watch-editor-stall.sh` in the **same message** (have it wait for the lock first). Restart the
    Editor at least every 3 tickets.
  - **Editor workers compile offline before every `unity recompile`** (a worker's own compile error puts up "Enter
    Safe Mode?", and the watcher can miss it).
- **Unity:** pass `--project-path F:/GameDev-wt/<...>/AquariumCiv` with **forward slashes** from Bash (tooling.md,
  2026-11-14). Use `--timeout 60` on short calls. Long ones go in the background, with a check after a minute. When
  anything stalls, run the dialog check first. Never run a bare `unity command test run`. Use `Tools/run-tests.sh`, one
  assembly or class per run (`STALL_S=600` for the Life and Sim EditMode assemblies). Don't edit code while a batch
  runs. Leave **Run In Background ON** at the end.
- **Known traps:**
  - **#697 and #696 share a cause** (`LifePlanner.Sync` replans everyone when the sim signature changes). One worker
    does both, #697 first (diagnose with a replan log, write the cause in both issues), and it's **merged before any
    other Life change**. Seed-pinned Life tests may move; re-seed only with a reason written in the commit.
  - Care and EcoMistView budget tests are flaky (pass on rerun); MistDusk 20/21 h already failed on `dev`. Check against
    the base before calling anything a regression. A ticket whose failures also fail on the base can merge. Time-box a
    hunt to 90 minutes.
  - The debug panel is two files: `View/SimDebugPanel.cs` and `View/SliceDebugPanel.cs`. Each worker keeps its cheats in
    one block. **Every new testable state gets a cheat in the same change** (CLAUDE.md).
  - Glass panes leak if a view rebuilds without destroying the old ones. Count panes before and after a reload in the
    batched pass.
- **Playable, not green:** every player-facing piece works through the real HUD and real touches at phone size. Cheats
  only reach a state.
- **Closed-world writing:** people live in the tank. No outside world in any new line (#703's jar line, #706's option
  labels).
- **Seen, not read:** a fix that can be shown in the box beats a line of text (#700, #705, #706).
- **Free assets only.** Record every source and licence in `docs/assets-log.md`.
- **No publishing:** don't run `ios-testflight.yml`.
- **GitHub:** if GraphQL hits its rate limit, use `gh api` REST; don't wait. If `gh issue comment`/`edit` is blocked by
  the permission classifier, collect the commands and give them to the user at the end.

## 3. Agents: Sonnet, iterated until done
- **Every worker is Sonnet** (`model: "sonnet"` on every Agent call), one issue each, or a chain of issues on the same
  files. You (the orchestrator) review.
- Brief each worker from a shared rules file in your scratchpad: the lane, offline csc, tests, cheats in one block,
  privacy, closed-world writing, commits without push. Add per issue: the issue number, the worktree and branch, the
  files it may touch as its fence, what *other* workers are touching right now, and the issue's *Done when* copied in as
  its checklist.
- A worker's final reply is under 400 words and contains:
  - the branch and commit, and the files it changed;
  - its cheats;
  - its test results, with counts;
  - each *Done when* box, ticked or not, with the evidence (test name, screenshot path);
  - **exactly what to verify in Unity**: classes to run, and what each screenshot should show;
  - risks and open questions.
- **The review loop (the core of this run).** After every worker reply:
  1. Treat its claims as unverified. Read the diff. Run its tests yourself (lane A: `dotnet test Tools/SimTests`;
     lane B: the classes it named). Look at every screenshot it names. Run the base before accepting "it was already
     failing".
  2. Check each *Done when* box against the evidence, and the work against the issue's words and its comments.
  3. If anything is short, **send it back** to the same worker with `SendMessage` (it keeps its context): a numbered
     list of what's missing or wrong, each with the evidence. If that worker is gone, start a fresh Sonnet worker on the
     same branch with the round log as its brief.
  4. Repeat until every box is ticked. Log each round in the status file.
  5. **After 4 rounds on one issue without convergence**, stop sending it back: write what's still short in the issue,
     merge what's solid behind a cheat or leave it unmerged, and list it for the user at the end.
- **Every tick:** merge accepted branches into `p9/playtest1-b`, run `dotnet test Tools/SimTests` on the merge, and scan
  for lane A work that can start now (the user shouldn't have to ask what runs in parallel). `ListAgents`, and
  `TaskStop` anything that finished but still looks active.
- Wait in the background; no poll loops. Near the 5 h usage limit: keep to low-stakes tasks, and let running workers
  finish.

## 4. The work
| # | Issue | What | Lane | Notes |
|---|---|---|---|---|
| 1 | #697 → #696 | Mass replans: everyone sets off then all stop; sleepers leave the tent before the last one goes in (back after #607) | A (replan log, keep the current activity through any replan, a test per trigger) → B (watched at night and by day) | **First, alone.** Cause written in both issues. #607's fix is the starting point, not the answer |
| 2 | #699 | Deer path round the fire and the camp props | A | |
| 3 | #703 | Ducks placed on a frozen pond | A (jar rule + need line) → B (jar look) | **Your call (record it):** ★ the duck jar is shut while the pond is frozen and says why in the people's words. The thaw-wait option only if the jar route breaks something |
| 4 | #700 | A path wins over a rack: the rack moves | A (placing rule, path check, test) → B (take-down/rebuild beat) | **Spec-gated** (§2): spec sheet + AnimLab clip for the user; without sign-off, the clean move at a quiet moment is the fallback in `p9/playtest1-b` |
| 5 | #698 | A child vanishes after the rabbit chase | B | Find it in a long PlayMode run with the chase cheat; the doll stays visible to the end |
| 6 | #669 (+ comment) | History replay: life **and buildings** only from when they existed | B | Widened on 2026-11-14 (feedback#52). Houses, racks, woodpile, choice props |
| 7 | #702 | The Feedback pill is always reachable | B (UI) | PlayMode test over every panel |
| 8 | #707 | HUD top: one look, no white backing, the title's font, Settings beside the other buttons | B (UI) | Follow-up to #595/#604; #649's gear icon if not in yet. Look at day and night |
| 9 | #704 | The fire's green ring takes the ground's colour | B (look) | Grass, dry, snow shots |
| 10 | #701 | The pond's water stays inside its dirt rim | B (look) | Small, grown, at the glass |
| 11 | #676 (+ comment) | The stew by what the camp has (fish, rabbit, berries/roots) and looks different for each | A (stew kind from the stash) → B (look) | The pot work from #681 stands; only the by-kind stew is new. Coordinate with #692 if it's running |
| 12 | #706 | Touch choices show their options (glow/label) and name them | B (UI) | Every touch choice, not only the newcomers |
| 13 | #705 | The laundry reads: clothes on the line during and after | B (prop + beat) | **Spec-gated** (§2) |
| 14 | #671 (comment) | No new work: the "line" a tester asked about is this bug | — | Its walkthrough stop includes the wet/snow ground close up |
| 15 | #655 (comment) | Phone heat: mostly the screen share | B (last, if time) | Profile the frame in the Editor (CPU/GPU ms, draw calls, idle box vs. busy camp) and write the numbers in the issue. No device claims. Leave it open |

**Design calls (no build; the user decides):** #708 (a deeper pond seen through the glass), #709 (the bird: nest and
eggs, a visit only, or dropped), #710 with #658 (animal caps and/or a bigger box), #687 (water rework). One Sonnet docs
worker writes a short proposal per issue, early and in parallel: what it is, the cost, ★ a recommendation and an
alternative. For #710 it counts the animals in a year-20 camp headless (lab) first. For #687 it starts from **the
user's read** in the issue comment (the tester saw the pond grow; the link to their misting didn't show because it grows
too little), so the ★ is a visible payoff soon after the stroke. The proposals go into `docs/open-questions.md` (§6) and
as a comment on each issue.

**Out of scope here:** #692 and #543 (the comments from feedback#58/#62 belong to #692's own step and its spec
sign-off), #668 (the kit's whole design). Don't start them.

- Start lane A with #697 → #696 alone. When it's merged, run 2, 3 and 4 (lane A parts) and the design worker in parallel.
- Lane B, one Editor worker at a time, in this order: 1, 5, 6, 7, 8, 9, 10, 3, 11, 12, 4, 13, then 15.
- **Closing:** close an issue only when it needs no look from the user: #696, #697, #698, #699, #702 when their *Done
  when* is proven. Everything else, and anything you'd like the user to judge: build your best version, merge it (except
  the spec-gated ones), and **keep the issue open** with "Built in `<commit>` (PR #…), waiting for the user's sign-off in
  the walkthrough." Close work issues with `bash Tools/close-issue.sh <n> "<note>"`.
- **Feedback notes** (private repo; already `fb:triaged`): when you close a work issue, close the notes it answers with
  a comment naming the issue and the commit. #696 → fb#69, #697 → fb#64, #698 → fb#68, #699 → fb#63, #700 → fb#57,
  #701 → fb#50, #702 → fb#54, #703 → fb#53, #704 → fb#67, #705 → fb#61, #706 → fb#71, #707 → fb#66, #708 → fb#49,
  #709 → fb#59, #710 → fb#60, #669 → fb#52, fb#30, #676 → fb#65, #671 → fb#47. Notes whose issue stays open stay open.
- **Praise and pulse:** any new `fb:praise` or `fb:pulse` note that arrives during the run gets its row in
  `docs/design/results/praise-and-pulse.md` (in your own words) and `fb:triaged`. New problem notes: triage them as
  issues the same way, and **don't** start them in this run; list them in the report.

## 5. Verification in Unity (one batched pass, then the play-through)
1. Bring the Editor up on `p9/playtest1-b`: health gate, the lock, the watcher, `unity recompile`, and the console
   (0 errors). If it shows "Enter Safe Mode?", fix the compile error and answer Ignore with
   `dismiss-dialogs.ps1 -Click`.
2. Run EditMode, one assembly at a time: Sim, Life, View, Audio, ShowMe, Cards, Moments, Shaping, TimeLapseScreen,
   Game.UI. The counts must be above 0.
3. Run PlayMode: every class the workers named, plus `CarePlayTests`, `WorldTapPlayTests`, the placing and pond-life
   play tests, the history/time-lapse play tests, `PulseQuestionPlayTests`, and `SmokePlay` (category, which includes
   `SlicePlaythroughSmoke`).
4. Fix the failures (send them back to the worker who owns them, same review loop). For each one, decide whether it
   belongs to the ticket or was already failing: run the class on the base. Re-run only what failed, plus the smoke
   test. Don't rerun what already passed on the same commit.
5. **The play-through.** Add the beats below to a `Playtest1BPlaythrough` (explicit, its own category), then play it by
   touch at 1179×2556 from a fresh box. Use cheats only to reach a state:
   - a camp night: the last sleeper walks in and nobody comes out; by day, no crowd that sets off and turns back over
     10 game minutes;
   - deer graze near the camp and go round the fire;
   - winter: the duck jar says the pond is frozen; after the thaw, ducks placed stay;
   - a path grows toward the fish rack: the rack moves off it;
   - the rabbit chase ends with the child walking back, visible;
   - History scrubbed back before the first house and the thicket: no house, no berries, no rabbits;
   - the Feedback pill tapped with each panel open;
   - the HUD top by day and by night;
   - the fire on grass, on dry ground and in snow; the pond small, grown and at the glass;
   - a fish stew and a rabbit stew;
   - a touch choice with its options marked;
   - (walkthrough branch only) the laundry on the line; the rack taken down and rebuilt.

   Screenshot every beat and look at every shot. Any beat you can't reach by touch is a blocker: fix it before the end.
6. **Tester:** one `tester` run with a narrow brief:
   - what changed;
   - what to play;
   - a pass list taken from the issues' *Done when*;
   - the fresh-box path;
   - scene sanity: leftover roots, the mesh count and the glass pane count before and after.

   Run it after 6 view tickets or 4 hours of merged Editor work, whichever comes first, and once at the end. Ask for a
   budget of about 45 minutes. Fix every **[real]** finding (review loop) and list the **[polish]** ones.

## 6. The walkthrough the user answers
The user judges in the Editor, one stop at a time, on the `OpenQuestionsSignOff` panel (menu **Aquarium Civ/Open
questions sign-off**). It reads its stops from `docs/open-questions.md` and stages the look stops through the
`Staged()` table in `OpenQuestionsSignOff*.cs`. Extend it; don't build a second tool.
1. **Branch:** build it on `p9/playtest1-b-signoff` = `p9/playtest1-b` + the spec-gated branches (#700's beat, #705), in
   its own worktree. The Editor runs there for the walkthrough.
2. **`docs/open-questions.md`:** add a section **"Playtest 1, batch 2 (2026-11-14 notes)"** at the top:
   - **Sign-offs (look):** one bullet per thing to judge, each naming its issue: the HUD top (#707), the fire ring
     (#704), the pond rim (#701), the stews by kind (#676), the marked choice options (#706), the duck jar in winter
     (#703), History before the first house (#669), the faded glass on wet ground (#671), and anything else you want
     judged.
   - **Animation sign-offs:** #705 and #700: first the spec sheet (a stop that shows the spec text), then the AnimLab
     clip (a live stop with "Set up again").
   - **Decisions:** #708, #709, #710 with #658, #687: each with ★ your recommendation and an Alternative.
   - **Calls I made:** one bullet per call you made without the user (the duck jar, any re-seeded fixtures, anything
     left short after 4 rounds).
   - Leave the older sections below it unchanged.
3. **`Staged()`:** add an entry per issue with stops that set up the state with the cheats and frame the camera, each
   with a one-line question for the user (the pattern of the existing entries). Live stops (the tent at night, the
   deer, the rack move, the laundry) give a "Set up again" so the user can watch twice.
4. **Check it in auto mode first:** create `Temp/agent/oqsignoff/AUTO` (or `OQSIGNOFF_AUTO=1`), run the test, read
   `auto-results.md` and **look at every shot** in `Temp/agent/oqsignoff/`. Every new stop must stage (no "not
   staged") and frame what its question asks about. Fix and re-run until it does. Delete the `AUTO` file after.
5. Leave the Editor **ready** (not in play mode) on the walkthrough branch, Run In Background ON, the lock released, so
   the user can start it from the menu.
6. **After the user answers** (if they do so in this session): act on the verdicts. Move answered items into
   `docs/decisions.md`; merge the approved spec-gated branches into `p9/playtest1-b` (then a follow-up PR to `dev` if
   the main one is already merged); close the sign-off issues they approve (and their notes); open follow-up issues for
   the changes they ask for.

## 7. Checklist (copy into PLAYTEST1-B-STATUS.md)
- [ ] `p9/playtest1-b` from `origin/dev`; this prompt committed
- [ ] 1 · #697 → #696 cause written in both issues; merged
- [ ] 2 · #699 merged
- [ ] 3 · #703 merged (call recorded)
- [ ] 4 · #700 rule merged; take-down/rebuild beat specced, on its branch
- [ ] 5 · #698 merged
- [ ] 6 · #669 merged (buildings too)
- [ ] 7 · #702 merged
- [ ] 8 · #707 merged (open for look sign-off)
- [ ] 9 · #704 merged (open for look sign-off)
- [ ] 10 · #701 merged (open for look sign-off)
- [ ] 11 · #676 stew by kind merged (open for look sign-off)
- [ ] 12 · #706 merged (open for look sign-off)
- [ ] 13 · #705 specced, built and clipped, on its branch
- [ ] 15 · #655 numbers in the issue (if time)
- [ ] Design proposals for #708, #709, #710 + #658, #687 written (open-questions.md + issue comments)
- [ ] Every issue's *Done when* ticked with evidence, or listed as short after 4 rounds
- [ ] Batched Editor pass green (EditMode 10 assemblies, PlayMode classes, smoke); failures fixed or noted as baseline
- [ ] `Playtest1BPlaythrough` + the touch play-through done, every beat looked at
- [ ] Tester run(s) on cadence and at the end; [real] fixed; [polish] listed
- [ ] Walkthrough: branch, open-questions.md section, `Staged()` stops, auto run clean, every shot looked at
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released; Editor ready
- [ ] PR `p9/playtest1-b` → `dev`, CI green (watch each job, stop on the first fail), merged
- [ ] Final report to the user:
  - what's in, and what waits on the walkthrough;
  - how to start the walkthrough, which branch it's on, about how many stops and minutes; verdicts land in
    `docs/overnight/open-questions-signoff.md`;
  - the [polish] list;
  - the calls you made without them;
  - anything left short after 4 rounds;
  - new notes that came in during the run;
  - the wall-clock time per phase
