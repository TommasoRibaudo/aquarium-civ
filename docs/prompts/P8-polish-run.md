# Prompt: polish the approved opening, the meadow with rabbits and berry bushes, and the Polish backlog

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`, with the Unity Editor
open on `AquariumCiv`. It's written to run unattended and to survive context compaction. You (the session) are the
**orchestrator**: you plan, brief, review, merge and close. Workers do the issues.

---

You're finishing the **living-land opening** of Aquarium Civ, from the first frame to a camp with a meadow, rabbits,
a hunt and berry bushes. You're also clearing the **Polish** milestone. That's every open issue in the GitHub
milestone "Polish" (#208–#211, #266–#288), plus the leftovers of #246, #248 and #249. Don't stop to ask: make the
sensible call, write it down, and keep going. Stop only for something that would break `dev`/`main`, publish
anything, or that CLAUDE.md says to report.

The user approved the opening on 2026-11-03 (build 16): the first run, the first misting, the water table, and the
pond as the sign of the water level. Their calls on how each part should feel are written into the issues. **The issues
are binding.** Don't reopen a decision an issue records.

## 0. Resume first
Read `docs/overnight/P8-POLISH-STATUS.md`. If it exists, continue from the first unchecked step and don't redo
finished work. If it doesn't, create it from the checklist in §6. Update it after **every** finished step (one line:
what, commit, anything odd), and check the wall clock at every update. It's your memory across compaction.

## 1. Read before any code
- `CLAUDE.md` (every rule applies), `docs/workflow.md` (lanes, Editor lock, the loop, baseline first),
  `docs/tooling.md` (all of it, including the dated log), `docs/game-design-practices.md`.
- **The design:**
  - `docs/design/keepers-hands.md` §2, §2.1 and §5: the arc, the tribe before the camp, the water table and humidity.
  - `docs/design/results/P8.md`: what's built, the tester's findings and the phone fixes after build 15.
  - `docs/research/15-barren-to-living-box.md` §3–§4 (the ecology rules, the box's stages) and
    `docs/design/spikes/ecology-lab.md` §5–§6 (the model's rules and its tuned constants).
- **The issues:** `gh issue list -R TommasoRibaudo/aquarium-civ --milestone Polish --state open`, then
  `gh issue view <n> --comments` for each one. **The comments carry later decisions**: #266's "every mist shows a
  little green at once", #267's clean water from the pond, and #271's "tune together with #266". Each issue's
  What, Touches and Done when are binding.
- The code each issue names under Touches.

## 2. Guardrails
- **Git:**
  - Create the integration branch `p8/polish` from `origin/dev`.
  - **Its first commit** is this prompt, copied from `F:\GameDev-wt\ecology-lab\docs\prompts\P8-polish-run.md`
    into `docs/prompts/`.
  - One branch per issue or bundle, `vs/<n>-<slug>`, from `p8/polish`. Lane A work goes in its own worktree under
    `F:\GameDev-wt\<n>`. Merge finished work into `p8/polish`. At the end, open a PR from `p8/polish` to `dev`
    (`gh pr create`), let CI pass, then merge it.
  - Never commit `AquariumCiv/AquariumCiv.slnx`, `Glass.mat` or `AquariumCiv/.utmp/`. Never push to `main`/`dev`, and
    never force-push.
- **The living land stays behind its switch:** with `SimConfig.ecology` off, every existing test and fingerprint must
  stay green and the old slice must play as before. The exceptions are the polish issues that are about the old slice
  too: #208–#211, #272–#276.
- **Unity, the same orchestration as every earlier run:**
  - **Editor lock:** a worker driving the Editor writes `F:\GameDev\.agent-editor.lock` (owner, issue, start time) and
    deletes it when done. Only one Editor worker at a time. Check the lock before starting any lane B work.
  - **Health gate:** run `bash Tools/editor-health.sh` before taking the lock. Exit 1 → `bash Tools/restart-editor.sh`;
    exit 2 → tell the user. Restart at least every 3 tickets.
  - **Stall watcher:** every time an agent takes the lock, start `bash Tools/watch-editor-stall.sh` in the background
    **in the same message**. When it exits with STALL, act at once: read the status, then resume the agent or finish the
    work yourself.
  - Short Unity calls take `--timeout 60`. Long ones run in the background, with check-ins every minute. When anything
    stalls, run the dialog check first. Never run a bare `unity command test run`. No YAML hand-edits while the
    Editor is reachable.
  - Tell every worker: **when a monitor ends with work still running, carry on, don't hand back.**
- **Tests:** batch them (several small tickets → one Editor pass), and don't rerun what already passed on the same
  commit. **Baseline first:** a failure that also fails on the base commit isn't the ticket's. Merge the ticket, file
  one issue per failing class, and hunt it as its own ticket with a **90-minute time box** (report at 60). Run the full
  EditMode suite after each merge of sim work that touches `Assets/`, or at least every 4 hours.
- **Debug panel stays current:** every new state (rabbits, the hunt, puddles, berries, frogs, ducks, the pond's first
  water, new grass since the last visit) gets a cheat in the same change, as each issue says.
- **Playable, not green:** every player-facing piece must work through the real HUD and real touches. Cheats are only
  for reaching a state, never for proving that the player can do something.
- **Free assets only** (Kenney, KayKit, CC0 sound). Record every source and licence in `docs/assets-log.md`.
- **No publishing:** don't run `ios-testflight.yml`. Write the one command the user runs.

## 3. Models and agent comms
- **Default worker: Sonnet** (`model: "sonnet"`). One substantial issue, or one chain of issues on the same files, per
  agent.
- **Bundles of simple issues: one Opus worker** (`model: "opus"`) does the whole bundle in one Editor pass and one
  test batch.
- **The tester:** the `tester` agent (`.claude/agents/tester.md`), on its cadence (§5).
- **Briefing a worker:** give it the issue number(s) and tell it to read the issue and its comments, CLAUDE.md,
  `docs/workflow.md`, and the relevant design sections. Tell it:
  - its branch and worktree;
  - its lane, and whether it may take the Editor lock;
  - that its `Touches` are its fence;
  - its Done when;
  - "Batch your Unity tests; run only your own feature tests plus `SlicePlaythroughSmoke`."
- **When a worker finishes:** it pushes its branch, comments on the issue with what it did, the tests and screenshot
  paths, and hands back. To continue a worker with its context intact, use `SendMessage` to its ID (for fixes after
  review); don't start a fresh agent.
- **Every loop tick:**
  - (a) `ListAgents`, and `TaskStop` any agent that finished but still looks active.
  - (b) Fewer than 3 lane A agents running → start the next item whose dependencies are done and whose Touches don't
    overlap anything running. Also start phase 1 (the pure logic) of a `needs:editor` item while the Editor is busy.
  - (c) The Editor is free → start the next lane B item.
- **Near the 5-hour usage limit:** let running agents finish, start no new Opus bundles, and keep your own replies short.

## 4. The work, in order
Lane A = no Unity (`dotnet test Tools/SimTests`), several in parallel. Lane B = the Editor, one at a time.

| # | Work | Issues | Worker | Lane | After |
|---|---|---|---|---|---|
| 1 | **Quick fixes for the first run and the UI**: every fogged pane can be wiped in the first run; the first run stays in daylight; scrolling glides; the Offices list scrolls | #268, #278, #272 | **Opus bundle** | B | — (do it first: the user keeps having to skip the tutorial) |
| 2 | **The grass's pace**: grass spreads on its own; the mist's sim part (a little green at once, the rest of the wet area greens over time, a bigger radius); retune so the camp gate stays near the lab's 3–4 years with care | #271, the sim half of #266 | Sonnet | A | — |
| 3 | **Jobs follow the land**, then **dirty water while the pond is dry and clean water from the pond** (same planner files, so one agent runs both in a row) | #285 → #267 | Sonnet | A, then B for the cards and the bucket look | — |
| 4 | **History keeps the land**: phase 1 is the records and the save (lane A); phase 2 is the replay drawing (B) | #287 | Sonnet | A → B | — |
| 5 | **The misting rework** (view): the spray, the sound, the wet area, small puddles that people use, then **no stutter** (same files) | #266 → #277 | Sonnet | B | #2 |
| 6 | **The water**: the pond reaches the glass, the water table shows as damp dirt (both in `BoxView.Eco.cs`) | #269 → #270 | Sonnet | B | — |
| 7 | **The pond's first water is a moment**, then **frogs and ducks** | #279 → #286 | Sonnet | B | #6 |
| 8 | **Rabbits in the box** (tracks, then rabbits) | #282 | Sonnet | A (sim if needed) → B | #2 |
| 9 | **The hunt, rebuilt** (hunters follow the rabbits until the farming age) | #283 | Sonnet | A (planner) → B | #3, #8 |
| 10 | **Hides become the new tents; the camp begins as a small moment** | #284 | Sonnet | B | #9 |
| 11 | **Berry bushes**: berries by season, gatherers at the bushes that grew, the first bush is a sign | #288 | Sonnet | A (planner) → B | #3 |
| 12 | **New grass since your last visit is a fresher green** | #281 | Sonnet | B | #2, #5 |
| 13 | **The opening's sound** (recorded CC0: the barren bed, frogs, the water trickle, mixed with the mister's fsss) | #280 | Sonnet | B | #5 |
| 14 | **P8 leftovers**: check #246, #248 and #249 against the code and close what's done. Build the one missing beat: **the tribe moves its fire next to the first green** (keepers-hands §2.1) | #246, #248, #249 | **Opus bundle** | A → B | #2 |
| 15 | **The camera moves freely up to the glass** | #273 | Sonnet | B | — |
| 16 | **Bridges where paths cross canals** | #274 | Sonnet | B | — |
| 17 | **Rituals**: the doll stops clipping into roofs, then symbol bubbles | #275 → #276 | Sonnet | B | — |
| 18 | **Older polish**: chores staggered at dawn, Alwin's leader line, the breadcrumbs | #208, #209, #210, #211 | **Opus bundle** | B | — |

- The order of lane B: 1 → 5 → 6 → 8 → 9 → 10 → 11 → 7 → 12 → 13 → 14 → 3 (B part) → 4 (B part) → 15 → 16 → 17 → 18.
  The opening and the meadow come first, the old-slice polish last. Swap items when one is blocked.
- **Issues that need the user's look or feel sign-off** (`needs:user`): don't wait. Build your best version against
  the user's recorded calls, merge it, and **keep the issue open** with a comment: "Built in `<commit>`; waiting for the
  user's sign-off on the phone." List each one in the final report.
- **Reviewing and merging (you):**
  1. Read the diff.
  2. Rerun the tests (`dotnet test Tools/SimTests` for anything in Sim/Life, and Unity tests for anything under
     `Assets/`).
  3. Look at the screenshots.
  4. Merge into `p8/polish`.
  5. Close the work issue with `bash Tools/close-issue.sh <n> "<note>"`, so the card moves.
  6. **Close the player feedback it answers**: each work issue has a comment "Feedback to close with this: Closes #…".
     Close those with a comment naming the work issue and the commit.

## 5. Testing
- **Per item:** its Done when, plus `SlicePlaythroughSmoke` for anything in View, UI or Runtime. PlayMode UI tests call
  `GameUiPanel.AcceptInputUnfocused(true)`. Keep `EcoPhonePlaythrough`, `EcoFirstRunTests` and `EcoMistPlayTests`
  green, and extend `EcoPhonePlaythrough` as the opening grows (rabbits seen, a hunt, berries picked).
- **The `tester` agent:** run it after **6 lane B items or 4 hours of merged Editor work**, whichever comes first, and
  again at the end. Brief it with:
  - what changed;
  - what to play: a fresh living-land box at portrait phone sizes (1179×2556 and 1080×1920), by touch;
  - what to watch for: misting must feel great (the spray, the sound, a wet area at once, a little green at once, small
    nice puddles, no pale blotches, no stutter); the pond meets the glass; the water table reads as damp dirt with no
    line; rabbits and the hunt read at default zoom; no woodcutter or berry picker without trees or bushes; the tribe's
    names before the camp;
  - a pass list, taken from the issues' Done when;
  - the fresh-box path;
  - scene sanity: leftover roots, mesh count.

  Don't touch the Editor while it runs. Fix every **[real]** finding (one issue each, same loop). List the **[polish]**
  ones for the user.
- **The final play-through (the bar since build 15).** Before you call the run done, play a fresh living-land box at
  1179×2556 through the real HUD by touch:
  1. Wipe **every** fogged pane.
  2. Mist: you get the wet area, a little green and the sound.
  3. Name the box.
  4. Open the Kit and mist several spots with the Mister.
  5. Use the Cloth on the fog.
  6. See the grass spread on its own; see new grass in the fresher green after an away.
  7. See rabbit tracks, then rabbits.
  8. A hunter follows them and makes a catch; hides dry by the fire.
  9. The camp begins as its small moment.
  10. Gatherers pick berries at real bushes.
  11. The pond's first water meets the glass, with its moment, then frogs.
  12. History shows the land growing.

  Only **time-advance** cheats (+1 season, +1 year, "away for N hours") may be used to cover the years between beats.
  Never use gate cheats or action cheats. Screenshot each beat and look at every shot. Any beat you can't reach by
  touch is a **blocker**, not a footnote: fix it before the end.
- **Build prep:** write the command the user runs, `gh workflow run ios-testflight.yml -f notes="P8 polish: opening,
  meadow, rabbits, hunt, berries"`, in `docs/design/results/P8.md`. Don't run it.

## 6. Checklist (copy into P8-POLISH-STATUS.md)
- [ ] Branch `p8/polish` from `origin/dev`; this prompt committed
- [ ] 1 · #268 #278 #272 (Opus bundle) merged, closed
- [ ] 2 · #271 + #266 sim merged
- [ ] 3 · #285 → #267 merged, closed
- [ ] 4 · #287 merged, closed
- [ ] 5 · #266 → #277 merged (#266 open for sign-off)
- [ ] 6 · #269 → #270 merged (open for sign-off)
- [ ] 7 · #279 → #286 merged
- [ ] 8 · #282 merged
- [ ] 9 · #283 merged
- [ ] 10 · #284 merged
- [ ] 11 · #288 merged
- [ ] 12 · #281 merged, closed
- [ ] 13 · #280 merged (open for a listen)
- [ ] 14 · #246 #248 #249 checked and closed; the fire moves to the first green
- [ ] 15 · #273 merged (open for sign-off)
- [ ] 16 · #274 merged
- [ ] 17 · #275 → #276 merged
- [ ] 18 · #208–#211 (Opus bundle) merged, closed
- [ ] Linked player feedback closed for every closed work issue
- [ ] Tester runs on cadence; [real] findings fixed; [polish] listed
- [ ] Full EditMode suite + `SlicePlaythroughSmoke` + `EcoPhonePlaythrough` green on `p8/polish`; ecology off = the slice is unchanged
- [ ] The final play-through done by touch, every beat screenshotted and looked at
- [ ] `P8.md` updated: what's in, the tests, the build command
- [ ] PR `p8/polish` → `dev`, CI green, merged
- [ ] Final report

## 7. The final report (to the user, at the end)
Keep it short and plain:
- What was built, and the PR link.
- What the play-through and the tester found, with the [polish] items.
- **What the user does next**, as a list:
  - run the TestFlight command;
  - play the opening on the phone, from a fresh box to the berries;
  - sign off each `needs:user` issue still open (list them, each with what to look at).
- Put every open question and every call you made without the user in `docs/open-questions.md`, and publish them as
  an open-questions artifact with a copy button, as in earlier autonomous runs.
