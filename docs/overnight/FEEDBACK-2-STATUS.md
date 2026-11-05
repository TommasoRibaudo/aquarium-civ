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
- [x] 4 · #333 merged
- [x] 5 · #332 merged (open for look sign-off)
- [x] 6+7 · #329 and the #281 tint merged (open for sign-off)
- [x] 8 · #327 proposal written, shots taken (open, `needs:user`)
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
- 21:03 9 · #330 options written (keepers-hands §5.7: A keep / B paths+yards / C1 Keeper-tree shoots / C2 forest; rec B then C1), merged 752a325. Stays open needs:user.
- 21:02 Editor restarted (47 reloads) on vs/332-camp-rack in F:\GameDev; lock + stall watcher up. Call: started #332 (Editor) now beside lane A instead of after it: no shared files, Editor was idle.
- 21:05 1 · #325 merged (a79f856): SetVariant(v, announce:false) at session start; TelemetryTests +1. Worker didn't finish the full dotnet run; I run it on the merges.
- 21:06 2 · #328 merged (f8984c1) + my review fix 7384e32 (HUD busy read 4x/s, not per frame). RecallCalm pure rule + RecallCalmTests (4) + TelemetryPlayTests.TheRecallQuestionWaitsForACalmMoment. Cheat "Ask recall (when calm)".
- 21:06 3 · #331 merged (8dea379): SandGusts scheduler + 4 gust cuts from the old hiss loop (deleted), AudioDirector plays them; SandGustsTests (7). Cheat "Sand gust now". Worker's offline View compile showed CS0234 in GlassMask.cs (untouched): check at recompile. Wind loop already random-offset, 40 s: left.
- 21:09 8 · #327 phase 1 merged (fce92a9): docs/design/people-talk.md (inventory, options A/B/C, rec A, shot list). PIL mock script in scratchpad 327-mock.py. Shots still to take (Editor queue).
- 21:12 4 · #333 merged (5d13ef4) + 9465add (dup comment). RabbitHerd startle (freeze 0.3 s, bolt to a new home far from tap; tap spot + old spot avoided 180 s field time; never removed), wider roam + spread homes; hash-keyed randomness (sim fingerprint test). Hook: GlassVoice.HandleTaps → RabbitView.TapAtScreen, additive (knock/ripple still happen). Hunt target and sign rabbit skipped. Cheats "Spawn rabbits here", "Startle all". New PlayMode RabbitViewPlayTests.Touch_StartlesARabbit_... Lane A done 21:13 (15 min after start).
- dotnet SimTests on the #325 merge: 1327/1327.
- 21:20 dotnet SimTests on 7f56286 (all lane A merged): 1345/1345.
- 21:34 #332 first pass (e305b66): logs gone, A-frame rack; I reviewed the shots: pelts small/edge-on, read as a clothesline, tan on tan, default shot staged on a barren Y0 box. Sent back for one iteration (≤2 big tilted hides, rest in a visible pile, darker hide, real camp state, JPG shots). Stall watcher exit 3 was the idle review gap (no dialog, ready); restarted.
- 21:50 5 · #332 round 2 accepted (2986a75), merged d6f886f: ≤2 laced pelts on stretching frames (lean 12°: 40° goes edge-on at the 52° camera pitch), the rest in a folded pile (clamp(n-2,0,3)), darker hides; LifePlanner.HideFrameDistance 0.34→0.5 (outside fence, one constant). Shots JPG in docs/design/results/p8/feedback2/332_*. Open for look sign-off. Editor recompile on d6f886f (all lane A + #332): 0 errors, 4 old warnings. 6+7 Editor worker started on vs/329-281-skip-tint.
- 22:29 6+7 merged (16487e0): #329 ButtonKind.Ghost pills (hairline, faint fill 0.34, 44 pt), Quiet untouched, gallery row (gallery shot not taken); #281 tint lift 0.7→0.25, softer colour, alpha 0.9→0.55, FadeSeconds 180→100 (noon light ratio 1.034→1.012, changed px 28758→11432). FirstRunPlayTests 3/3, NewGrassPlayTests 1/1, EcoSeen 7/7. Both open for sign-off. Worker hit 3 watcher stalls: an [Explicit] shot class matched 0 tests and my runner waited its 40-min limit (fixed 57d436c). Editor time ~70 min (box 60).
- 22:41 8 · #327 shots merged (39bdadb): today/A/B/C pairs + A4 extra (327_*.jpg), measured doll 28 px at zoom 1 (52 at 0.45, 22 at 1.25) → A's 45 px switch ≈ zoom 0.62. Mocks via PIL. Stays open needs:user. Editor items done; restart, then batched pass.
- 22:35-23:01 EditMode batch (Editor restarted first), on p8/feedback-2-pass: Sim 736/738 (2 skipped), Audio 48/48 (SandGusts 7), View 149/151 (1 fail + BoxLeak inconclusive by design in an interactive Editor), ShowMe 106/106, Cards 49/49, Moments 44/44, Shaping 66/66, TimeLapseScreen 37/37, Game.UI 39/39, Life 272/272. The fail: ViewKitTests.EveryPieceKind_DrawsInEveryEra "LogSeat in Camp" (ticket #332: retired kind draws nothing by design) → test skips LogSeat (4cb4787); ViewKitTests 11/11.
- 22:48 Feedback2Playthrough (explicit, category Feedback2Play) written offline by a worker, merged df5f0fe: the batch's beats by touch (first run, mister, cloth, tweezers weed/tuft/vine/sapling, rabbit tap + no return, camp rack + no LogSeat, recall never over a sheet/tool).
- 23:05-23:20 PlayMode batch on p8/feedback-2-pass: Rabbit 3/3, Audio 4/4, CampStash 1/1, PeopleWiring 15/15, Care 4/4, WorldTap 7/7, PondLife 3/3, Chatter 7/7, FirstRunPolish 10/10, NewGrass 1/1, Tester2Fix 6/6, SmokePlay 2/2; Telemetry 2/3 (AScriptedSession now green: #325 fixed); Hunt 1/2.
  - HuntViewPlayTests.Hunter_StalksARealRabbit "rabbit stays put while stalked": ticket #333 (base files swapped in: 2/2). Cause: rabbits spread over the meadow, the hunted one fled from a passer-by. Fix 6a49f57: the hunted rabbit minds nobody until the strike. Rerun 2/2.
  - TelemetryPlayTests.TheRecallQuestionWaitsForACalmMoment (#328's new test): a test bug: the fixture's touch never reaches the HUD tool buttons (picked 'hud-clean', no click), and on a spotless box the cloth puts itself down after 2.2 s anyway. Fix 629c857: cloth as state + frame-by-frame watch; the by-touch Kit + tool + answer check moved into Feedback2Playthrough beat 5b.
