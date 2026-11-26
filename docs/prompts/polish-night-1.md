# Prompt: polish night 1 (2026-11-25): the triaged bugs and polish, a sign-off page for the morning

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`. The Unity Editor can be open
or closed. It's written to run unattended all night and to survive context compaction. You (the session) are the
**orchestrator**: you plan, brief, review, merge, verify and close. **Sonnet workers** do the issues, and you keep sending
work back to them until every issue's *Done when* is truly done. In the morning the user reads **one sign-off page** and
gives verdicts.

---

You're working through a polish batch of Aquarium Civ: the issues triaged on 2026-11-25 from the first outside playtest
(private repo `aquarium-civ-feedback`), from the #722 story-events tester pass, and two small launch blockers. **Don't
stop to ask.** Make the sensible call, write it down, and keep going until the checklist in §7 is done. Stop only for
something that would break `dev`/`main`, publish anything, or that CLAUDE.md says to report. **The issues are binding**,
with their comments. Don't reopen a decision an issue or `docs/decisions.md` records. **This is the polish phase: no new
features beyond what the issues ask.**

## 0. Resume first
Read `docs/overnight/POLISH-NIGHT-1-STATUS.md`. If it exists, continue from the first unchecked step and don't redo
finished work. If it doesn't, create it from the checklist in §7. After **every** finished step add one line: the wall
clock, what was done, the commit, anything odd. It's your memory across compaction. Keep a **round log** per issue there
(round 1, 2, …: what the worker claimed, what you found, what you sent back).

## 1. Read before any code
- `CLAUDE.md` (every rule applies), `docs/workflow.md`, `docs/tooling.md` (all of it; the dated log from 2026-11-14 on
  closely), `docs/game-design-practices.md`, `docs/design/people-and-things.md`, `docs/design/animation-specs.md`.
- `docs/decisions.md`, especially the rows of 2026-11-24 and 2026-11-25 (the airship call; **the direction: lean into
  watching people age and die**).
- How the last run went: `docs/prompts/playtest-1-batch-2-run.md` and its status file `docs/overnight/` (the latest).
- The issues: `gh issue view <n> -R TommasoRibaudo/aquarium-civ --comments` for every issue in §4. The ones from the
  playtest link a private note: read it with `gh issue view <m> -R TommasoRibaudo/aquarium-civ-feedback` and **look at its
  screenshot** (base64 in a fenced block; decode it into your scratchpad:
  `awk '/^```$/{f=!f;next} f' | grep -E '^[A-Za-z0-9+/=]{60,}$' | tr -d '\n' | base64 -d > n.jpg`, then Read it).
- The code each issue names.

## 2. Guardrails
- **The user merges. You never merge a PR**, never push to `main`/`dev`, never force-push. You open the PR (§5) and stop
  there.
- **Privacy (user rule):** the code repo is public. Nothing from a private note goes into a public issue, commit, PR,
  doc, test name or the sign-off page's repo copy: no tester quotes, screenshots, device or tester id. Paraphrase, and
  link the private note. Decoded screenshots stay in your scratchpad.
- **Git and worktrees:**
  - `F:\GameDev` itself holds someone's uncommitted work. **Never work, stage, stash or switch branches there.**
  - The Editor's worktree is `F:\GameDev-wt\pt1b` (the Editor is usually open on it). Create the integration branch
    `p9/polish-night-1` from `origin/dev` **in that worktree** (`git -C F:/GameDev-wt/pt1b checkout -b p9/polish-night-1
    origin/dev`; first check `git status` there is clean apart from untracked scene files). Its first commit is this
    prompt (`docs/prompts/polish-night-1.md`, from `origin/dev` or `origin/docs/polish-night-1-prompt`).
  - Each worker gets its own branch `vs/<n>-<slug>` from `p9/polish-night-1`, in its own worktree `F:\GameDev-wt\<n>`.
    Clean worktrees up when their branch is merged.
  - **Never `git commit -a`.** Stage files by name. Never commit `AquariumCiv.slnx`, `Glass.mat`, `AquariumCiv/.utmp/`,
    `_Recovery/`, `InitTestScene*.unity`, or a stray save scene. Before the PR, check `git diff --stat origin/dev` for
    those and for any `.unity`/`.prefab`.
- **Animation sign-off (user rule):** any **new** people-and-things animation or beat needs a spec sheet in
  `docs/design/animation-specs.md` that the user signs off, then a clip the user signs off, **before it is merged into
  `dev`**. Tonight that's #776 (the statue and how people use it: spec only, no build) and any beat a worker adds that
  didn't exist (say so in the review). Those live on their own branch, not in `p9/polish-night-1`. **Fixes to existing
  motion** (#767 the rod, #770 the dance spot, #771 the wolf's turn, #772 the ball off the glass, #779 the scene spots)
  are merged, and their clips go on the sign-off page with the issue left open.
- **Lanes:**
  - Lane A workers never touch the Editor: offline csc (`Tools/offline-csc.py`, name every assembly a change touches in
    one call, tooling.md 2026-11-25) and `dotnet test Tools/SimTests`.
  - Lane B (look, touch, UI, motion) runs **one Editor worker at a time**, on the Editor in `F:\GameDev-wt\pt1b`, with the
    worker's branch checked out there (detached) for its run. Before each: `bash Tools/editor-health.sh` (exit 1 → `bash
    Tools/restart-editor.sh` with `PROJ=F:/GameDev-wt/pt1b/AquariumCiv`; exit 2 → tell the user), take
    `F:/GameDev/.agent-editor.lock`, and keep a watcher: a background loop that checks `unity status` and
    `Tools/unity-dialogs/dismiss-dialogs.ps1` every minute.
  - Editor workers compile offline before every `unity recompile` (a compile error puts up "Enter Safe Mode?").
- **Unity:** `--project-path F:/GameDev-wt/pt1b/AquariumCiv` with forward slashes from Bash. `--timeout 60` on short
  calls; long ones in the background with a check after a minute. Stalls: run the dialog check first. Never a bare
  `unity command test run`: use `Tools/run-tests.sh` with `PROJ=...`, one assembly or class per run, `STALL_S=600` for the
  Sim and Life assemblies (their tests run minutes without logging: a "STALLED" line there may be the runner giving up,
  not Unity; check `unity command test status` before rerunning). **Never edit files in `pt1b` while a test batch runs
  there.** Leave Run In Background ON.
- **Known traps:**
  - PRs into `dev` skip the Unity suite, so **your full local pass (§5.2) is the only Unity check before `main`.** The
    0.4.1 release broke on six tests that had gone stale on `dev` unnoticed (2026-11-25). Inconclusive counts as a
    failure in CI: a test that `Assume`s its setup is a bug; make it set up what it needs.
  - The land is 6 x 6 with its centre at z = `Land.CZ` (0.8), not 0. Camera bounds are offsets from it.
  - The debug panel is two files: `View/SimDebugPanel.cs` and `View/SliceDebugPanel.cs`. Each worker keeps its cheats in
    one block. **Every new testable state gets a cheat in the same change.**
  - Glass panes leak if a view rebuilds without destroying the old ones: count panes before and after in §5.
  - `BoxLeakTests.AfterADomainReload_OneBoxAndNoOrphans` is inconclusive in an interactive Editor by design; CI runs it.
- **Playable, not green:** player-facing pieces work through the real HUD and real touches at phone size (750x1334 and
  1179x2556). Cheats only reach a state.
- **Closed-world writing:** people live in the tank. No outside world in any new line (#773, #781, #564).
- **Seen, not read** beats a line of text. **People use everything** (core rule): anything new in the box ships with its
  work and life uses.
- **Free assets only**, recorded in `docs/assets-log.md`.
- **No publishing:** don't run `ios-testflight.yml` (0.4.1 is with the user now).
- **GitHub:** GraphQL rate limit → `gh api` REST (issue comments, close, labels); board moves wait for the reset. If a
  command is blocked by the permission classifier, collect it and give it to the user at the end.
- **New feedback notes** that arrive overnight (the user is testing 0.4.1): read them, add `fb:triaged` with a comment,
  and file problems as issues the same way, **without** starting them. Praise and pulse notes get their row in
  `docs/design/results/praise-and-pulse.md` in your own words. List them all in the report.

## 3. Agents: Sonnet, iterated until done
- **Every worker is Sonnet** (`model: "sonnet"`), one issue each or a chain on the same files. You review.
- Brief each worker from a shared rules file in your scratchpad (the lane, offline csc, tests, cheats in one block,
  privacy, closed-world writing, commit without push), plus: the issue, the worktree and branch, the files it may touch
  (its fence), what *other* workers are touching now, and the issue's *Done when* as its checklist.
- A worker's final reply (under 400 words): the branch and commit, files changed, cheats, test results with counts, each
  *Done when* box with its evidence, **exactly what to verify in Unity** (classes, what each screenshot should show),
  risks.
- **The review loop.** After every worker reply:
  1. Treat its claims as unverified. Read the diff. Run its tests yourself. Look at every screenshot it names. Run the
     base before accepting "it was already failing".
  2. Check each *Done when* box against the evidence and the issue's words.
  3. Anything short → send it back with `SendMessage` (a numbered list, each with the evidence). If the worker is gone,
     start a fresh Sonnet worker on the same branch with the round log as its brief.
  4. Repeat until every box is ticked; log each round.
  5. After 4 rounds without convergence: write what's short in the issue, merge what's solid or leave it unmerged, and
     list it for the user.
- **Every tick:** merge accepted branches into `p9/polish-night-1`, run `dotnet test Tools/SimTests` on the merge, start
  any lane A work whose files don't overlap. `ListAgents`; `TaskStop` anything finished that still looks active.
- Wait in the background; no poll loops. Near the 5 h usage limit: low-stakes tasks only, let running workers finish.

## 4. The work
| # | Issue | What | Lane | Notes |
|---|---|---|---|---|
| 1 | #766 | The remembered person's note stays on top; other cards don't open | B (UI) | One sheet at a time; PlayMode test |
| 2 | #764 | The wedding's "I do" before the ceremony | A → B | The caption waits for the vows beat |
| 3 | #765 | Two fishermen on one spot | A | Spots reserved per fisher; test |
| 4 | #762 | Fields over the leather rack | A | As #700 keeps racks off lots; test |
| 5 | #763 | The feather pole flickers when panning | B | Find the cause first; write it in the issue |
| 6 | #779 | People inside each other in the story scenes | A → B | 1.2, 2.1, 5.2; spots a body width apart; tests; `StoryEventShots` clips |
| 7 | #784 | Two story cheats refuse in an ordinary box | A | The cheat sets up what it needs |
| 8 | #782 | "Nothing to eat" bubble during the fruit pick | A | The line reads the box's state |
| 9 | #780 | The 4.1 hint hidden by the buttons on small phones | B (UI) | All four asks at both sizes |
| 10 | #781 | The song-name hint says "at it" | A (words) | Words go on the sign-off page |
| 11 | #773 | "One big pot": one endless stew, or a fresh one each time? | A (words) | Words go on the sign-off page |
| 12 | #564 | The glass voice's line on the first mist | A (words) + B | Launch blocker; words on the sign-off page |
| 13 | #767 | The rod leaves the water during a chat | B (motion) | Clip on the sign-off page |
| 14 | #770 | The dance in the way of buildings | A → B | Open ground; test; clip |
| 15 | #771 | Wolves jitter turning in place | B (motion) | QuadReel clip on the sign-off page |
| 16 | #772 | The glass stops a thrown ball | A → B | Follows #744; test; clip |
| 17 | #768 | Fields next to the town centre | A (Settle) | Keep organic placement (decisions 2026-10-29); 3 seeds before/after |
| 18 | #769 | The church inside the wood | A (Settle) → B | Screenshot of a village with its church |
| 19 | #775 | Dirty glass reads dirtier, stays visible zoomed in | B (look) | Shots: clean/half/dirty × home/zoomed |
| 20 | #559 | Trash and weeds read as what they are | B (look) | Launch blocker; decided "rework both" (decisions 2026-11-10) |
| 21 | #783 | Story scenes are specks at the default zoom | spec only | A short proposal of the camera behaviour (check #672, #560) for the sign-off page; **no build** |
| 22 | #774 | The person card and History: family, relations, stories, a family tree | mock-ups only | HTML mock-ups for the sign-off page; **no build** until the user picks |
| 23 | #776 | A statue of the founder | spec only | Spec sheet (when, where, how people use it) for the sign-off page; **no build** |
| 24 | #777 | Research: lean into watching people age and die | docs | `docs/research/23-ageing-and-death.md`: what the game does now, games that do it well, 5–8 options within polish, ★ a recommendation |
| 25 | #118 | Flaky `PersonCardPlayTests.TapADoll` friend line | B (test) | Find the cause; fix the test or the code; 10 runs green |

**Out of scope tonight** (don't start): the 22 issues built and waiting for the user's 0.4.1 phone check (#566, #643–#645,
#647–#650, #652–#657, #659, #665, #670, #672, #673, #675, #677, #680), the design issues that need the user first
(#249, #668, #685, #686, #688, #691, #761), the P9 scenes (#428–#430, #440–#444, #447), P10, and #753 (the test rework).

- Lane A first, in parallel where the files don't overlap: 3, 4, 7, 8, 10, 11, 17, 2 (sim half), 6 (sim half), 14 and 16
  (rules), and the docs workers for 21, 22, 23, 24.
- Lane B, one Editor worker at a time, in this order: 1, 5, 9, 2, 6, 12, 13, 14, 15, 16, 18, 19, 20, 25.
- **Closing:** close an issue only when it needs no look from the user: #762, #765, #766, #784, #118, #782 when their
  *Done when* is proven (`bash Tools/close-issue.sh <n> "<note>"`). Everything else: build your best version, merge it
  (except the spec-gated ones), and **keep the issue open** with "Built in `<commit>` (PR #…), waiting for the user's
  sign-off (polish night 1)."
- **Feedback notes:** when you close an issue that answers a private note (its body links it), close the note with a
  comment naming the issue and the commit.

## 5. Verification (one batched pass, then the play-through and the tester)
1. Bring the Editor up on `p9/polish-night-1` with everything merged: health gate, lock, watcher, `unity recompile`,
   console 0 errors.
2. **The full EditMode suite, as CI runs it**, one assembly per run: `AquariumCiv.Sim.Tests`, `AquariumCiv.Life.Tests`,
   `AquariumCiv.View.Tests`, `AquariumCiv.Cards.Tests`, `AquariumCiv.Audio.Tests`, `AquariumCiv.Game.UI.Tests`,
   `AquariumCiv.Moments.Tests`, `AquariumCiv.Shaping.Tests`, `AquariumCiv.ShowMe.Tests`,
   `AquariumCiv.TimeLapseScreen.Tests`. **Zero failures and zero inconclusive** (except `BoxLeakTests` in the
   interactive Editor). Also `dotnet test Tools/SimTests` green.
3. PlayMode: every class the workers named, `StoryEventShots` (EXPLICIT=1), and `SmokePlay` (category).
4. Fix failures through the review loop. Decide each against the base (run the class on `origin/dev`). Re-run only what
   failed, plus smoke.
5. **The play-through by touch** at 1179x2556 and 750x1334, from a fresh box, cheats only to reach a state: the
   remembered note and another card; a wedding; two fishers; a field by the rack; the pole while panning; the story
   scenes 1.2, 2.1, 5.2; the four asks; the fruit pick's bubbles; the first mist; a fishing chat; a dance in a grown
   village; a wolf turning; a ball at the glass; a village's fields and church; dirty glass home and zoomed; trash and
   weeds. Screenshot every beat and look at every shot.
6. **Tester:** one `tester` run with a narrow brief (what changed, what to play, the pass list from the *Done when*
   boxes, the fresh-box path, scene sanity: leftover roots, mesh count and glass panes before and after; about 45
   minutes). Fix every **[real]** finding; list the **[polish]** ones.
7. Open the PR `p9/polish-night-1` → `dev` and watch its CI to the end. **Don't merge it.** Spec-gated branches get no PR.

## 6. The sign-off page for the morning
The user signs off from **one page**, read on the phone or the desktop.
1. **Collect the media** in your scratchpad: for each item a GIF of the motion fixes (frames from the PlayMode shots, PIL,
   real-time frame duration, skip the first frame if it shows the glass edge) or before/after screenshots for looks,
   the words options for #781, #773 and #564, the mock-ups for #774, the specs for #776 and #783, and the research
   summary for #777.
2. **Publish it as a private Artifact** (load the `artifact-design` skill first; the page from 2026-11-25's #722 events
   sign-off, built in `scratchpad/events722`, is the model). One card per item: its issue, what changed in two lines, the
   media, the question, and three verdict buttons (**Good / Needs work / Not sure**) with a note field. At the top: what's
   in, what's merged, what waits. At the bottom: a **Copy all verdicts** button that puts the verdicts as plain text
   (`#n Good` / `#n Needs work: note`) on the clipboard, so the user pastes them back in one message. No tester quotes on
   the page (it may be shared): paraphrase.
3. Also write `docs/overnight/polish-night-1-signoff.md` (the same items, questions and media paths, no media) and commit
   it to `p9/polish-night-1`.
4. **When the user answers** (if in this session): act on the verdicts. Move them into `docs/decisions.md`; merge the
   approved spec-gated work (a follow-up PR to `dev`); close the approved issues and their notes; send "Needs work" items
   back through the review loop; file follow-ups.

## 7. Checklist (copy into POLISH-NIGHT-1-STATUS.md)
- [ ] `p9/polish-night-1` from `origin/dev` in `F:\GameDev-wt\pt1b`; this prompt committed
- [ ] Bugs: #766 · #764 · #765 · #762 · #763 (cause in the issue) merged
- [ ] Story polish: #779 · #784 · #782 · #780 merged; #781 words ready
- [ ] Words: #773 · #564 ready (and #564 wired)
- [ ] Motion: #767 · #770 · #771 · #772 merged, clips recorded
- [ ] Town: #768 · #769 merged (3 seeds before/after)
- [ ] Looks: #775 · #559 merged, shots recorded
- [ ] Specs and mock-ups: #783 proposal · #774 mock-ups · #776 spec (own branch)
- [ ] Research #777 written (`docs/research/23-ageing-and-death.md`)
- [ ] #118 fixed, 10 runs green
- [ ] Every issue's *Done when* ticked with evidence, or listed as short after 4 rounds
- [ ] Full EditMode suite (10 assemblies) zero failures and zero inconclusive; SimTests; PlayMode classes; smoke
- [ ] The touch play-through at both sizes, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Sign-off page published (private), every card's media looked at; `polish-night-1-signoff.md` committed
- [ ] Closable issues closed (and their notes); the rest left open with "waiting for the user's sign-off"
- [ ] New feedback notes triaged (none started)
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released; Editor ready
- [ ] PR `p9/polish-night-1` → `dev` open, its CI watched to the end (not merged)
- [ ] Final report to the user:
  - the sign-off page's link, and how many items (about how many minutes);
  - what's in the PR, and what waits on the sign-off;
  - the calls you made without them;
  - anything short after 4 rounds;
  - the tester's [polish] list;
  - new feedback notes from the night;
  - wall-clock time per phase.
