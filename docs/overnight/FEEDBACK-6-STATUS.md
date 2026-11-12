# Feedback batch 6 (build 25 notes #580–#583) — status

Triaged 2026-11-11 → #587 (touch swap, from #581), #588 (village fire pit + well, #580), #589 (supper plays out, #583), #504 (deer body + graze, #582).
Mode: hybrid (workflow.md §7): three offline Sonnet workers in parallel, one batched Editor pass, tester, PR p9/feedback-6 → dev.
Worker rules: session scratchpad worker-rules.md (no Editor, offline csc, cheats in one block, report < 400 words).

## Checklist
- [x] p9/feedback-6 from origin/dev (ceeb267); worktrees F:\GameDev-wt\{587,588,504}
- [ ] #587 merged (vs/587-touch-swap)
- [ ] #588 + #589 merged (vs/588-589-fire-supper)
- [ ] #504 merged (vs/504-deer-look)
- [ ] Editor pass: recompile, EditMode per assembly, PlayMode named classes + SmokePlay, screenshots looked at, AnimLab graze
- [ ] tester run; [real] findings fixed
- [ ] PR → dev merged; issues commented/closed; notes #580–#583 closed with their issue
- [ ] finish checklist: Run In Background ON, lock released, tooling.md note

## Log
- 2026-11-11 workers launched (Sonnet ×3)
- #587 round 1 → sent back (tool-in-hand couldn't pan; moments rail kept old mapping; no one-finger lean test). Round 2 b86f417 accepted, merged 942f55f. EditMode BoxCamera* 31/31.
- #504 round 1 merged 7796ddb; Destroy edit-mode fix 4d5b716; QuadGrazeTests 5/5. Shots: neck a giant ball, antlers off the muzzle, stick legs → round 2 in the Editor (worker on p9/feedback-6, under my lock).
- #588/#589 merged 9822610 (SimTests 1827/1827). PR #591 opened.
- Unity pass: EditMode 10 assemblies 2208 tests 0 fail; PlayMode cammode 9, glass 3, care 4 (after fix), arrival 10 (after fix), deer 3, smoke 2.
- Tester: 1 [real] (village supper identical: era festival year overrode it) → fixed (no supper choice in a festival year; cheats end the festival), FireAndSupperTests 9/9, smoke 2/2. [polish] listed in PR.
