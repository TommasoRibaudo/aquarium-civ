# Gates 2 and 3: working status

Prompt: [docs/prompts/while-testing-gate2-3.md](../prompts/while-testing-gate2-3.md). This file is the agent's memory
across context compaction: resume from the first unchecked step.

## For you (latest first)
**2026-10-29**
- TestFlight #7 (0.3.0) first failed at upload: the `testflight` environment only allowed `main`. You added
  `overnight/p3-p6` to its branches and re-ran it (run 40895173646).
- No P3–P6 test data yet. Results come from `python Tools/results/collect.py` (writes `docs/design/results/data/Pn.md`
  from the feedback issues); I re-run it as data arrives.
- Gate 1 care layer ready on `vs/prep` for the vertical slice (`CareConfig.Gate1`, `CareMoodView`); P2–P6 untouched.
- Nothing waiting for you.

## Checklist
- [x] read docs (§1), branch vs/prep created from overnight/p3-p6
- [x] results pipeline: issues read, results tables started in P3–P6.md (1be45cd)
- [ ] Gate 2 sheet filled (P3, P4) + recommendation
- [ ] Gate 3 sheet filled (P5, P6) + recommendation
- [ ] blocking bugs from testers: fixed / listed (ongoing)
- [x] vs/prep: Gate 1 care layer (CareConfig) + tests (86fc90f)
- [ ] vs/prep: vertical-slice plan draft
- [x] vs/prep: polish backlog ranked (docs/design/polish-backlog.md)
- [ ] vs/prep: engineering spikes (UI automation, look sampler, save versioning)

## Log
- 2026-10-29: started. TestFlight #7 blocked by the environment branch rule (fixed by the user). Issues so far: P1 #3–4,
  P2 #6–8 (0.2.0), none from 0.3.0. Results pipeline committed on overnight/p3-p6 (1be45cd, docs/Tools only).
- 2026-10-29: vs/prep created and pushed. From here, results docs and STATUS.md are committed on **vs/prep** (keeps the
  build branch quiet; tell gamedev-16 before any commit to overnight/p3-p6). dev already has overnight/p3-p6 (PR #10):
  a later vs/prep PR targets dev (not opened: the prompt says no PRs).
- 2026-10-29: CareConfig + CareMoodView + CareTests (86fc90f). Gate1 equals P2 B's world fingerprint; shared tests 36/36.
  Console: only the existing P2 warning CS0414 (`messBeforeClean`, left alone: P2 is protected).

## Bugs from testers
| Date | Issue | Prototype | What | Blocking? | Status |
|---|---|---|---|---|---|
