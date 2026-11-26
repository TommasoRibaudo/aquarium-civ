# Polish night 1 (2026-11-25): status

Prompt: `docs/prompts/polish-night-1.md`. Integration branch `p9/polish-night-1` in `F:\GameDev-wt\pt1b`.
Orchestrator scratchpad (rules file, issues, media):
`C:/Users/User/AppData/Local/Temp/claude/ab34a40a397e3f22/19ddfdeb-f758-4e04-a659-959a4ac09afd/scratchpad`.

**Mode:** hybrid (workflow.md §7). Lane A Sonnet workers in parallel worktrees (offline csc + SimTests), lane B
Sonnet workers one at a time on the pt1b Editor, then one batched pass. Reason: mixed batch; logic items have a test,
look items need screenshots per ticket.

## Checklist
- [x] `p9/polish-night-1` from `origin/dev` in `F:\GameDev-wt\pt1b`; this prompt committed (b53e5d3a)
- [ ] Bugs: #766 · #764 · #765 · #762 · #763 (cause in the issue) merged
- [ ] Story polish: #779 · #784 · #782 · #780 merged; #781 words ready
- [ ] Words: #773 · #564 ready (and #564 wired)
- [ ] Motion: #767 · #770 · #771 · #772 merged, clips recorded
- [ ] Town: #768 · #769 merged (3 seeds before/after)
- [ ] Looks: #775 · #559 merged, shots recorded
- [ ] Specs and mock-ups: #783 proposal · #774 mock-ups · #776 spec (own branch)
- [ ] Research #777 written (`docs/research/23-ageing-and-death.md`)
- [ ] #118 fixed, 10 runs green
- [ ] Every issue's *Done when* ticked with evidence, or listed as short after 4 rounds
- [ ] Full EditMode suite (10 assemblies) zero failures and zero inconclusive; SimTests; PlayMode classes; smoke
- [ ] The touch play-through at both sizes, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Sign-off page published (private), every card's media looked at; `polish-night-1-signoff.md` committed
- [ ] Closable issues closed (and their notes); the rest left open with "waiting for the user's sign-off"
- [ ] New feedback notes triaged (none started)
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released; Editor ready
- [ ] PR `p9/polish-night-1` → `dev` open, its CI watched to the end (not merged)
- [ ] Final report to the user

## Log
- 22:29 branch + prompt commit b53e5d3a. Editor closed at start (no instance). TestFlight #31 (0.4.1) was already
  running when the user asked to "start the build": not started again.

## Round logs
- 22:45 lane A workers started (Sonnet): 768 settle (#768/#769/#762, abd8e6ed8f97905ba), 765 fishers (afa60e49289251795),
  779 story (#779/#764/#784, afccd5af7ac2cff1a), 782 bubble (a4e98018f01a72aa8), 781 words (#781/#773/#564,
  a8bce2bd8ad5df267), 770 dance+ball (#770/#772, a45acfb35312942c0), 777 research (a9a87a566977e431f),
  783+776 specs (ac6a35b5171c6c5a0), 774 mock-ups (a5c4da79082618a39). Worktrees F:/GameDev-wt/<n>, branches vs/<n>-*.
- 22:40 Editor up on pt1b (ready, 0 errors); lock taken; watcher scratchpad/watch.sh. Lane B #766 worker (a6d9caeac5d29247d) on vs/766-one-sheet in pt1b.
- 22:50 Status file moved to the merge worktree F:/GameDev-wt/pn1 (p9/polish-night-1 checked out there; pt1b holds worker branches for the Editor).

### #782
- round 1 (a27d5839): HungerLineFor (NewStartView) also null after StoryEvent.FirstFruit; HungerLineTests (2, EditMode) written, offline csc 0. Accepted pending Unity run + screenshot (batched pass). Merged into p9.

### #777
- round 1 (c4e52cda): doc written (A last days, E Keeper notices, C left behind ★). Games from memory, no web → round 2: verify via WebSearch, sources, file:line cites.
- round 2 (3681ac3a): 8 games web-verified with sources; cites fixed (death is yearly, SimCore.cs:292). Accepted, merged.

### #774
- round 1 (95ceae4d): 4 mock-ups + index + README + PNGs. My look: tree A gen chips cover faces, folded frame clips a chip and shows empty rows, 38-year-old "elder" → round 2.

### #783 / #776
- round 1: #783 spec 2822170b (docs/design/783-story-camera.md; ★ A lean-in via BoxCamera.LeanIn + GlideBack, player wins) merged as a proposal. #776 spec 224d9254 on vs/776-statue-spec (B81 + people-and-things row), NOT merged (spec-gated). Names checked in code (LeanIn, GlideBack, CameraFree, pin hours). Accepted.
- round 2 (013f2d31): chips in a gutter, folded view tidy, ages consistent (70-year-old elder). All 4 PNGs looked at by me. Accepted, merged (docs only).

### #781 / #773 / #564
- round 1 (cf67b6dd): ★ options wired; 3 options each in docs/design/polish-night-1-words.md. #564 GlassVoice.SayFirstMist from EcoMistView.Pulse when greened>0 (EcoState.firstMistWords), cheat "First-mist line". Note for the sign-off: #773's "kept going" framing is wider than the sim (SupperPick plays tonight + tomorrow only). SimTests 2167/2167 (worker). Accepted, merged; Unity: StoryEventBatch2Tests + first-mist caption in the pass.

### #765
- round 1 (945fe0b3): cause PondShore.NearestShore fell back to slot 0's spot; new SpreadAlongShore (0.14 apart); ice holes by slot. FishersApartTests 6 (5 fail on base). SimTests 2171/0 (worker). Accepted, merged; screenshot in the pass ("Three fishers now").

### #770 / #772
- round 1 (8b720a87, a83a53f4): OpenGround.RingFit/RingCentre keep the ring + margin off footprints (DanceClearTests 18, 15 fail with fix off); Arc/KidPlay glass bounce off pane inner faces with tap_glass pitched down (KidPlayGlassTests 435, AnimBench). Accepted, merged; clips in the pass. Note: AnimBench.csproj doesn't build on dev (CampScenes6, DeerView...) — pre-existing, to file.
- 23:20 SimTests on the merge: first run cut by my own 600 s timeout (1576 passed, then "aborted"); rerun in background.

### #779 / #764 / #784
- round 1 (cb2c906a, f35ba121, d804403a): BodySpots (0.09), 1.2 couple gap + kids' free spots, 2.1 watchers along the shore; vowAt gates the I do + 5.2 caption; DebugStandProp for the two cheats. SimTests 2171/0 (worker). Unproven: 5.2 and 2.1 overlap not reproduced offline; StoryEventShots now assert nobody inside anybody. #784's proof is in the Explicit StoryEventShots class: needs a non-Explicit PlayMode test (Done-when) → round 2 in the Editor stint. Merged (e9cf0c24); merged tree offline csc 0 errors on 9 assemblies.

### #768 / #769 / #762
- round 1 (b8a3d62d + a4408156, a pair): fields fert 1.6→3.0, water 0.3→1.2, ≥1.1 off the green (3 seeds table in report); racks never under a field, placer keeps fields off racks (RackLotTests); church lot+0.25 clear of trees (ecology OFF only: 18/11/12→0; ecology ON 0 before and after). SimTests 2170/0. #768/#762 accepted. #769 → round 2: reproduce with ecology on (eco woods spreading round the church), fix the spread, test.
- 23:35 SimTests on p9 (#782,#777,#783,#774,#781,#765,#770) 2191/0. (#779 merged after the build: covered in the next run.)

### #766
- round 1 (e76ffa3f): BookView.Open closes person/crowd/building cards + HUD sheet; CardStackView.Covered while the book is open. OneSheetPlayTests 2/2 (both sizes), smoke 2/2. Shot 750_2 looked at by me: the other card up, no note. Accepted, merged. Closable after the pass.
- Failures seen in its runs (to fix, no pre-existing excuses): BookPlayTests ASwipeTurnsAndATapDoesNot + TheRibbonComesWithANewPageAndGoesWhenRead (fail on base too), CardStackPlayTests.LongCardsScrollInWholeLinesInsideTheSheet(1), PersonCardPlayTests.TapADollOpensTheirCardAndTheirLines (#118). → a lane B fix ticket with #118.
- 23:45 lane B #763 worker (a17c84a9e4a669a3c) on vs/763-feather-pole in pt1b (from p9 9f6201d1).
- 23:50 phase-1 lane A workers: #771 wolf turn (afbc1ffd73db8e106, wt 771), #767 rod chat (a403f8f92914fad1f, wt 767).

### #771
- round 1 (3fa9d148): QuadTurn (accel 700, cap 150 on the spot, 7° dead-zone, 0.12 s hold, move hysteresis) in WolfView only; QuadTurnTests 9/9 (standalone dotnet). QuadReel clip "wolf-idle-turn" (new vs old side by side) to render in the Editor pass. Deer/dogs share the linear pattern: not changed (note for the user). Accepted, merged.
- AnimBench.Tests doesn't build on dev (CampScenes6.cs:989 ConsequenceMeshes missing from AnimBench.csproj): fix in the pass.
- round 2 (956550b9): EcoModel.NoWood hook (TakesSeed) → Settlement.WoodKeptOff (church lot+0.25); ClearWoodRound at build. Ecology-on tests: spread seed 7 61→0, pre-existing 109→0; SimTests 2172/0. Accepted, merged (all three). Screenshots in the pass.
- 00:05 AnimBench build: rotten beyond ConsequenceMeshes (DeerView, CampLife, PegDoll, Mathf.Acos shim, CharmTie): reverted my try; file an issue at the end.

### #767
- round 1 (3cffc568): cause CampDay.Pastime partner → PeopleView faced the partner; head is baked into the body mesh, so a 22° shoulder turn toward the partner, rod stays over the water (FishBeats.ChatYaw). Cheat "Fishers chat now". FishBeatsTests new case. Worker's SimTests 1481 (low count: rerun on the merge). Accepted, merged; clip in the pass.
- 00:20 SimTests on p9 (all lane A merged: + #779, #768, #771, #767) 2204/0.

### #763
- round 1 (a4450a3c): cause z-fighting inside each feather (blade 0.006, spine proud by 0.0015, tip overlapping), not shadows (0.208 vs 0.213) nor rebuilds (mesh/transform asserted unchanged). Fix: feathers rolled ±30°, tip on the blade end, spine 0.012. FeatherPoleSteadyPlayTests 2/2 (speckle 0.21→0.069, assert <0.15), FeatherPolePlay 1/1, smoke 2/2. Crops looked at by me. Accepted, merged; cause posted on the issue. Seen: the pole stood inside a thicket (seed 4242): note.
- 00:45 lane B #780 worker (af8add6cb65602871) on vs/780-ask-fit (from 1bff61b1).
- (clock note: times above from 22:45 on were my estimates; real clock at this line: 23:45)

### #780
- round 1 (d4340d89): .kg-sheet.cards--grow (height auto, 30-60 %) while a choice face is drawn. AskFitPlayTests 2/2 (failed on 4.1 before the fix), DilemmaCard 9/9, smoke 2/2. 10 shots; I looked at 4.1 and supper at 750: clear. Accepted, merged (23:57).
- CardStackPlayTests.LongCardsScrollInWholeLinesInsideTheSheet(1) fails on base too (2.68 vs 3.0): into the test-fix ticket. Polish note: the caption pill covers the top-right chat icon (supper shot).
- 23:58 Editor restarted (3 tickets). Lane B #779/#764/#784 Unity half (ae45745746b6acbd5) on vs/779-unity.
- round 2 Unity (00:55, c395eacd on vs/779-unity): 1.2 step-back swap bug + meet in a crowd solid fixed; #764 PlayMode 4/4, vows frame with the line; #784 non-Explicit cheat tests pass; 3 StoryEventShots now in CI. Short: wedding frame still shows a guest merged with the laundry extra; assertion narrowed to cast pairs → round 3 (camp life out of the held scene's ring, assertion restored). #764, #784 accepted.
- round 3 (01:51, 179f90b0): PeopleView heldSpots + ApartFromHeld (camp-life spots step 0.09 off held people; slots apart from earlier claims). Assertion: every standing pair within 0.4 u of the scene (own ≥0.07, others ≥0.05). Shots 3x each 9/9; View.Tests 935/0 (+1 BoxLeak inconclusive by design), smoke 2/2. ADay_DollsWalk failed on base too: fixed (visible dolls only). Wedding r3 frame looked at by me: apart. Accepted, merged. #764/#784 closable after the pass; #779 closable after the pass too (no look needed? → it asks shots: on the sign-off page as evidence, close).
- 01:52 lane B #775 (ab8967bf521446c49) on vs/775-dirty-glass.
- 01:53 phase-1 lane A: #118+play test fixes (a51db2b7c04c594b2, wt 118), #559 trash/weeds meshes (a937add504e024d5e, wt 559).

### #559
- phase 1 (01:54, 3507666e): WeedMesh rewritten (15 thin blades, 3 seed stalks, dead-brown foot, no dome), WeedLookTests 2. Trash: no trash mesh exists (removed by decision 2026-11-02, #313) — verified in decisions.md row 114. Call: rework weeds only; the Editor shot confirms nothing boulder-like near the tents; on the sign-off page as a note. Editor tuning next (after #775).

### #118 + base failures
- phase 1 (01:58, 5ee9d150): LongCards — GlassScroll.TallestRow counted rows taller than the room (code bug, fixed); ASwipe — Drag step in px was 0.05 (test bug, fixed); TapADoll — jump check allows speed x clamped dt, re-baselines dolls back in sight, message names causes (cause unproven); Ribbon — cause not found (hypothesis: cascade from the swipe timeout). OnLog stack lines asked. Editor run next.
- OnLog stacks 18ee4d7c. Merged into p9 (unverified in Unity: the pass runs PersonCard/Book/CardStack 3x).

### #775
- round 1 (02:06, 82807eaa): cover curve 1.05→0.95, PatchAlpha 0.66→0.76, faded pane keeps up to 60 % capped 0.4; GrimeLook 3/3, smoke 2/2. My look at the sheets: before ≈ after, zoomed shots show no film → round 2 (obvious difference, numbers).
- round 2 (02:13, efcbdb9f): cover smoothstep(0,0.8), keep 0.85, cap 0.52, PatchAlpha 0.84, darker brown. Centre-band diff vs clean: home dirty 18.7→30.0, zoom dirty 13.0→48.1, zoom half 6.8→40.2. GrimeLook 3/3, GrimeMatrix 3/3, CameraGlassFeel 3/3, smoke 2/2. After sheet looked at by me: clearly grimier, film stays zoomed. Side effect for the page: the first box's opening grime limit 0.3→0.33 (measured 0.3004). Accepted, merged; waiting on the user's look.
- 02:14 lane B #559 tuning (aa99a4b424c5a9bb2) on vs/559-weeds-tune.
- tune round 1 (02:22, 0289920e): WeedScale 2.4→3.1, straw colours. My look: weeds taller than the tent (giant reeds) → round 2 (low and wide, before/after pair).
- tune round 2 (02:32, 8de46ffa): WeedScale 2.4, 34 blades, low (tallest ≈ a doll), tiny heads; WeedLookTests bounds lowered with reason; WeedLook 2/2, WritingWeeding 30/30, ToolGrip 12/12, Tweezers 5/5, smoke 2/2. before/tuned camp_compare looked at by me: a flowering bush → scruffy straw tufts. Accepted, merged; user's look on the page.
- 02:33 PASS started: Editor restarted, pt1b detached at p9 36d152ce, recompile 0 errors; EditMode 10 assemblies running (bw6msr65r); SimTests on the tip running.
- 02:41 SimTests on the tip 36d152ce: 2204/0.
- 03:04 EditMode 10 assemblies: Sim 1193/0 (2 skipped by design: .NET-pinned hashes), Life 646, View 937 (+1 BoxLeak inconclusive by design), Cards 46, Audio 53, UI 49, Moments 45, Shaping 54, ShowMe 134, TLS 40. Zero failures.
- 03:31 PlayMode: OneSheet 2, FeatherPoleSteady 2, FeatherPole 1, AskFit 2, DilemmaCard 9, StagedMoment 4, StoryEventShots 5, Tweezers 5, FisherShore 1, CameraGlassFeel 3, CardStack 20/20 x3, GrimeLook 3, smoke 2, SlicePlaythroughSmoke 2: all green. PersonCard 13/14 then 14 x2 (TapADoll: doll 16 walked 0.627 u in 0.86 s, limit 0.587, worst step 0.006: no jump, the new way-limit too tight). BookPlayTests Ribbon fails 3/3 (MissingReferenceException in GetComponent; stack lines stop in Unity). → Editor fix worker.
- #118 round 2 (03:46, 7b98628e): TapADoll way limit = 0.05 + DollStepWatch.FastestDrive (1.0) x elapsed (walkers legitimately reach ~1.0 u/s via DrawnSlack); 14/14 x5. Ribbon: real bug, LaundryView.Tick (LaundryLines.cs:175) touched a cached GameObject destroyed by a box rebuild → drop and redraw; BookPlayTests 5/5 x3. OnLog keeps 14 stack lines. Smoke 2/2. #118 closable.
- 03:47 media worker (a86a7d4bcf1910f07) on vs/media-pn1. Filed #786 (AnimBench build), added to the board. tooling.md entry committed (e1ebd49d).
- 04:30 media done (715e893c on vs/media-pn1; files in pt1b Temp/agent/media/). Weak: #772 ball clip (no miss on the pane), #770 kids' ring (none in frame); #770 festival still: a dancer in the well's lot, ring against a house → #770 round 2 (lane A, building rectangles + festival props). Fields before/after (seed 7) looked at: before ring the market, after at the east edge.
- 04:35 tester (a3b4daa51a1fc6906) stopped after 4 min claiming no time; told to continue. Its T01 log: #564 said once via the real mist, first run ends normally. Sign-off page drafted in scratchpad/pn1page (missing 775/559/763 media: Temp/agent/775, 559, 779 folders and 763 crops vanished during the run).
- 04:36 tester partial (stops every ~5 min): scene sanity 1179 before 19 roots/0 orphans/15 MF/1 glass set, after 22/0/16/1 (no pane leak). #564 said once at both sizes via the real mist. [real] the #564 caption under the first run's Skip step/Skip all pills (+ the "Tap to name you…" plate label off the right edge). [polish] people ~12 px at the 1179 home view in the first run. Fix worker (aee6bbcbdbd9be93a) on vs/564-caption-skip, also re-shooting 775/559 into scratchpad/reshoot.
- 04:41 #770 round 2 (95f8a50c): FootprintBlocked adds every building lot rectangle (+margin, yaw both ways); DanceClearTests 26 (11 fail with it off); SimTests 2212/0. Round 3: FestivalDressing still centred on the green → follow the ring.
- 04:52 #770 round 3 (265772b5): FestivalLayout (pure) puts poles 0.08 outside the staged ring, tables beyond, off lots; DanceClearTests 34; SimTests 2220/0. Merged into p9. Unity: MomentPlayTests + the clip after the caption fix.
- 05:04 tester [real] fixed (vs/564-caption-skip): first frame of a caption had no layout, the skip row didn't step under it; plate label first frame at the old left. CaptionBound fallback + CaptionShown event, PlacePlate hides until placed. FirstRunWordsClear 2/2 (failed twice before), FirstRun 3/3, FirstRunPolish 10/10, smoke 2/2. Re-shoots 775/559 in scratchpad/reshoot. Merged.
- 05:13 SimTests on 7c87414d: Failed:     0 Passed:  2220 
- 06:30 final verify (48a0266b, merged ddc1b5c0): Life 662/662, View 937 (+1 BoxLeak inconclusive by design), ShowMe 134/134; PlayMode MomentPlayTests 10/10 (WatchHurryCheer fixed: since #587 one finger pans, the drag check now asserts the camera moved), StoryEventShots 5/5, FirstRunWordsClear 2/2, PersonCard 14/14, Book 5/5, StagedMoment 4/4, SmokePlay 2/2, SlicePlaythroughSmoke 2/2. #770 festival clip good (8 dancers on open grass, poles round the ring, tables outside, fire/house/well clear). #772 clip still misses: the cheat's centre HX-0.5 is too far for a miss to reach the pane, and the shot followed a natural ball game → cheat fix worker (ad7e360c) on vs/772-cheat.
