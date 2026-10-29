# Gates 2 and 3: working status

Prompt: [docs/prompts/while-testing-gate2-3.md](../prompts/while-testing-gate2-3.md). This file is the agent's memory
across context compaction: resume from the first unchecked step.

## For you (latest first)
**2026-10-29 (evening): Gates 2 and 3 decided** (decisions.md): chronicle cards; time-lapse + small polished book;
1 h = 1 year; shape the land; new box on the shelf. P5 survey bug (#17) left unfixed by your call. Next: vertical-slice plan.

**2026-10-29 (evening): test results in**
- 11 issues from your session (#11–#21), all you; lab only, no diaries. Results in `results/P3–P6.md`, both decision
  sheets filled with recommendations at the bottom: [test-session-gate2-3.md](../design/test-session-gate2-3.md).
- Gate 2: chronicle cards (A) clearly; time-lapse (A) for history, weakly; time compression untested.
- Gate 3: shape the land (B) on one note, no survey; new box on the shelf (B) clearly.
- **Bug, waiting for you (#17):** P5's survey can't be sent on iPhone (6th question pushes Send off-screen, the IMGUI
  scroll view doesn't drag by touch). The fix is in the shared `SurveyPanel` (P2 uses it), so it needs your go; ships
  as 0.3.1 only if you want P5 replayed.
- Listed, not blocking: notables' age contradicts the calendar (born year 1, age 6 in year 12).

**2026-10-29**
- TestFlight #7 (0.3.0) first failed at upload: the `testflight` environment only allowed `main`. You added
  `overnight/p3-p6` to its branches and re-ran it: **green** (run 40895173646, 15:18 UTC). Waiting for you: add it to
  the Friends group, paste the "What to Test" text, fill the build number in the testflight.md build log row.
- No P3–P6 test data yet. Results come from `python Tools/results/collect.py` (writes `docs/design/results/data/Pn.md`
  from the feedback issues); I re-run it as data arrives.
- Ready on `vs/prep` for after Gate 3, nothing decided: vertical-slice plan draft (options per gate outcome),
  ranked polish backlog, three engineering spikes (save versioning, UI automation, look sampler), each with a
  recommendation in docs/design/spikes/. Gate 1 care layer for the vertical slice (`CareConfig.Gate1`, `CareMoodView`); P2–P6 untouched.

## Checklist
- [x] read docs (§1), branch vs/prep created from overnight/p3-p6
- [x] results pipeline: issues read, results tables started in P3–P6.md (1be45cd)
- [x] Gate 2 sheet filled (P3, P4) + recommendation (n=1)
- [x] Gate 3 sheet filled (P5, P6) + recommendation (n=1, P5 has no survey)
- [ ] blocking bugs from testers: fixed / listed (ongoing; #17 P5 survey scroll listed, waiting for the user)
- [x] vs/prep: Gate 1 care layer (CareConfig) + tests (86fc90f)
- [x] vs/prep: vertical-slice plan draft (docs/design/vertical-slice-plan.md, options only)
- [x] vs/prep: polish backlog ranked (docs/design/polish-backlog.md)
- [x] vs/prep: engineering spikes (UI automation ba55981, look sampler, save versioning cd40089; docs/design/spikes/)

## Log
- 2026-10-29: started. TestFlight #7 blocked by the environment branch rule (fixed by the user). Issues so far: P1 #3–4,
  P2 #6–8 (0.2.0), none from 0.3.0. Results pipeline committed on overnight/p3-p6 (1be45cd, docs/Tools only).
- 2026-10-29: vs/prep created and pushed. From here, results docs and STATUS.md are committed on **vs/prep** (keeps the
  build branch quiet; tell gamedev-16 before any commit to overnight/p3-p6). dev already has overnight/p3-p6 (PR #10):
  a later vs/prep PR targets dev (not opened: the prompt says no PRs).
- 2026-10-29: CareConfig + CareMoodView + CareTests (86fc90f). Gate1 equals P2 B's world fingerprint; shared tests 36/36.
  Console: only the existing P2 warning CS0414 (`messBeforeClean`, left alone: P2 is protected).

- 2026-10-29: polish backlog (327ed81), vertical-slice plan draft (b07ff64), save-versioning spike (cd40089,
  docs/design/spikes/save-versioning.md; shared tests 42/42). TestFlight #7 green. Next: re-run the collector as
  issues arrive; spikes left: UI automation, look sampler.
- 2026-10-29: UI automation spike: UI Toolkit card + `UiProbe`, first PlayMode test assembly `_Shared/PlayTests`,
  fixture touch taps buttons by name, 1/1 on first run (docs/design/spikes/ui-automation.md). Recommendation: UI
  Toolkit for the slice. Shared EditMode 42/42. Next: look sampler spike; collector re-runs (no 0.3.0 issues yet).
- 2026-10-29: look-sampler spike (`LookSampler`, `EventLog.weightedLook` off by default), 47/47, checked live in P6
  lab with local saves backed up and restored, P2 scene reopened. All §4 prep done; remaining work is §3 (data).

## Bugs from testers
| Date | Issue | Prototype | What | Blocking? | Status |
|---|---|---|---|---|---|
