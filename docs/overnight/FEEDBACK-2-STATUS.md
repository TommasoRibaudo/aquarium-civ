# Feedback batch 2: status (resume file)

Brief: `docs/prompts/feedback-batch-2-run.md`. Integration branch `p8/feedback-2`, worktree `F:\GameDev-wt\feedback2`.

**Mode:** hybrid (workflow.md §7). Mode 2 parallel offline workers for the logic issues (#325, #328, #331, #333) and
the docs proposals (#330, #327 inventory); one Editor worker at a time for the look issues (#332, then #329 + #281 tint,
then #327 shots); then one batched Editor pass and the touch play-through. Why: the logic issues can be judged by a test
or a number and have separate Touches; the look issues need screenshots to iterate.

## Checklist
- [x] `p8/feedback-2` from `origin/dev`; `Tools/run-tests.sh` added
- [x] 1 · #325 merged
- [x] 2 · #328 merged
- [x] 3 · #331 merged (open for a listen)
- [ ] 4 · #333 merged
- [ ] 5 · #332 merged (open for look sign-off)
- [ ] 6+7 · #329 and the #281 tint merged (open for sign-off)
- [ ] 8 · #327 proposal written, shots taken (open, `needs:user`)
- [x] 9 · #330 options written (open, `needs:user`)
- [ ] Batched Editor pass green (EditMode 10 assemblies, PlayMode classes, smoke); failures fixed or filed as baseline
- [ ] `EcoPhonePlaythrough` + the touch play-through done, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] PR → `dev`, CI green, merged
- [ ] Final report to the user (what's in, sign-offs, [polish], calls made, wall clock per phase, open-questions.md)

## Log
- 2026-11-04 20:58 run start.
- 21:01 `p8/feedback-2` from origin/dev (9858591). Merged `origin/docs/batch-modes` (PR #335, docs only, still open) so the
  brief and workflow §7 are on the branch; PR #335 becomes redundant once this lands. Added `Tools/run-tests.sh`
  (from the PR #326 scratch runner, with bounded waits).
- 21:16 9 · #330 options written (keepers-hands §5.7: A keep / B paths+yards / C1 Keeper-tree shoots / C2 forest; rec B then C1), merged 752a325. Stays open needs:user.
- 21:05 Editor restarted (47 reloads) on vs/332-camp-rack in F:\GameDev; lock + stall watcher up. Call: started #332 (Editor) now beside lane A instead of after it: no shared files, Editor was idle.
- 21:20 1 · #325 merged (a79f856): SetVariant(v, announce:false) at session start; TelemetryTests +1. Worker didn't finish the full dotnet run; I run it on the merges.
- 21:25 2 · #328 merged (f8984c1) + my review fix 7384e32 (HUD busy read 4x/s, not per frame). RecallCalm pure rule + RecallCalmTests (4) + TelemetryPlayTests.TheRecallQuestionWaitsForACalmMoment. Cheat "Ask recall (when calm)".
- 21:28 3 · #331 merged (8dea379): SandGusts scheduler + 4 gust cuts from the old hiss loop (deleted), AudioDirector plays them; SandGustsTests (7). Cheat "Sand gust now". Worker's offline View compile showed CS0234 in GlassMask.cs (untouched): check at recompile. Wind loop already random-offset, 40 s: left.
- 21:31 8 · #327 phase 1 merged (fce92a9): docs/design/people-talk.md (inventory, options A/B/C, rec A, shot list). PIL mock script in scratchpad 327-mock.py. Shots still to take (Editor queue).
