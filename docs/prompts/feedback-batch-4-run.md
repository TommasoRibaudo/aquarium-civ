# Prompt: the fourth playtest-feedback batch (#538–#543, #495, #526)

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`. The Unity Editor can be open
or closed. It's written to run unattended and to survive context compaction. You (the session) are the **orchestrator**:
you plan, brief, review, merge, verify and close. Workers do the issues.

---

You're working through the fourth batch of playtest-feedback polish for Aquarium Civ: the notes from builds 24 and 25
(2026-11-09/10, notes #527–#537). Don't stop to ask: make the sensible call, write it down, and keep going. Stop only
for something that would break `dev`/`main`, publish anything, or that CLAUDE.md says to report. **The issues are
binding.** Each one quotes the user's own words, and the comments carry later calls. Don't reopen a decision an issue
records. **This is the polish phase: no new features beyond what the issues ask.**

## 0. Resume first
Read `docs/overnight/FEEDBACK-4-STATUS.md`. If it exists, continue from the first unchecked step and don't redo
finished work. If it doesn't, create it from the checklist in §6. After **every** finished step, add one line: the wall
clock, what was done, the commit, and anything odd. It's your memory across compaction.

## 1. Read before any code
- `CLAUDE.md` (every rule applies), `docs/workflow.md`, `docs/tooling.md` (all of it; the dated log from 2026-11-04 on
  closely), `docs/game-design-practices.md`.
- The previous batch, for how it ran and what went wrong: `docs/overnight/FEEDBACK-3-STATUS.md`,
  `docs/overnight/feedback3-pass-results.md`, PR #529.
- The issues: `gh issue view <n> -R TommasoRibaudo/aquarium-civ --comments` for every one in §4, and the playtest
  note each one quotes. **Look at the note's screenshot.** It's base64 in a folded block; decode it to your scratchpad
  (`awk '/^```$/{f=!f;next} f' | grep -E '^[A-Za-z0-9+/=]{60,}$' | tr -d '\n' | base64 -d > n.jpg`) and Read it.
- The code each issue names under Touches.

## 2. Guardrails
- **Git:**
  - Create the integration branch `p9/feedback-4` from `origin/dev`, in its own worktree `F:\GameDev-wt\feedback4`. Its
    first commit is this prompt (`docs/prompts/feedback-batch-4-run.md`; copy it from `F:\GameDev` if it isn't on
    `origin/dev` yet).
  - Give each worker its own branch `vs/<n>-<slug>`, from `p9/feedback-4`, in its own worktree `F:\GameDev-wt\<n>`.
  - At the end, open a PR from `p9/feedback-4` to `dev`, let CI pass, then merge it.
  - **Never `git commit -a`.** Stage files by name. Never commit `AquariumCiv.slnx`, `Glass.mat` or
    `AquariumCiv/.utmp/`. Never push to `main`/`dev`, and never force-push. Before the PR, check
    `git diff --stat origin/dev` for those files and for any `.unity`/`.prefab`.
- **How this batch runs (the pattern from PRs #326 and #529):**
  - Lane A workers never touch the Editor. They compile-check offline with csc (tooling.md, 2026-10-29) and run
    `dotnet test Tools/SimTests`.
  - The look and touch issues run as **one Editor worker at a time**, after the lane A work they depend on is merged.
    Before each one: `bash Tools/editor-health.sh` (exit 1 → `bash Tools/restart-editor.sh`; exit 2 → tell the user),
    then the Editor lock, then `bash Tools/watch-editor-stall.sh` in the **same message** (have it wait for the lock
    first). Restart the Editor at least every 3 tickets.
  - **Editor workers compile offline before every `unity recompile`.** In batch 3, a worker's own compile error put up
    "Enter Safe Mode?" and the watcher missed it.
  - You do **one batched Editor pass** at the end (§5), then fix what it finds.
- **Unity:** `--timeout 60` on short calls. Long ones go in the background, with a check after a minute. When anything
  stalls, run the dialog check first. Never run a bare `unity command test run`. Use `Tools/run-tests.sh`, one assembly
  or class per run. Leave **Run In Background ON** at the end.
- **Known traps:**
  - Seed-pinned tests move when a change shifts the sim's random stream. Give new randomness its own seeded stream
    (or `Terrain.Hash3`), never the main stream. If a fixture moves, find the branch, re-seed, and say why.
  - Care and EcoMistView budget tests are flaky (pass on rerun); MistDusk 20/21 h already fail on `dev`. Don't chase
    those; check against the base before calling anything a regression.
  - The debug panel is two files: `View/SimDebugPanel.cs` and `View/SliceDebugPanel.cs`. Each worker keeps its cheats in
    one block. **Every new testable state gets a cheat in the same change** (CLAUDE.md).
  - The living land's pacing is slow: in batch 3, there was no pond by y19 without cheats. Reaching a y20+ camp with a
    pond, settlers and fishing by state cheats is fine. Note it in the status file.
- **Playable, not green:** every player-facing piece works through the real HUD and real touches. Cheats only reach a
  state.
- **Closed-world writing:** people live in the tank. No outside world in any new line of text.
- **Free assets only.** Record every source and licence in `docs/assets-log.md`.
- **No publishing:** don't run `ios-testflight.yml`.
- **GitHub:** if GraphQL hits its rate limit, use `gh api` REST; don't wait. If `gh issue comment`/`edit` is blocked by
  the permission classifier, collect the commands and give them to the user at the end.

## 3. Models and agents
- **Workers default to Sonnet** (`model: "sonnet"`), one issue each, or a chain of issues on the same files.
- Use **Opus** for #543 (it spans the work planner, the stash, fishing and a new prop) and #542's repro and diagnosis
  (it's a touch and camera problem, and the cause isn't known yet).
- Brief each worker from a shared rules file in your scratchpad: the lane, offline csc, tests, cheats in one block,
  closed-world writing, commits without push. Add per issue: the issue number, the worktree and branch, Touches as its
  fence, and what *other* workers are touching right now.
- A worker's final reply is under 400 words and contains:
  - the branch and commit, and the files it changed;
  - its cheats;
  - its test results, with counts;
  - **exactly what to verify in Unity**: classes to run, and what each screenshot should show;
  - risks and open questions.
- Treat a worker's claims as unverified until you've checked them yourself: look at every screenshot it names, and run
  the base before accepting "it was already failing".
- **Every tick:** merge finished branches into `p9/feedback-4` and run `dotnet test Tools/SimTests` on the merge.
  `ListAgents`, and `TaskStop` anything that finished but still looks active.
- Near the 5 h usage limit: switch remaining work to Sonnet, keep to low-stakes tasks, and let running workers finish.

## 4. The work
| # | Issue | What | Lane | Worker | Notes |
|---|---|---|---|---|---|
| 1 | #495 | Fishers stand on the water's edge as the pond grows; no fishing on dry floor | A (spot from the eco pond's wet cells) → B check | Sonnet | The user, 2026-11-10: "they cast where there is no water when the pond is growing, I thought this was fixed." Re-check the spot each day, not once. Also covers the settlers' shore-hut fisher (`ApartGroupsView` `fishAt`) |
| 2 | #539 | Settlers' camp clear of the growing pond; their fire's glow off the water | A (`ApartSpots.FreeSpot` against the pond's full extent; move-back rule) → B (fire pool) | Sonnet | Shares the "where is water going to be" helper with #495: build it once, in #495's branch, and chain #539 after it |
| 3 | #541 | Two kids on the same game play it together | A (grouping rule + EditMode test) → B shot | Sonnet | Never two solo copies of the same game in view |
| 4 | #538 | Camera: free left/right pan across the floor up to the glass at every zoom | A (`BoxCameraMath.PanBoundsFor`) → B feel shots | Sonnet | Keep #273's rules: never through or past the glass, soft band at the limit. Keep the #273 PlayMode fixture green. `needs:user` feel sign-off |
| 5 | #526 | The dark poles by the tents: find what they are, then make them read or remove them | B (look) | Sonnet | Not the Village lanterns (`PieceKind.Lantern` is Village era). Note #537's shot: two dark posts, a coloured band, a pale tip, between tents at night. Day and night shots. `needs:user` |
| 6 | #540 | The stew pot never stands inside the drying rack | B (look) | Sonnet (bundle with 5: same camp, same shots) | Check every fire-side prop for overlaps, at several camp sizes |
| 7 | #542 | Ducks (and fish) are hard to place | B (touch) | **Opus** | Reproduce by touch at 1179×2556 first and write down the cause in the issue, then fix |
| 8 | #543 | Fish basket → fish drying rack → food pile | A (stash by kind, the work chain) → B (props, AnimLab clip) | **Opus** | After #495. One fish rack, shared with #429's smoking rack. Berries keep their basket. `needs:user` look sign-off |

- Start lane A in parallel: 1, 3 and 4. Chain 2 after 1 and 8 after 1. Run 5+6 in the Editor right away, since they're
  lane B only. Then run 7, then the lane B parts of 4, 2, 1, 3 and 8, one Editor worker at a time. After that comes your
  batched pass (§5).
- Issues that need the user's look or feel (`needs:user`: #538, #526, #543): build your best version, merge it, and
  **keep the issue open**. Comment "Built in `<commit>` (PR #…), waiting for the user's sign-off on the phone."
  **Close only the ones that need no sign-off.**
- **Feedback notes:** when you close a work issue, close the playtest notes it answers with a comment naming the issue
  and the commit: #539 → #527, #532; #540 → #531; #541 → #528; #542 → #530; #495 → its earlier note #487. Notes
  whose issue stays open for sign-off stay open: #538 → #533, #526 → #537, #499; #543 → #535.
- Close work issues with `bash Tools/close-issue.sh <n> "<note>"`.

## 5. Verification (one batched pass, then the play-through)
1. Bring the Editor up on `p9/feedback-4`: health gate, the lock, the watcher, `unity recompile`, and the console
   (0 errors). If it shows "Enter Safe Mode?", fix the compile error and answer Ignore with
   `dismiss-dialogs.ps1 -Click`.
2. Run EditMode, one assembly at a time: Sim, Life, View, Audio, ShowMe, Cards, Moments, Shaping, TimeLapseScreen,
   Game.UI. The counts must be above 0.
3. Run PlayMode: every class the workers named, plus the #273 camera glass-feel fixture, `CarePlayTests`,
   `WorldTapPlayTests`, the life-slot / placing play tests (#423/#424), the fishing and pond-life play tests,
   `Feedback3Playthrough`, `Tester2Fix`, and `SmokePlay` (category).
4. Fix the failures. For each one, decide whether it belongs to the ticket or was already failing: run the class on the
   base. Re-run only what failed, plus the smoke test.
5. **The play-through.** Add the beats below to a `Feedback4Playthrough` (explicit, its own category), then play it by
   touch at 1179×2556. Use cheats only to reach a state:
   - the camera: pan left and right at close, mid and far zoom, reaching each side wall, never past the glass;
   - the camp by day and night: the poles read or are gone; the pot and the rack stand apart;
   - two kids free at once: one game together;
   - Kit → place ducks on the pond, then fish, by touch, with no fumbling;
   - the pond growing: the fisher always stands at the water's edge, never on the floor;
   - settlers arrive while the pond is small, then the pond fills: their camp is never in or squashed by the water, and
     no firelight shows on the water;
   - a catch: fish basket → rack → food pile with fish on it.

   Screenshot every beat and look at every shot. Any beat you can't reach by touch is a blocker: fix it before the end.
6. **Tester:** one `tester` run at the end with a narrow brief:
   - what changed;
   - what to play;
   - a pass list taken from the issues' Done when;
   - the fresh-box path;
   - scene sanity: leftover roots, and the mesh count before and after.

   Ask for a budget of about 45 minutes. Fix every **[real]** finding and list the **[polish]** ones.
7. PR `p9/feedback-4` → `dev`. Check the diff for forbidden files. Watch CI in the background
   (`gh pr checks --watch`, with a timeout). Merge when it's green.

## 6. Checklist (copy into FEEDBACK-4-STATUS.md)
- [ ] `p9/feedback-4` from `origin/dev`; this prompt committed
- [ ] 1 · #495 merged (fisher on the water's edge; the shared "future water" helper)
- [ ] 2 · #539 merged (settlers' camp and its fire)
- [ ] 3 · #541 merged (kids play together)
- [ ] 4 · #538 merged (open for feel sign-off)
- [ ] 5+6 · #526 and #540 merged (#526 open for look sign-off)
- [ ] 7 · #542 cause written in the issue, fix merged
- [ ] 8 · #543 merged (open for look sign-off)
- [ ] Batched Editor pass green (EditMode 10 assemblies, PlayMode classes, smoke); failures fixed or noted as baseline
- [ ] `Feedback4Playthrough` + the touch play-through done, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released
- [ ] PR → `dev`, CI green, merged
- [ ] Final report to the user:
  - what's in;
  - what waits for their sign-off: the camera feel, the poles, the fish basket and rack;
  - the [polish] list;
  - the calls you made without them;
  - the wall-clock time per phase;
  - `docs/open-questions.md` updated
