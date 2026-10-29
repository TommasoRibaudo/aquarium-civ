# Overnight P3–P6 status

Prompt: `docs/prompts/overnight-P3-P6.md`. Started 2026-10-28.

- [x] branch overnight/p3-p6 created from p2-return-to-mess
- [x] P3 plan → build → verify → tester → write-up → tag proto/P3-v1
- [x] Test Hub (P2 entry + P3) → tester
- [x] P4 (seeded ~100 years) ... → tag proto/P4-v1, added to hub
- [x] P5 ... → tag proto/P5-v1, added to hub
- [x] P6 ... → tag proto/P6-v1, added to hub
- [x] P2 regression: rebuild, play 30 s, tests green
- [x] test-session-gate2-3.md + artifact
- [x] REPORT.md

## Log
- branch created from p2-return-to-mess @ 7aa57b6.
- P3 built (shared story layer, notables, portraits, lab strip/HUD plumbing). Tests 21/21, P2 fingerprint test added. Screens OK at 1080x1920. Next: tester run 1.
- P3 tester run 1: fail (3 real: junk steals taps, duplicate names, tiny people targets). Fixed in ab6d1d0. Run 2 (re-verify) running.
- P3 tester run 2 passed (+1 real side effect fixed after). Tagged proto/P3-v1.
- Test Hub built (177b1c7): hub first in build, auto-open of a running diary (P2 by default), lab strip with Switch/Hub, pair question. Hub tester run running.
- Hub tester: fail (3 real: second diary hid P2, lab wiped diary, scroll left buttons dimmed). Fixed; diary preservation verified in play.
- P4: tester run 1 fail (5 real), fixed + verified in play. Tagged proto/P4-v1. Next: P5 from staging.
- P5 built (71a0df1): nudge sim + tests (31/31, P2 fingerprint intact), omens/land props, credited answers. Tester run 1 running.
- NOTE: another session (likely gamedev-05) has uncommitted sound work in this same tree (Juice/MessView/WipeCleaner/RubSynth/SoundPreview). Not mine; left untouched and unstaged. Briefly committed by accident, undone before push.
- P5 tester run 1 fail (7 real), fixed, verified in play. Tagged proto/P5-v1. Next: P6.
- Guide published: https://claude.ai/artifact/1YB4s8BGquSAKtQayG1CPU. P6 built (acbb152), tester run 1 running.
- P6 tester run 1 fail (6 real), fixed, verified in play. Tagged proto/P6-v1. Next: P2 regression, version 0.3.0, report.
- P2 regression passed (rebuilt + original scene, 0 errors, 31/31). Version 0.3.0 (bf0533d). REPORT.md done. Branch pushed.
