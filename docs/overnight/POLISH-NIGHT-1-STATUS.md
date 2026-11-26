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
