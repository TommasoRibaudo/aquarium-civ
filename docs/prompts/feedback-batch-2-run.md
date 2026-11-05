# Prompt: the second playtest-feedback batch (#325, #328, #329, #331, #332, #333, #281 tint, #327 and #330 proposals)

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`. The Unity Editor can be open
or closed. It's written to run unattended and to survive context compaction. You (the session) are the **orchestrator**:
you plan, brief, review, merge, verify and close. Workers do the issues.

---

You're working through the second batch of playtest-feedback polish for Aquarium Civ. Don't stop to ask: make the
sensible call, write it down, and keep going. Stop only for something that would break `dev`/`main`, publish anything,
or that CLAUDE.md says to report. **The issues are binding.** Each one quotes the user's own words, and the comments
carry later calls. Don't reopen a decision an issue records.

## 0. Resume first
Read `docs/overnight/FEEDBACK-2-STATUS.md`. If it exists, continue from the first unchecked step and don't redo
finished work. If it doesn't, create it from the checklist in §6. After **every** finished step, add one line: the wall
clock, what was done, the commit, and anything odd. It's your memory across compaction.

## 1. Read before any code
- `CLAUDE.md` (every rule applies), `docs/workflow.md`, `docs/tooling.md` (all of it; read the 2026-11-04 entries
  closely), `docs/game-design-practices.md`.
- The previous batch, for context: PR #326 (`p8/feedback`: tweezers, no trash, vine, tree, voices, frogs, glass, pin,
  lapse order) and its issues #311–#320.
- The issues: `gh issue view <n> -R TommasoRibaudo/aquarium-civ --comments` for every one in §4.
- The code each issue names under Touches.

## 2. Guardrails
- **Git:**
  - Create the integration branch `p8/feedback-2` from `origin/dev`, in its own worktree `F:\GameDev-wt\feedback2`. Its
    first commit is `Tools/run-tests.sh` (this prompt is already in `docs/prompts/`).
  - Give each worker its own branch `vs/<n>-<slug>`, from `p8/feedback-2`, in its own worktree `F:\GameDev-wt\<n>`.
  - At the end, open a PR from `p8/feedback-2` to `dev`, let CI pass, then merge it.
  - **Never `git commit -a`.** Stage files by name. The main checkout always has a dirty `AquariumCiv/AquariumCiv.slnx`,
    and `commit -a` once swept it into a commit.
  - Never commit `AquariumCiv.slnx`, `Glass.mat` or `AquariumCiv/.utmp/`. Never push to `main`/`dev`, and never
    force-push.
  - Before the PR, check `git diff --stat origin/dev` for those files and for any `.unity`/`.prefab`.
- **How this batch runs (it worked in PR #326: 10 issues in 2 h 45 m):**
  - Workers never touch the Editor. They compile-check offline with csc (tooling.md, 2026-10-29) and run
    `dotnet test Tools/SimTests`.
  - You do **one batched Editor pass** at the end (§5), then fix what it finds.
  - Exception: the look issues (#332 rack, #329 buttons, #281 tint) need screenshots to iterate. Run them as **one Editor
    worker at a time** after the lane A workers finish. Follow the Editor lock, `bash Tools/editor-health.sh`, and start
    `bash Tools/watch-editor-stall.sh` in the same message the lock is taken. The watcher exits immediately if the lock
    isn't there yet, so have it wait for the lock first (for example, a 10-minute wait loop, then the watcher).
- **Unity:** `--timeout 60` on short calls. Long ones go in the background, with a check after a minute. When anything
  stalls, run the dialog check first. Never run a bare `unity command test run`.
  - The test runner pattern that worked: start `unity command test run --mode <M> --filter <F> [--filter_type
    assembly|category] --async_tests true`, then poll `unity command test status --format json` until `completed`
    after it has shown `running`. Read `data.result.summary` and `results[].Status`. Use one assembly per run.
  - Put the runner in `Tools/run-tests.sh` (copy the loop from tooling.md's 2026-11-04 note) so it's in the repo.
- **Known traps (from PR #326):**
  - Unity's NUnit has no `Is.AnyOf`. Use `CollectionAssert.Contains`.
  - Seed-pinned tests move when a change shifts the sim's random stream. Find which branch moved them (run the class on
    each branch), re-seed the fixture, and say why in a comment. Give new randomness its own seeded stream (or
    `Terrain.Hash3`), never the main stream.
  - Throttles must not swallow a fresh tap.
  - Fades must use distance to the object itself, not to its infinite plane.
  - The debug panel is two files: `View/SimDebugPanel.cs` and `View/SliceDebugPanel.cs`. Each worker keeps its cheats in
    one block.
- **Playable, not green:** every player-facing piece works through the real HUD and real touches. Cheats only reach a
  state.
- **Free assets only.** Record every source and licence in `docs/assets-log.md`.
- **No publishing:** don't run `ios-testflight.yml`.
- **GitHub writes:** `gh issue comment` and `gh issue edit` may be blocked by the permission classifier. If they are,
  collect the commands and give them to the user at the end; don't work around the block.

## 3. Models and agents
- **Workers default to Sonnet** (`model: "sonnet"`), one issue each, or a chain of issues on the same files.
- Use **Opus** only for #327's proposal (design judgment across many files).
- Brief each worker from a shared rules file in your scratchpad: no Editor, offline csc, tests, cheats in one block, the
  closed-world writing rule, and commits without push. Add per issue: the issue number, the worktree and branch,
  Touches as its fence, and what *other* workers are touching right now.
- A worker's final reply is under 400 words and contains:
  - the branch and commit, and the files it changed;
  - its cheats;
  - its test results, with counts;
  - **exactly what to verify in Unity**: classes to run, and what each screenshot should show;
  - risks and open questions.
- Treat a worker's claims as unverified until you've checked them yourself. In PR #326 one report said it had saved
  screenshots without having run Unity. Another said a failure "was already there" without running the base.
- **Every tick:** merge finished branches into `p8/feedback-2` and run `dotnet test Tools/SimTests` on the merge.
  `ListAgents`, and `TaskStop` anything that finished but still looks active.

## 4. The work
| # | Issue | What | Lane | Worker | Notes |
|---|---|---|---|---|---|
| 1 | #325 | Telemetry: the first session logs `variant` before `session_start` | A (+ Unity check) | Sonnet | A baseline bug. Read-only analyzer check |
| 2 | #328 | The recall survey waits for a calm moment | A (`CanAskNow` rule) → B wiring | Sonnet | Never over the Kit, a tool in hand, a first-run step, a toast or a caption |
| 3 | #331 | Sand hiss as occasional gusts; check the barren wind loop | A (scheduler, like `FrogCroaker`) → B | Sonnet | The user named the sand hiss. Cut the gusts from the same CC0 file |
| 4 | #333 | Rabbits: a tap startles them to another part of the meadow; they roam and spread out | A (roam/startle logic) → B | Sonnet | **Never removed by a tap.** They don't drift back to the startle spot. Keep the hunt (#283) working |
| 5 | #332 | Camp: the logs round the fire go; a clearer drying rack | B (look) | Sonnet | People sit on the ground at the fire. Screenshots at default zoom and close |
| 6 | #329 | Skip step / Skip all get a light button look | B (look) | Sonnet | Only these two buttons; `ButtonKind.Quiet` is shared |
| 7 | #281 comment | Fresh green: tone it down, fade sooner (note #321) | B (look) | Sonnet (bundle with 6) | Keep #281 open for the user's shade sign-off |
| 8 | #327 phase 1 | People's chatter: inventory of bubbles, the top caption and symbol marks, plus 2–3 options as screenshots | docs + B for shots | **Opus** | **No build.** End with `needs:user` |
| 9 | #330 | Tweezers in a forest: write the options (bonsai shoots vs saplings) | docs | Sonnet | **No build.** End with `needs:user` |

- Start lane A first, in parallel: items 1, 2, 3, 4, 9, and 8's inventory.
- Then run the Editor items one at a time: 5, then 6+7 together, then 8's screenshots. After that comes your batched
  pass (§5), which covers the lane B wiring of 2, 3 and 4.
- Issues that need the user's look, listen or feel (`needs:user`: #329, #331, #332, #281, #327, #330): build your best
  version (or write the proposal), merge it, and **keep the issue open**. Comment "Built in `<commit>` (PR #…), waiting
  for the user's sign-off on the phone." **Close only the ones that need no sign-off.**
- **Feedback notes:** when you close a work issue, close the playtest notes it answers (#321→#281, #322→#331,
  #323→#332, #324→#333) with a comment naming the issue and the commit. Notes whose issue stays open for sign-off stay
  open.
- Close work issues with `bash Tools/close-issue.sh <n> "<note>"`.

## 5. Verification (one batched pass, then the play-through)
1. Bring the Editor up on `p8/feedback-2`: health gate, the lock, the watcher, `unity recompile`, and the console
   (0 errors). If it shows "Enter Safe Mode?", fix the compile error and answer Ignore with
   `dismiss-dialogs.ps1 -Click`.
2. Run EditMode, one assembly at a time: Sim, Life, View, Audio, ShowMe, Cards, Moments, Shaping, TimeLapseScreen,
   Game.UI. The counts must be above 0.
3. Run PlayMode: every class the workers named, plus `CarePlayTests`, `WorldTapPlayTests`, `TelemetryPlayTests` (should
   be green after #325), `RabbitView`/`HuntView` play tests, `PondLifePlayTests`, `ChatterPlayTests`, the first-run
   tests, and `SmokePlay` (category).
4. Fix the failures. For each one, decide whether it belongs to the ticket or was already failing: run the class on the
   base. Re-run only what failed, plus the smoke test.
5. **The play-through. PR #326 skipped this; don't.** Run `EcoPhonePlaythrough`, then play a fresh living-land box at
   1179×2556 by touch through these beats, using time-advance cheats only:
   - wipe, mist, name the box;
   - Kit → Mister, Cloth and Tweezers (pluck a weed, a tree tuft and the vine);
   - the camp with the new rack and no logs round the fire;
   - rabbits: tap one, see it flee, and see it not come back;
   - a hunt, berries, the pond.

   Screenshot every beat and look at every shot. Any beat you can't reach by touch is a blocker: fix it before the end.
6. **Tester:** one `tester` run at the end with a narrow brief:
   - what changed;
   - what to play;
   - a pass list taken from the issues' Done when;
   - scene sanity: roots, and the mesh count before and after.

   Ask for a budget of about 45 minutes, so it finishes. In PR #326 the first broad pass covered half and a second
   narrow pass was needed. Fix every **[real]** finding and list the **[polish]** ones.
7. PR `p8/feedback-2` → `dev`. Check the diff for forbidden files. Watch CI in the background
   (`gh pr checks --watch`, with a timeout). Merge when it's green.

## 6. Checklist (copy into FEEDBACK-2-STATUS.md)
- [ ] `p8/feedback-2` from `origin/dev`; `Tools/run-tests.sh` added
- [ ] 1 · #325 merged
- [ ] 2 · #328 merged
- [ ] 3 · #331 merged (open for a listen)
- [ ] 4 · #333 merged
- [ ] 5 · #332 merged (open for look sign-off)
- [ ] 6+7 · #329 and the #281 tint merged (open for sign-off)
- [ ] 8 · #327 proposal written, shots taken (open, `needs:user`)
- [ ] 9 · #330 options written (open, `needs:user`)
- [ ] Batched Editor pass green (EditMode 10 assemblies, PlayMode classes, smoke); failures fixed or filed as baseline
- [ ] `EcoPhonePlaythrough` + the touch play-through done, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] PR → `dev`, CI green, merged
- [ ] Final report to the user:
  - what's in;
  - what waits for their sign-off: a look, or a listen on the phone;
  - the [polish] list;
  - the calls you made without them;
  - the wall-clock time per phase;
  - `docs/open-questions.md` updated
