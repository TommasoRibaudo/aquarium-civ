# Overnight report: P3–P6, Test Hub, Gate 2/3 session

**Test session guide (artifact):** https://claude.ai/artifact/1YB4s8BGquSAKtQayG1CPU (private to you) · source: [test-session-gate2-3.md](../design/test-session-gate2-3.md)

Branch `overnight/p3-p6` (from `p2-return-to-mess`), pushed as a backup. Nothing merged, no TestFlight run, P2 untouched
(its sim fingerprint test passes; P2 scene and save format unchanged).

## Per prototype

| | Status | Tag | Tester verdict | Top polish (for you to decide) |
|---|---|---|---|---|
| **P3** While You Were Away | done | `proto/P3-v1` | run 1 fail (3 real) → fixed → run 2 pass (+1 real side effect, fixed) | named people stand close (1 in 4 taps opens a neighbour); cards cover half the tank; 9 of 10 people are named at the start |
| **Test Hub** | done | (in P3–P6 tags) | fail (3 real) → fixed, verified in play | pair question has empty space; ticks say played/surveyed only per session; "Start diary" asks a second tap |
| **P4** Book of Ages | done | `proto/P4-v1` | run 1 fail (5 real) → fixed, verified in play | family trees show children under one parent; snapshots include the HUD; repeated chapter titles |
| **P5** Keeper's Nudge | done | `proto/P5-v1` | run 1 fail (7 real) → fixed, verified in play | tray covers the tank; the shrine is small; look-time needs a tap |
| **P6** Eras & Shelf | done | `proto/P6-v1` | run 1 fail (6 real) → fixed, verified in play | the world change at an era is small at phone size (banner and confetti carry it); the annex is a second full glass box; wide views make people specks |

## Decisions made on your behalf
- **Shared changes are opt-in** (`SimConfig.storyEvents`, `recordYears`, nudges only when placed, `WipeCleaner.peopleFirst`,
  `PrototypeConfig.separateLabSave`): P2 runs exactly as tested. A test fingerprints P2's world (seed 1234, 48 h, both
  variants) so any drift fails.
- **Lab and diary keep separate worlds for P3+** (`proto/P3_lab.json` vs `proto/P3.json`): a lab session never wipes a
  running diary (the hub tester found it did).
- **The hub lists every diary with a save** (P2 first) and opens the chosen one on launch; starting a second diary takes
  a second tap. Reaching the hub from P2: the "Test hub" button on the thank-you screen (P2's ledger hold still switches
  lab/diary, unchanged).
- **P4 gets an optional 3-day diary** (spec: "diary on the P3 build") for the "opens history unprompted" metric.
- **P5's lab runs at ×60** so an answer comes 1–2 minutes after a nudge; answers vary 10–24 game months.
- **P6:** "the lid opens" is replaced by an annex rising from below (the box has an open top already).
- **Story ages** are notable-ages everywhere (notables age at half speed), so the book and the chronicle agree.
- **Version 0.3.0** set on this branch for the next TestFlight run (not run).

## P2 regression
Rebuilt P2 with the builder and played it 30 s: diary mode, no lab strip, save id `P2`, the new options off, 0 console
errors or warnings. Then restored P2's committed scene (the rebuild only reshuffled serialisation) and played the
original again: same result. Shared tests 31/31, including P2's sim fingerprint for both variants.

## Stuck / notes
- Another session (yours, `gamedev-05`) worked on a sound pass in the same working tree during the night (`Juice.cs`,
  `MessView.cs`, `WipeCleaner.cs`, `RubSynth.cs`, `SoundPreview.cs`). It was briefly swept into a P5 commit by mistake and
  taken back out before pushing; it stays uncommitted for that session. Editor time was shared by message.
- The `tester` agent type wasn't registered at first; the first P3 and hub runs used a general-purpose agent with
  `.claude/agents/tester.md` as its role.

- Every prototype failed its first tester run on real issues (16+ in total), all fixed. The second tester round was used
  for P3 only; later fixes were checked by the main session in play mode, so P4–P6 fixes have no second independent run.
- Build order: Hub, P2, P6, P5, P4, P3, P1, Sandbox, SampleScene (the Hub is always first).

## Your next step
1. Decide Gate 1 (P2).
2. Run `gh workflow run ios-testflight.yml --ref overnight/p3-p6 -f notes="0.3.0 Test Hub P3-P6"`.
3. Follow the test-session guide (link above).
