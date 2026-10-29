# Overnight P3–P6 status

Prompt: `docs/prompts/overnight-P3-P6.md`. Started 2026-10-28.

- [x] branch overnight/p3-p6 created from p2-return-to-mess
- [x] P3 plan → build → verify → tester → write-up → tag proto/P3-v1
- [x] Test Hub (P2 entry + P3) → tester
- [x] P4 (seeded ~100 years) ... → tag proto/P4-v1, added to hub
- [ ] P5 ... → tag proto/P5-v1, added to hub
- [ ] P6 ... → tag proto/P6-v1, added to hub
- [ ] P2 regression: rebuild, play 30 s, tests green
- [ ] test-session-gate2-3.md + artifact
- [ ] REPORT.md

## Log
- branch created from p2-return-to-mess @ 7aa57b6.
- P3 built (shared story layer, notables, portraits, lab strip/HUD plumbing). Tests 21/21, P2 fingerprint test added. Screens OK at 1080x1920. Next: tester run 1.
- P3 tester run 1: fail (3 real: junk steals taps, duplicate names, tiny people targets). Fixed in ab6d1d0. Run 2 (re-verify) running.
- P3 tester run 2 passed (+1 real side effect fixed after). Tagged proto/P3-v1.
- Test Hub built (177b1c7): hub first in build, auto-open of a running diary (P2 by default), lab strip with Switch/Hub, pair question. Hub tester run running.
- Hub tester: fail (3 real: second diary hid P2, lab wiped diary, scroll left buttons dimmed). Fixed; diary preservation verified in play.
- P4: tester run 1 fail (5 real), fixed + verified in play. Tagged proto/P4-v1. Next: P5 from staging.
