# Prompt: overnight build of P3–P6 + a Gate 2/3 test session

Paste everything below the line into a **new** Claude Code session started in `F:\GameDev`, with the Unity Editor
open on `AquariumCiv`. It is written to run unattended for many hours and to survive context compaction.

---

You're building prototypes **P3, P4, P5 and P6** of Aquarium Civ overnight, unattended, and then a **test session**
the user will play after deciding Gate 1. The user is asleep. Don't ask questions: make the sensible call, write
it down, and keep going.

## 0. Resume first
Read `docs/overnight/STATUS.md` if it exists. If it does, you (or an earlier session) already started: continue from
the first unchecked step and don't redo finished work. If it doesn't exist, create it from the checklist in §6.
Update it after **every** finished step (one line: what was done, commit hash, anything odd). It's your memory across
context compaction.

## 1. Read before any code
- `CLAUDE.md` (all rules apply, especially: never let a Unity call hang silently, the loop after every C# change,
  the `tester` sub-agent, the debug panel stays current, free assets only).
- `docs/tooling.md`, all of it, including the dated Log. Many traps are already solved there: Bash path quoting,
  in-play domain reload, `capture game_view` writing under `Assets/`, touch-fixture gotchas.
- `docs/design/game-definition.md`, `docs/design/prototype-plan.md` (§2–§5: P3, P4, P5, P6 and Gates 2 and 3 are
  your spec), and `docs/game-design-practices.md`.
- `docs/design/results/P1.md` and `P2.md`: how a prototype is built, tested and written up here. Copy that shape.
- The code: `Assets/Prototypes/_Shared/` (sim, clock, views, `WipeCleaner`, UI, logging), `P2_ReturnToMess/`, and
  `Assets/Editor/Agent/PrototypeSceneBuilder.cs`.

## 2. Hard guardrails
- **P2 is in a live diary test on the user's phone. Don't break it.**
  - P2's behaviour, scene and save format stay as they are. Shared changes must keep
    `AquariumCiv.Prototypes.Shared.Tests` green.
  - After any shared change: rebuild P2 (`BuildP2`), play it 30 s, and confirm 0 console errors.
  - New sim fields must default safely when an old save has none (see `econRng ??=` in `SimCore.Step`).
- **No TestFlight, no publishing.** Don't run `ios-testflight.yml`; a new build would replace P2 on the user's phone
  mid-diary. Prepare everything so the user can start it with one command later (§5).
- **Git:**
  - Work on a new branch `overnight/p3-p6`, created from `p2-return-to-mess`.
  - Commit after every step. Tag each prototype when it passes its tester run: `proto/P3-v1`, and so on.
  - You may push **only** this branch, as a backup.
  - Don't merge, don't touch `main`, `dev` or PR #5, don't open PRs, and never force-push.
- **Unity rules (CLAUDE.md):**
  - Status check before edits, `--timeout` on every call, background plus 1-minute check-ins for long ones.
  - Never hand-edit scene or prefab YAML while the Editor is reachable.
  - No package or plugin installs, no `unity self-update`.
  - If the Editor is stuck: run `Tools/unity-dialogs/dismiss-dialogs.ps1`. If it's still stuck, write it in STATUS.md
    and do the Editor-free work (docs, pure C# sim plus EditMode tests). Don't loop retries.
- **Assets:** only packs already imported (KayKit, Kenney). Anything new must be CC0, and you log it in
  `docs/assets-log.md`.
- **Prototype isolation:** each prototype gets its own folder, asmdef and scene, and references only `_Shared`.
  Deleting a prototype folder must not break anything.
- **Care layer in P3–P6:** the shared `WipeCleaner` plus mess, with the output penalty and mood link **off**. Care is
  Gate 1's question, not these prototypes'. Don't let it confound their A/Bs.
- **Lab pacing:** every prototype must be testable in about 7 minutes per variant. Use lab speed, step-away buttons
  and pre-generated history (P4 needs about 100 game years) so nothing needs real waiting.

## 3. Per prototype: the loop (P3 → P4 → P5 → P6, in that order)
1. **Plan.** Re-read the prototype's §5 entry. Write a short build plan at the top of `docs/design/results/Pn.md`:
   the hypothesis, the one variable between A and B, what's in and out of scope, and the metrics to log.
   When the spec is vague, pick the simplest reading that still tests the hypothesis, and note it.
2. **Build:**
   - Sim and logic in pure C#, with EditMode tests.
   - Views and UI in the prototype's folder.
   - A `BuildPn()` menu item in `PrototypeSceneBuilder`.
   - Debug panel cheats for every new state (spawn an event, kill or birth a notable, jump N years, trigger a
     nudge, trigger the era threshold, and so on).
3. **Verify** (CLAUDE.md loop):
   - Recompile with 0 errors.
   - Console with 0 errors or warnings.
   - Screenshots of both variants at portrait 1080x1920; look at every one.
   - Tests green, with a non-zero count.
4. **Tester sub-agent:** one run, briefed like `results/P2.md` shows: what changed, what to play, the pass list,
   which tests, and real touch input. Fix every **[real]** finding, then a short re-verify. At most 2 rounds per
   prototype. List the **[polish]** findings for the user in `Pn.md`; don't fix them.
5. **Write-up.** In `Pn.md`, record what was built, the tester results, the polish list and the open questions.
   Set the status line in `prototype-plan.md`. Commit, tag `proto/Pn-v1`, and update STATUS.md.

## 4. The Test Hub: switching variants clearly (build it right after P3 passes, then extend it)
The user must always know **what they are testing and what it's compared with**.
- **Hub scene** `Assets/Prototypes/_Hub/Hub.unity`, first in Build Settings:
  - One card per prototype, in test order: P3, P4, then P5, P6.
  - Each card shows the hypothesis in plain words, plus two buttons: "**A** · <short name>" and "**B** · <short name>",
    each with one line on what that version does.
  - A tick shows which variants were played this session.
  - It also has a "**P2 diary (continue)**" entry that opens P2 exactly as now, so a future build never strands the
    diary. If a P2 diary is in progress (P2 save exists), **auto-open P2 on launch**, and reach the hub via the
    ledger hold or a small "Test hub" button in P2's debug panel.
    That keeps the user's and friends' diary flow unchanged.
- **In every lab prototype**, a slim strip at the top, under the notch:
  - It reads, for example, `P3 · A: Chronicle cards (vs B: plain return)`.
  - It has a "Switch to B" button and a "Hub" button.
  - Colour-code A and B consistently across all prototypes (for example teal A, sand B) and use the same colours on
    the hub buttons.
  - Switching variants starts that variant's lab setup fresh (same seed and history for both, so they compare fairly).
- **After the second variant of a pair**, ask one pair question: "Which did you prefer, A or B, and why?" (two
  buttons plus a text box), then return to the hub.
  - Keep the 5-question survey per variant ("Done for now"), with each prototype's own Q4 from the plan.
  - Log the pair answer as a `pair_pref` event, tagged with proto and variant.
- Scene loading is the hub's job; prototypes still reference only `_Shared`. Put the hub's helpers (strip, pair
  question) in `_Shared/Runtime/UI/`.
- Give the hub a tester run of its own: navigation, the strip, switching and the pair question. IMGUI buttons can't be
  clicked from automation (see tooling.md), so verify them through state and screenshots.

## 5. The test session for the user (the last step; also write it for whatever is finished if time runs out)
Write `docs/design/test-session-gate2-3.md`, a guide the user can follow on the phone with no help:
- **Before you start:**
  - One command to build TestFlight once Gate 1 and P2 are decided. Use the version scheme in `docs/testflight.md`:
    the next minor, `0.3.0`, with its row in the build log.
  - A ready-to-paste "What to Test" text.
  - The note that friends get it only when the build is added to their external group.
- **Per block (P3, P4 → Gate 2; P5, P6 → Gate 3):** a table of steps, one row per variant:
  - what to open in the hub
  - what to do, for how many minutes, and what to look for
  - which survey appears
  - what it's compared with
  - a checkbox
  Order the variants A→B for the user (friends are counterbalanced by the existing schedule). Include the diary parts
  (P3 4-day, P5 3-day) as their own short sections, since they need real days. Say how to switch the device to diary
  for them (ledger hold, or the hub).
- **Gate 2 and Gate 3 decision sheets:**
  - The questions from prototype-plan.md with "fun if" thresholds.
  - Where each metric comes from: survey answers and the GitHub feedback issues via `FeedbackSender`.
  - A blank "decision" line for each, for `docs/decisions.md`.
- Also publish this guide as a private Artifact (load the `artifact-design` skill first). Put the link at the top of
  `docs/overnight/REPORT.md`.

## 6. STATUS.md checklist (create it like this, then tick as you go)
```
- [ ] branch overnight/p3-p6 created from p2-return-to-mess
- [ ] P3 plan → build → verify → tester → write-up → tag proto/P3-v1
- [ ] Test Hub (P2 entry + P3) → tester
- [ ] P4 (seeded ~100 years) ... → tag proto/P4-v1, added to hub
- [ ] P5 ... → tag proto/P5-v1, added to hub
- [ ] P6 ... → tag proto/P6-v1, added to hub
- [ ] P2 regression: rebuild, play 30 s, tests green
- [ ] test-session-gate2-3.md + artifact
- [ ] REPORT.md
```
**If time or budget runs short, the priority is:** P3 + Hub + the test session for P3, then P4 (Gate 2 complete),
then P5, then P6. Never end with a broken build: the last commit must compile, pass the tests and play.

## 7. End of the night: `docs/overnight/REPORT.md`
Keep it short and scannable:
- the artifact link
- per prototype: done / partial / not started, the tag, the tester verdict, and the top 3 polish items
- decisions you made on the user's behalf (with the reason)
- anything stuck and what you tried
- the exact next step for the user (decide Gate 1 → run the TestFlight command → follow the session guide)

Push the branch, then stop.
