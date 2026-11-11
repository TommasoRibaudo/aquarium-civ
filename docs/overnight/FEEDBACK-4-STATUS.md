# Feedback batch 4: status (resume file)

Prompt: `docs/prompts/feedback-batch-4-run.md` (builds 24/25, notes #527-#537). Integration branch `p9/feedback-4` from
origin/dev (edfdac3), worktree `F:\GameDev-wt\feedback4`.

**Mode:** hybrid (workflow.md §7). Lane A offline workers for #495, #541, #538 (and chained #539, #543 after #495);
one Editor worker at a time: #526+#540, then #542, then the lane B parts; then one batched Editor pass.
**Models:** the user wrote "use sonnet agents" above the prompt, so every worker is Sonnet, including #542 and #543
(the prompt had Opus for those two). Call made by the orchestrator, 2026-11-10.

## Checklist
- [x] `p9/feedback-4` from `origin/dev`; this prompt committed
- [x] 1 · #495 merged (fisher on the water's edge; the shared "future water" helper)
- [x] 2 · #539 merged (settlers' camp and its fire)
- [x] 3 · #541 merged (kids play together)
- [x] 4 · #538 merged (open for feel sign-off)
- [x] 5+6 · #526 and #540 merged (#526 open for look sign-off)
- [x] 7 · #542 cause written in the issue, fix merged
- [x] 8 · #543 merged (open for look sign-off)
- [ ] Batched Editor pass green (EditMode 10 assemblies, PlayMode classes, smoke); failures fixed or noted as baseline
- [ ] `Feedback4Playthrough` + the touch play-through done, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released
- [ ] PR → `dev`, CI green, merged
- [ ] Final report to the user

## Log
- 14:19 run start. Editor closed, lock free, health 0 (C: 28 GB). Issues and notes read; shots of #530/#531/#532/#537 looked at.
- 14:35 Workers started (all Sonnet): lane A #495 (wt 495, + shared water helper), #541 (wt 541), #538 (wt 538); Editor worker #526+#540 in F:\GameDev (vs/526-540-camp-props). Watcher waits for its lock (300 s quiet). Shared rules: scratchpad/worker-rules.md.
- 14:30 #538 merged (3faf03f, branch 50e510f): view-limited PanBoundsFor now = glass range (PanScale draw-in + soft band kept). SimTests 1721/1721. Verify: BoxCameraTests, CameraGlassFeel category (new FreePan_FromTheCamp), CameraModePlayTests, smoke; shots p538/*. Open for feel sign-off.
- 14:30 #541 merged (branch c58233a): cause = KidPlayDirector grouped per Place, so two lone kids at two places each drew Ball solo. PlayGroups.Gather (reach 3.2, same class) + Differ. Cheat 'Two kids play'. Verify: PlayGatherTests, PlayGroupTests, KidsTogetherPlayTests (shot two-kids-together).
- 14:40 #495 merged (9f8ea1c, branch 2205826): Pond shore from eco cover (new Sim/Eco/PondShore.cs: IsWet, NearestShore, FutureWater mask for #539); SimCore.FishingWater = FishingOpen && OpenWater gates the planner; PeopleView.DrawStand + ApartGroupsView FishSpot follow the shore each check. Cheat 'Pond +1 step'. Verify FisherShorePlayTests (Fishing), FishingPlayTests, PondLife. Risk: DrawStand scans cells per frame per fisher. #539 and #543 workers started from it (Sonnet).
- 14:40 Watcher STALL (5 min quiet): not a dialog; the #526/#540 worker's blocking EditMode run (Life tests). Restarted watcher with a 900 s window.
- 14:52 SimTests: #541 merge 1727/1727; #495 merge 1732/1732 (the #495 worker's host crash didn't repeat: likely two suites at once).
- 14:42 #539 merged (branch b01dc3d): SimCore.ApartCampSpot = one rule (FreeSpot/ShoreFree avoid PondShore.FutureWater); woods camp off the basin+margin; shore hut just outside the basin; spot re-asked every 5 s (a flooded camp moves at the next check, possibly in view: polish note); PoolReach clips the settlers' fire pool to future water. Cheats Settlers: woods/shore, Pond to full. Verify ApartPondPlayTests (4 shots 539-*), ApartGroupsPlayTests, smoke.
- 14:44 #526+#540 merged (4556294, branch 69281b2): the #537/#499 poles = ConsequenceKind.Feather (storyteller tradition prop by the tent door): now a pale stake in a stone ring with 3 fanned feathers (cream/yellow/red). #540: pure Life/CampSpots places fire-anchored consequence props clear of rack/stash/stone/pot/tents (view only, sim spot untouched); rack angle nudged off them. Cheat 'Camp props: show all'. Life.Tests 389/389, CampSpots 4/4, smoke 2/2. Looked at a_night_default, a_day_fire_close, a_day_feather_close: pot and rack apart, stakes read. #526 open for look sign-off. Worker moved the untracked prompt file aside in F:\GameDev (it's tracked on the branch now).
- 14:53 SimTests: #539 merge 1735/1735; #526/#540 merge 1739/1739. #542 Editor worker started (Sonnet) on vs/542-duck-placing in F:\GameDev; watcher waits for its lock (600 s).
- 14:55 #543 offline merged (2ed92e5, branch 276cd99): fish basket (PropKind.FishBasket creel), FishCaught→sim basket, HangFish at PlaceKind.FishRack, dries a day, RackKeeper TakeDownFish→pile shows dried fish (StashProps). Living land only. Cheats Fill fish basket / Fish on the rack / Dried fish to pile. SimTests 1745 (worker), AnimBench 543-fish-rack. Offline csc on merge 0 errors (6 assemblies). Follow-up sent: FishRackSpot via CampSpots (vs/543-rack-spot). Editor phase (FishChainPlayTests shots, AnimLab) in the pass.
- 15:05 SimTests on the #543 merge: 1751/1752 (1 failure; rerun to name it). #543 rack follow-up merged (59ee43d, branch 9f1df55: CampSpots.FishRackAngle, seats as discs, props avoid the fish rack; its branch 1753/1753).
- 15:05 #542 merged (639fd8f, branch eff2a9a). Cause (commented on #542): at default zoom the pond is ~100 px at the box edge and its glow can't be seen; the first tap leaned to zoom 0.15 on the ghost so the pond was cut off, and the confirm only counted within 0.32 u of the ghost (other water moved the ghost). Also the tap ray hit the pond bed (neighbour invalid cell). Fix: ducks/fish frame the whole pond on taking the tool (zoom 0.3-0.6), any valid water tap confirms at the ghost, captions say water, TapAt resolves water species on the water surface. PondPlacingPlayTests 2/2, LifeSlot 4/4, LifeArrival 11/11, SeedGhost 4/4, GhostFrame 3/3, SmokePlay 2/2. Looked at ducks-2-ghost: pond framed, caption clear; the duck ghost is small. Editor tickets so far: 2 (restart before the pass).
- 15:11 Final lane merge: offline csc 0 errors (7 assemblies), SimTests 1753/1753 (the 1751/1752 failure was FishChainTests.TheHideRackAngleIsThePlannersOwn vs #540's rack nudge, fixed by the follow-up). #543 checked off (open for look sign-off; its Editor look in the pass). Batched pass started (Sonnet) on vs/feedback4-pass in F:\GameDev; watcher 900 s.
- 15:16 Feedback4Playthrough written offline (branch 2c20f37, merged): beats a-g, Beat() soft failures, [f4] STATE CHEAT logs. Run after the pass.
- 16:50 batched pass done, merged 8d61af4. EditMode 10 asm green (View: 1 test-side enum fix). PlayMode 21 classes green after test-side fixes (FishChain late catch, FisherShore walking fisher); CarePlayTests glass budget = known flake. Odd: run-tests.sh printed previous-run totals for short runs (Life/Audio) -> counts checked from console. Pile fish read as a plank (#543 real) + AnimLab 543 framing broken -> Editor block 2 (vs/feedback4-play) also runs Feedback4Playthrough.
- 17:20 block 2 merged: #543 pile fish now broadside on a dark mat (looked: reads as fish); AnimLab gained a "rack" kind (sheet: hang judgeable); Feedback4_PlayedByTouch 1/1, 0 soft fails, 20 state cheats (logged), beats a-g looked at; SmokePlay 2/2. Next: tester.
- 18:40 user stopped the tester (no findings recorded) and asked for an Editor walkthrough to sign off ALL open questions: OpenQuestionsSignOff (65 stops, menu Aquarium Civ/Open questions sign-off) merged. PR waits for the user's sign-offs.
