# Prompt: work while the user tests Gates 2 and 3 (build 0.3.0)

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`, with the Unity Editor open
on `AquariumCiv`. It runs over several days, alongside the user's test session, and must survive context compaction.

---

The user and their friends are testing **TestFlight 7 = 0.3.0** (Test Hub, P3–P6) with the guide in
`docs/design/test-session-gate2-3.md` (artifact: https://claude.ai/artifact/1YB4s8BGquSAKtQayG1CPU). Gate 1 is decided:
**P2 variant B** (mess dims the sunlight and slows the work pace, and the people's mood shows it; `docs/decisions.md`).
Your job until Gates 2 and 3 are decided:
1. turn incoming test data into results the user can decide with,
2. keep the test running (blocking bugs only, never changes that alter what is being compared),
3. prepare the work that comes after Gate 3 (the vertical slice), without deciding anything that is the user's to decide.

The user is busy testing. Don't ask questions you can answer yourself: make the sensible call, write it down, carry on.
Do ask before anything in "Ask first" below.

## 0. Resume first
Read `docs/testing/STATUS.md` if it exists and continue from the first unchecked step; otherwise create it from §6.
Update it after every finished step (one line: what, commit hash, anything odd). It's your memory across compaction.

## 1. Read before acting
- `CLAUDE.md` (all rules: never let a Unity call hang silently, the loop after every C# change, the `tester` sub-agent,
  the debug panel stays current, free assets only).
- `docs/tooling.md`, all of it, especially the 2026-10-29 Log entries (overnight notes, tester harness recipes, shared
  working tree, recompile/reload traps, heredoc trap).
- `docs/overnight/REPORT.md`, `docs/design/prototype-plan.md` (Gates 2 and 3, §6–7), `docs/design/game-definition.md`,
  `docs/game-design-practices.md`, `docs/feedback.md` (issue format), `docs/design/results/P2.md`–`P6.md` (each ends with
  a polish list and open questions).

## 2. Guardrails
- **Don't change what is being compared.** Builds 0.3.x are a live A/B test. Until the user closes Gates 2 and 3, P3–P6
  behaviour, variants, pacing, wording and logging stay as shipped in `proto/P*-v1`. Only fix bugs that block a tester
  (crash, stuck, lost save, broken input, a metric that logs wrong data); write each in the prototype's results file
  as "fixed during the test" with the date, so the analysis can account for it.
- **No TestFlight runs** unless the user says so. Prepare a fix branch and the one command; the user starts it. A fix
  ships as the next patch (0.3.1, 0.3.2, ...) with its build-log row in `docs/testflight.md`.
- **Gate decisions belong to the user.** You fill the decision sheets with numbers and quotes and give a
  recommendation; you never write a gate decision into `docs/decisions.md` yourself.
- **Git:**
  - Test fixes go on `overnight/p3-p6` (the branch builds come from). Tag each shipped fix `proto/P3-v1.1` etc.
  - Post-Gate-3 preparation goes on a new branch `vs/prep` from `overnight/p3-p6`. It must not touch the prototype
    folders' behaviour, and it must not be what TestFlight builds from.
  - Push your branches as backup. Don't merge, don't touch `main`/`dev`, don't open PRs, never force-push.
- **Shared working tree:** another session (the user's, e.g. `gamedev-05`) may be editing in this tree and using the
  Editor (it ran a sound pass: `Juice`, `MessView`, `WipeCleaner`, `RubSynth`, `SoundPreview`). Before every commit, check
  `git status` for files you didn't write and stage paths explicitly (never `git add -A`). Agree Editor time by message
  (`ListAgents`, `SendMessage`) before long play-mode or tester runs. Never revert someone else's files.
- **Unity rules (CLAUDE.md):** status check before edits, `--timeout` on every call, background + 1-minute check-ins,
  never hand-edit scene/prefab YAML while the Editor is reachable, no package installs or `unity self-update`.
- **P2 stays protected:** `AquariumCiv.Prototypes.Shared.Tests` (incl. `P2WorldIsUnchangedByLaterPrototypes`) green
  after every shared change.
- **Privacy:** feedback issues and logs carry tester nicknames; never copy them anywhere outside this repo's docs.

## 3. Results: from test data to decision sheets (main job, repeat as data arrives)
- Read the GitHub feedback issues (`gh issue list -R TommasoRibaudo/aquarium-civ --label feedback --state all`,
  then `gh issue view`): surveys (`[P3·A] Survey…`), pair preferences (`[P3] A/B preference…`), free feedback.
  Survey and session summaries are in the issue bodies (see `docs/feedback.md`).
- For each prototype keep a "Results" section in `docs/design/results/Pn.md`:
  - a table per variant: testers, Q1/Q2/Q4 medians, the A/B preference count, and Q3 recall answers classified as
    "names a specific person or event" or not (for P3 this is the key measure: quote them);
  - the logged metrics the decision sheet asks for (cards read and dwell, history opens unprompted, nudges per
    check-in, seconds waited and watched at an era, old-box time), from the session summaries;
  - quotes that explain the numbers (short, nickname only), and anything that looks like a bug.
- Fill the **Result** column of the Gate 2 and Gate 3 sheets in `docs/design/test-session-gate2-3.md` as soon as a
  question has enough data (say how many testers it rests on). When a gate's sheet is complete, write a short
  recommendation at the bottom of that sheet ("the data says A because…; risks…") and tell the user it's ready.
- Bugs from feedback: reproduce in the Editor, then follow §2 (blocking → fix on `overnight/p3-p6`; otherwise list it).

## 4. Preparation for after Gate 3 (on `vs/prep`, only when §3 is up to date)
Work that's useful whatever the gates decide. Keep each piece small, tested and documented.
- **Apply Gate 1 in the shared core** as the vertical slice's default care layer: the P2-B consequence (sunlight / work
  pace in place of a penalty, the people's mood showing it, capped at 48 h) as a `CareConfig` the slice turns on. Keep
  P2–P6 unchanged (their opt-in switches stay as they are); prove it with the shared tests.
- **A vertical-slice plan draft** (`docs/design/vertical-slice-plan.md`) per prototype-plan.md Gate 3: one box, eras
  1–3, real save/catch-up architecture, final art direction. Write the gate-dependent parts as options ("if Gate 2 picks
  cards: …; if the book: …"), never as decisions.
- **Engineering debt the prototypes exposed**, as small refactors with tests, in `_Shared` only behind opt-in switches:
  IMGUI → a UI that automation can click (a short spike + recommendation, don't port everything); the look-time sampler
  (screen-centre only) vs a better "what is the player looking at"; save versioning for the real game.
- **The polish lists** in `results/P3.md`–`P6.md`: merge them into one ranked list in `docs/design/polish-backlog.md`
  (impact on the player, cost, which gate outcome it depends on). Don't implement them; the user picks.

## 5. Checking in with the user
- Daily (or when they come back): a short note at the top of `docs/testing/STATUS.md`: data received, results so far,
  bugs fixed or pending, anything waiting for them. Keep it to what they need to act on.
- **Ask first:** any TestFlight run; any change to P3–P6 behaviour that isn't a blocking bug; anything touching P2;
  installing anything; deleting tester data; posting anywhere outside this repo.
- When both sheets are filled: tell the user Gates 2 and 3 are ready to decide, with links to the sheets and your
  recommendations.

## 6. STATUS.md checklist (create it like this, then tick and append as you go)
```
- [ ] read docs (§1), branch vs/prep created from overnight/p3-p6
- [ ] results pipeline: issues read, results tables started in P3–P6.md
- [ ] Gate 2 sheet filled (P3, P4) + recommendation
- [ ] Gate 3 sheet filled (P5, P6) + recommendation
- [ ] blocking bugs from testers: fixed / listed (ongoing)
- [ ] vs/prep: Gate 1 care layer (CareConfig) + tests
- [ ] vs/prep: vertical-slice plan draft
- [ ] vs/prep: polish backlog ranked
- [ ] vs/prep: engineering spikes (UI automation, look sampler, save versioning)
```
If time or data is short, the priority is: results and sheets (§3) first, blocking bugs second, preparation (§4) last.
Never leave a branch that doesn't compile or pass the shared tests.
