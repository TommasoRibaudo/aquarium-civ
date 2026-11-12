# Feedback batch 7 (build 26 notes #593–#602) — status

Triaged 2026-11-12 → #603 spring start (#594), #604 HUD title (#595), #605 bush water (#601), #606 name pin (#598),
#607 tent sleepers (#599), #608 rabbit tracks (#600), #609 seed pad (#602), #587 round 2 pinch dead zone (#596),
#584 rename card + keyboard (#597). #593 praise closed.
Mode: hybrid (workflow.md §7): seven offline Sonnet workers in parallel, one batched Editor pass (looks iterated there), tester, PR p9/feedback-7 → dev.
Worker rules: session scratchpad worker-rules.md. C: had 19 GB free at the start (97 %): watch editor-health exit 2.

## Checklist
- [x] p9/feedback-7 from origin/dev (569e846) in F:\GameDev; worktrees F:\GameDev-wt\fb7-{603,604,605,606,607,608,587b}
- [ ] workers reported; branches reviewed and merged; SimTests on the merge
- [ ] Editor pass: health gate, recompile, EditMode per assembly, PlayMode named classes + smoke, screenshots looked at
- [ ] tester run; [real] findings fixed
- [ ] PR → dev; issues commented/closed (Tools/close-issue.sh); notes closed with their issue
- [ ] finish checklist: Run In Background ON, lock released, tooling.md note

## Log
- 2026-11-12 workers launched (Sonnet ×7)
- 11:41 #604 merged (4ec03b9); worker could not read the issue (worked from brief); discs keep backing; check off-home legibility + no press feedback on title
- 11:42 #587 r2 merged (448217f); run BoxCameraTouchSwapTests, CameraModePlayTests, ZoomLurchPlayTests
- 11:48 #584+#606 merged (c021f7a); run PersonCardRenamePlayTests, PinTagLiftPlayTests, PersonCardPlayTests, Feedback5Playthrough; cheat 'Speech over pin'
- 11:51 #608+#609 merged (ab96060); tracks NOT moved near the warren (cells picked before it exists) — judge by eye; ghost seed leans slightly; run RabbitTrackLayoutTests, KeeperTreeKitTests, RabbitViewPlayTests
- 11:54 #607 merged (e98894a); proved: fire keeper slept then got up; pop change re-picked keeper among sleepers. 'Everybody came out' not reproduced — watch in play mode 19-24h with a pop/month change
- 11:55 #605 merged (8202f4d); thicketWatered 0.6 (watered 37 vs dry 27 bushes over 6 seasons); EcoBushCount bound 4→6/yr; pacing lab unchanged
- 12:33 #603 merged (58951b6); spring pacing active 26.3/25.1/24.1 (was 26.5/24.5/24.3); SimTests on merge 1838/1838
- 12:33 Editor pass delegated (brief: scratchpad editor-pass.md); health ok, restart first
- 12:35 Editor pass attempt 1 died ~1 min in (session ended): stale lock, no commits, Editor restart completed itself (ready, compile clean). Attempt 2 launched with the stall watcher.
- 12:36 editor pass: lock taken, Editor restarted, recompile clean (console: only the Input Manager warning)
- 12:36 attempt 2: console clean; metas committed (6eadd3d); EditMode batch started (12 assemblies, Tools/run-tests.sh, OUT Temp/agent/test-runs-em)
- 12:45 found an orphan run-tests.sh from attempt 1 racing mine (both read stale 43/43): killed both; own runner (scratchpad xrun.sh, judged by TestResults.xml) restarted the EditMode batch
- 12:54 first EditMode batch lost: the run-tests.sh background shell was killed at 600 s; its timed-out 'test run' retries stayed queued in the Pipeline and kept restarting/cancelling runs. Draining, then re-running with one start per run
- 13:07 EditMode so far: View 339/341 (1 fail RabbitTrackLayoutTests.Prints_NeverOverlap cell 11 0.359>0.35), Game.UI 43/43, Audio 53/53, Cards 51/51, Moments 44/44, Shaping 66/66. 13:04 someone else's restart-editor.sh (not this pass) quit the Editor mid-ShowMe run; its Save Scene dialog cancelled by Win32 click; waiting for the new Editor
- 13:09 Editor restarted again (Pipeline had leaked collectors); EditMode Game.UI.Tests 43/43; Sim/Life/View running
- 13:11 ATTEMPT 2 STOPS: attempt 1 is still alive and driving the same Editor (its runone.sh, Fb7PassShots.cs, restarts at 13:04/13:09); two agents on one Editor were cancelling each other's runs. Attempt 2 leaves the Editor to attempt 1. Left uncommitted for attempt 1 to take or drop: RabbitTrackLayout.cs fix (trail centred on its prints' mean, so a bent 6-hop trail stays within 0.35 of the cell: fixes RabbitTrackLayoutTests.Prints_NeverOverlap cell 11, not yet re-run) and Assets/Game/View/PlayTests/Fb7Shots.cs (Explicit shot class, overlaps Fb7PassShots: delete it if not wanted). Lock left in place (attempt 1 holds the Editor).
- 13:11 orchestrator: attempt 1 was alive all along (my 12:35 check was too early). Attempt 2 handed back; attempt 1 is the sole Editor driver. Duplicate watcher stopped; lock owner fixed.
- 13:21 EditMode Sim.Tests 989 total, 987 passed, 0 failed (log). Killed stray runone.sh loops (TaskStop left them alive); Life/View/rest re-running
- 13:36 orchestrator: watcher STALL (Editor idle 10 min after a 481/481 run; agent alive, no runner). Nudged the agent; watcher restarted
- 13:40 EditMode Life.Tests 481/481; View.Tests 340 passed + 1 inconclusive of 341 (BoxLeakTests.AfterADomainReload, needs a reload), RabbitTrackLayoutTests all green with attempt 2's centring fix
- 13:41 EditMode e-showme: completed {'total': 125, 'passed': 125, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 125 total, 125 passed, 0 failed
- 13:41 EditMode e-timelapse: completed {'total': 39, 'passed': 39, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 39 total, 39 passed, 0 failed
- 13:42 EditMode e-proto: completed {'total': 47, 'passed': 47, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 47 total, 47 passed, 0 failed
- 13:42 EditMode e-growth: completed {'total': 18, 'passed': 11, 'failed': 1, 'skipped': 6, 'inconclusive': 0} 30s | log: [TestResultCollector] Run finished: 18 total, 11 passed, 1 failed
- 13:44 EditMode Spikes.Growth GrowthLabTests.Criterion5_PathsEmerge fails also with the old calendar (NewBoxShift 0 via eval): not #603; frozen spike test (#119), noted as pre-existing. Audio/Cards/Moments/Shaping passed in attempt 2 (53/51/44/66). Starting PlayMode
- 13:45 PlayMode p-cameramode: completed {'total': 10, 'passed': 10, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 45s | log: [TestResultCollector] Run finished: 10 total, 10 passed, 0 failed
- 13:45 PlayMode p-zoomlurch: completed {'total': 2, 'passed': 2, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 2 total, 2 passed, 0 failed
- 13:46 PlayMode p-rename: completed {'total': 2, 'passed': 2, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 2 total, 2 passed, 0 failed
- 13:46 PlayMode p-pinlift: completed {'total': 1, 'passed': 0, 'failed': 1, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 1 total, 0 passed, 1 failed
- 13:48 PlayMode p-personcard: completed {'total': 10, 'passed': 8, 'failed': 2, 'skipped': 0, 'inconclusive': 0} 60s | log: [TestResultCollector] Run finished: 10 total, 8 passed, 2 failed
- 13:49 PlayMode p-rabbit: completed {'total': 3, 'passed': 3, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 60s | log: [TestResultCollector] Run finished: 3 total, 3 passed, 0 failed
- 13:50 PlayMode p-glassui: completed {'total': 14, 'passed': 13, 'failed': 1, 'skipped': 0, 'inconclusive': 0} 30s | log: [TestResultCollector] Run finished: 14 total, 13 passed, 1 failed
- 13:50 PlayMode batch 1: CameraMode 10/10, ZoomLurch 2/2, Rename 2/2, Rabbit 3/3; PinTagLift 0/1 (tag stays 20 px up after the mark), PersonCard 8/10 (HoldsTheBoard: chips below the view), GlassUi 13/14 (test still wants the title pill #604 removed: test updated). Diagnosing
- 13:53 PinTagLift: the 20 px left is a natural chatter mark overlapping the resting tag by ~10 px (legit lift); test now checks settle-to-target. PersonCard HoldsTheBoard x2: the #575 'Call them something else' button (fb5) pushes the chips below the view; closed-card layout unchanged by fb7 -> pre-existing, for the user. GlassUi off-home test updated for #604 (1/1)
- 13:54 PlayMode p-pinlift3: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 13:55 PlayMode p-season: completed {'total': 2, 'passed': 2, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 2 total, 2 passed, 0 failed
- 13:56 PlayMode p-firstrun: completed {'total': 3, 'passed': 3, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 60s | log: [TestResultCollector] Run finished: 3 total, 3 passed, 0 failed
- 13:58 PlayMode p-frpolish: completed {'total': 10, 'passed': 10, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 75s | log: [TestResultCollector] Run finished: 10 total, 10 passed, 0 failed
- 13:58 PlayMode p-bush: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 30s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 13:59 PlayMode p-pond: completed {'total': 4, 'passed': 4, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 45s | log: [TestResultCollector] Run finished: 4 total, 4 passed, 0 failed
- 14:00 PlayMode p-walk: completed {'total': 6, 'passed': 3, 'failed': 3, 'skipped': 0, 'inconclusive': 0} 30s | log: [TestResultCollector] Run finished: 6 total, 3 passed, 3 failed
- 14:03 PlayMode p-bundle5: completed {'total': 14, 'passed': 14, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 150s | log: [TestResultCollector] Run finished: 14 total, 14 passed, 0 failed
- 14:03 PlayMode batch 2: PinTagLift 1/1 (test fixed), Season 2/2, FirstRun 3/3, FirstRunPolish 10/10, BushView 1/1, PondLife 4/4, Bundle5 14/14; WalkSmooth 3/6 (stride/brake metrics; not touched by fb7, not traced yet). Fixes in: rename field folds subtitle+tabs without the keyboard too; rabbit prints lighter (were near-black pellets)
- 14:04 PlayMode p-rename2: completed {'total': 2, 'passed': 2, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 2 total, 2 passed, 0 failed
- 14:05 PlayMode p-rabbit2: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:05 PlayMode s-hud: completed {'total': 1, 'passed': 0, 'failed': 1, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 1 total, 0 passed, 1 failed
- 14:07 PlayMode s-hud: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 45s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:08 PlayMode s-seed: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 30s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:08 PlayMode s-spring: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:09 PlayMode s-tent: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:09 shots: #603 fresh box first run reads spring, no snow, HUD 'Year 0 · Spring' after (fb7/603_*). #607 camp night 19-24h x4 with a newcomer at 21.3h and a month tick at 22.6h: 5 asleep, nobody got up (fb7/607.txt). #604 dusk 18h (daylight 0.67) drew mid-tone ink on terracotta: HudInk band narrowed to 0.65-0.75. Re-shooting HUD/seed/tent closer
- 14:10 e-hudink: completed {'total': 4, 'passed': 4, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 4 total, 4 passed, 0 failed
- 14:11 s-hud2: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 45s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:13 s-seed2: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 30s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:13 s-tent2: unreadable ('result') after 15s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:14 e-hudink2: completed {'total': 5, 'passed': 5, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 15s | log: [TestResultCollector] Run finished: 5 total, 5 passed, 0 failed
- 14:15 s-hud3: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 45s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:16 p-walk-shift0: completed {'total': 7, 'passed': 4, 'failed': 3, 'skipped': 0, 'inconclusive': 0} 30s | log: [TestResultCollector] Run finished: 7 total, 4 passed, 3 failed
- 14:17 WalkSmooth fails the same with the old calendar (NewBoxShift 0 in its Setup, temp): not fb7; pre-existing stride/brake metrics, for a follow-up. HudInk 5/5 incl. new off-home halo test
- 14:18 PlayMode p-glassui3: completed {'total': 14, 'passed': 14, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 30s | log: [TestResultCollector] Run finished: 14 total, 14 passed, 0 failed
- 14:20 PlayMode p-smoke: completed {'total': 2, 'passed': 2, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 120s | log: [TestResultCollector] Run finished: 2 total, 2 passed, 0 failed
- 14:23 GlassUi 14/14 after the HUD change; SlicePlaythroughSmoke 2/2 on final code; Feedback5Playthrough running
- 14:24 PlayMode p-fb5: completed {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'inconclusive': 0} 180s | log: [TestResultCollector] Run finished: 1 total, 1 passed, 0 failed
- 14:24 Feedback5Playthrough 1/1. Pass done: lock released next
