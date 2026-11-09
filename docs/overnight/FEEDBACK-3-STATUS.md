# Feedback batch 3: status (resume file)

Asked by the user 2026-11-09: "run the batch as a hybrid". Same shape as batch 2 (`docs/prompts/feedback-batch-2-run.md`:
guardrails, the test runner, the pass, the play-through, the tester, the PR); this file is the resume point.
Integration branch `p9/feedback-3` from origin/dev (750da52), worktree `F:\GameDev-wt\feedback3`.

**Mode:** hybrid (workflow.md). Editor-free parallel workers for #519 (sim), #521, #522, #523, #520 (scheduler), #518
(seed flow + pan guard, logic and PlayMode test written offline). One Editor worker at a time for the looks: #524, then
#525 + #526, then #516 (after #518 is merged: both touch the kit/ghost code). Then one batched Editor pass, the touch
play-through, the tester, PR → dev.

Notes → issues: #509/#510/#511→#516 (#511 closed), #513→#518, #515→#519, #512→#520, #508→#521, #503→#522, #500→#523,
#505→#524, #514→#525, #499→#526, #501→#419 (no work). #517 closed (user: the placed thicket reads fine; the ghost doesn't).

## Checklist
- [x] `p9/feedback-3` from origin/dev
- [x] #519 ducks only where water is drawn (sim) merged
- [x] #521 first camp days spread out merged
- [x] #522 crowd line names the speaker merged
- [x] #523 no weeds on the trodden ground merged
- [x] #520 barren wind repeat merged (open for a listen)
- [x] #518 seed ghost + two-finger guard merged (open: seed-in-jar question)
- [ ] #524 wet at the glass merged (open for look)
- [ ] #525 + #526 tree glow, tent poles merged (open for look)
- [ ] #516 placing life: ghost, glow, button merged (open for look)
- [ ] Batched Editor pass green (EditMode by assembly, PlayMode classes, smoke)
- [ ] Touch play-through, every beat looked at
- [ ] Tester run; [real] fixed; [polish] listed
- [ ] Issues closed / sign-off comments; answered notes closed
- [ ] PR → dev, CI green, merged
- [ ] Final report; docs/open-questions.md updated

## Log
- 2026-11-09 run start. Editor closed, lock free, health OK (C: 22 GB).
- Lane A started in parallel (worktrees F:\GameDev-wt\<n>): #519 (Opus), #521, #522, #523, #520 (Sonnet), #518 (Opus: input). Editor worker #524 (Sonnet) on vs/524-wet-glass in F:\GameDev; stall watcher waits for its lock. Next in the Editor queue: #525+#526, then #516 (after #518 merges).
- 16:51 #522 merged (b5225b2, branch 1c9efdd): Who = notable name, else crowd first name (SimState.crowdNames), else null; caption hides an empty attribution (GlassCaption.Set). Verify: LineRouteTests (3 new), PeopleWiringPlayTests, CaptionEveryLinePlayTests, caption shot.
- 16:55 Session interrupted ~16:55. Resumed all 6 workers from their transcripts (#521, #523, #524 had uncommitted work; #519, #520, #518 none yet). #524 still holds the lock; stall watcher restarted.
- 16:57 #520 merged (3ccb8f9, branch 65ca9d0): the 40 s barren_wind_loop was the repeat (batch 2's 'left' was wrong). BarrenWind: 2 voices (B pitch ~0.87, ~46 s), wandering gain 0.3-0.9, pitch drift ±3.5%; cue 0.9→0.68; fade smoothstep^1.5. Offline csc merge 0 errors (list ShowMe too). Verify: AquariumCiv.Audio.Tests; Audio status readout; a listen (needs:user).
- 17:12 #518 merged (a42d1a8, fa1cd13): KitPlace shared ghost flow (KitPress two-finger guard, GhostTap, GhostLook), SeedPlaceView, mister/tweezers/cloth wait 0.1 s for a 2nd finger. Cheat Seed in hand. Verify: SeedGhostPlayTests (4) + LifeSlot, LifeArrival, Tweezers, EcoMistView, Care, GestureEdge, FirstRun, smoke. Risks: wipes 0.1 s late, slow-frame mister test.
- #519 merged (dd97681; I committed the worker's files): root cause of #515 = SimCore.Story random duck story fired on ecology boxes (now !EcologyOn only). Duck/fish ground = OpenWater; jar/adoption wait for water; flocks keep to water or leave with a line (wording mine-to-confirm). Cheat Pond dries now. SimTests 1709/1709 on the merge.
- #521 merged (835e6b4): cause = SpreadDiggers skipped before the camp gate + JobWork sent all to Stream. Now ≤2 diggers at 3 patches, cook at fire, others water/door chores, lunch in turns. CampSpreadTests (5). Verify Life.Tests + first-morning shot; dig patch offsets untested in Editor.
- #523 merged (172596e): IsTroddenGround (fire ring 1.15 + SettledCell); MessProps FindSpot skips trodden, outward search fallback; PullTroddenVine each EcoStep. TroddenGroundTests (5).
- #524 merged (4f0ea5b): EcoWetFace strip + AtEdge. Reviewed shots: effect doesn't read; sent back for one stronger iteration (30 min). Merge offline csc 0 errors (11 assemblies); dotnet on full merge running.
